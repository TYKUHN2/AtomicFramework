using AtomicFramework.Communication;
using System.ComponentModel;
using System.Diagnostics;
using System.Reflection;
using System.Runtime.InteropServices;
using System.Text;

namespace AtomicFramework
{
    internal static class Program
    {
        public static string? BEP_PATH
        { get; private set; }

        public static string? MANAGED_PATH
        { get; private set; }

#pragma warning disable CS8618
        public static Logger Log
        {
            get; private set;
        }

        static Program()
        {
#pragma warning restore CS8618
            // TODO: Get Steam path from HKCU/Software/Valve/Steam/SteamPath and read steamapps/appmanifest_2168680.acf to find game folder
            // TODO: On linux, use ~/.steam/steam/ for steam path instead of HKCU
            BEP_PATH = Environment.GetEnvironmentVariable("BEP_PATH");
            if (BEP_PATH != null && !Path.IsPathRooted(BEP_PATH))
                BEP_PATH = null;

            MANAGED_PATH = Environment.GetEnvironmentVariable("MANAGE_PATH");
            if (MANAGED_PATH != null && !Path.IsPathRooted(MANAGED_PATH))
                MANAGED_PATH = null;

            if (BEP_PATH == null || MANAGED_PATH == null)
            {
                if (BEP_PATH != null)
                    MANAGED_PATH = Path.GetFullPath("../NuclearOption_Data/Managed", BEP_PATH);
                else if (MANAGED_PATH != null)
                    BEP_PATH = Path.GetFullPath("../../BepInEx", MANAGED_PATH);
                else
                {
                    MANAGED_PATH = Environment.GetEnvironmentVariable("NUCLEAR_OPTION_REFERENCES");

                    // If we don't set BEP_PATH, we won't continue processing, so we don't need to reclear MANAGED_PATH
                    if (MANAGED_PATH != null && Path.IsPathFullyQualified(MANAGED_PATH))
                        BEP_PATH = Path.GetFullPath("../../BepInEx", MANAGED_PATH);
                }
            }

            if (BEP_PATH != null)
            {
                string basePath = Path.Combine(BEP_PATH, "core");
                AppDomain.CurrentDomain.AssemblyResolve += (sender, args) =>
                {
                    string assemblyPath = Path.Combine(basePath, new AssemblyName(args.Name).Name + ".dll");
                    if (!File.Exists(assemblyPath)) return null;
                    Assembly assembly = Assembly.LoadFrom(assemblyPath);
                    return assembly;
                };

                Assembly.LoadFile(Path.Combine(BEP_PATH, "plugins", "AtomicFramework", "AtomicFramework.dll"));
            }

        }

        [STAThread]
        static void Main(string[] args)
        {
            using Communication.ModManager comm = new();
            Log = comm.CreateLogger();

            WarningWriter writer = new();
            Console.SetOut(writer);
            Console.SetError(writer);

            try
            {
                if (!args.Contains("restart"))
                {
                    ApplicationConfiguration.Initialize();

                    ModManager window = new();

                    Application.Run(window);

                    Plugin[] disabled = window.GetDisabled();

                    string[] NATIVE_DISABLE = [.. disabled.Where(p => p.atomicVersion == null).Select(p => p.guid)];
                    string[] ATOMIC_DISABLE = [.. disabled.Where(p => p.atomicVersion != null).Select(p => p.guid)];

                    comm.WritePacket(new DisablePluginsPacket(NATIVE_DISABLE, ATOMIC_DISABLE));
                }
                else
                {
                    if (BEP_PATH == null)
                        return;

                    DisablePluginsPacket packet = (DisablePluginsPacket)comm.ReadPackets()!;
                    ProcessStartInfo si = new(Path.Combine(BEP_PATH, "../NuclearOption.exe"))
                    {
                        UseShellExecute = false,
                        WorkingDirectory = Path.Combine(BEP_PATH, "..")
                    };
                    si.Environment.Remove("BEP_PATH");
                    si.Environment.Remove("MANAGE_PATH");

                    string fullPath = Path.GetFullPath(si.FileName);

                    Process? parent = Process.GetProcesses().FirstOrDefault(p => p.MainModule?.FileName == fullPath);
                    if (parent != null)
                    {
                        if (ProcessCommandLine.Retrieve(parent, out string? cmdline) == 0)
                        {
                            IReadOnlyList<string> nArgs = ProcessCommandLine.CommandLineToArgs(cmdline!);
                            foreach (string arg in nArgs)
                                si.ArgumentList.Add(arg);
                        }

                        parent.Kill();
                    }

                    Process.Start(si);

                    comm.Poll();

                    comm.WritePacket(packet);
                }
            }
            catch (Exception ex)
            {
                Log.LogError(ex);
                throw;
            }
        }

        private class WarningWriter(): TextWriter
        {
            public override Encoding Encoding => Encoding.UTF8;

            public override void Write(char _)
            {
                
            }
        }
    }

    // Credit: https://stackoverflow.com/a/46006415
    internal static class ProcessCommandLine
    {
        private static class Win32Native
        {
            public const uint PROCESS_BASIC_INFORMATION = 0;

            [Flags]
            public enum OpenProcessDesiredAccessFlags : uint
            {
                PROCESS_VM_READ = 0x0010,
                PROCESS_QUERY_INFORMATION = 0x0400,
            }

            [StructLayout(LayoutKind.Sequential)]
            public struct ProcessBasicInformation
            {
                public IntPtr Reserved1;
                public IntPtr PebBaseAddress;
                [MarshalAs(UnmanagedType.ByValArray, SizeConst = 2)]
                public IntPtr[] Reserved2;
                public IntPtr UniqueProcessId;
                public IntPtr Reserved3;
            }

            [StructLayout(LayoutKind.Sequential)]
            public struct UnicodeString
            {
                public ushort Length;
                public ushort MaximumLength;
                public IntPtr Buffer;
            }

            // This is not the real struct!
            // I faked it to get ProcessParameters address.
            // Actual struct definition:
            // https://learn.microsoft.com/en-us/windows/win32/api/winternl/ns-winternl-peb
            [StructLayout(LayoutKind.Sequential)]
            public struct PEB
            {
                [MarshalAs(UnmanagedType.ByValArray, SizeConst = 4)]
                public IntPtr[] Reserved;
                public IntPtr ProcessParameters;
            }

            [StructLayout(LayoutKind.Sequential)]
            public struct RtlUserProcessParameters
            {
                [MarshalAs(UnmanagedType.ByValArray, SizeConst = 16)]
                public byte[] Reserved1;
                [MarshalAs(UnmanagedType.ByValArray, SizeConst = 10)]
                public IntPtr[] Reserved2;
                public UnicodeString ImagePathName;
                public UnicodeString CommandLine;
            }

            [DllImport("ntdll.dll")]
            public static extern uint NtQueryInformationProcess(
                IntPtr ProcessHandle,
                uint ProcessInformationClass,
                IntPtr ProcessInformation,
                uint ProcessInformationLength,
                out uint ReturnLength);

            [DllImport("kernel32.dll")]
            public static extern IntPtr OpenProcess(
                OpenProcessDesiredAccessFlags dwDesiredAccess,
                [MarshalAs(UnmanagedType.Bool)] bool bInheritHandle,
                uint dwProcessId);

            [DllImport("kernel32.dll")]
            [return: MarshalAs(UnmanagedType.Bool)]
            public static extern bool ReadProcessMemory(
                IntPtr hProcess, IntPtr lpBaseAddress, IntPtr lpBuffer,
                uint nSize, out uint lpNumberOfBytesRead);

            [DllImport("kernel32.dll")]
            [return: MarshalAs(UnmanagedType.Bool)]
            public static extern bool CloseHandle(IntPtr hObject);

            [DllImport("shell32.dll", SetLastError = true,
                CharSet = CharSet.Unicode, EntryPoint = "CommandLineToArgvW")]
            public static extern IntPtr CommandLineToArgv(string lpCmdLine, out int pNumArgs);
        }

        private static bool ReadStructFromProcessMemory<TStruct>(
            IntPtr hProcess, IntPtr lpBaseAddress, out TStruct? val)
        {
            val = default;
            var structSize = Marshal.SizeOf<TStruct>();
            var mem = Marshal.AllocHGlobal(structSize);
            try
            {
                if (Win32Native.ReadProcessMemory(
                    hProcess, lpBaseAddress, mem, (uint)structSize, out var len) &&
                    (len == structSize))
                {
                    val = Marshal.PtrToStructure<TStruct>(mem)!;
                    return true;
                }
            }
            finally
            {
                Marshal.FreeHGlobal(mem);
            }
            return false;
        }

        public static string ErrorToString(int error) =>
            new string[]
            {
            "Success",
            "Failed to open process for reading",
            "Failed to query process information",
            "PEB address was null",
            "Failed to read PEB information",
            "Failed to read process parameters",
            "Failed to read command line from process"
            }[Math.Abs(error)];

        public static int Retrieve(Process process, out string? commandLine)
        {
            int rc = 0;
            commandLine = null;
            var hProcess = Win32Native.OpenProcess(
                Win32Native.OpenProcessDesiredAccessFlags.PROCESS_QUERY_INFORMATION |
                Win32Native.OpenProcessDesiredAccessFlags.PROCESS_VM_READ, false, (uint)process.Id);
            if (hProcess != IntPtr.Zero)
            {
                try
                {
                    var sizePBI = Marshal.SizeOf<Win32Native.ProcessBasicInformation>();
                    var memPBI = Marshal.AllocHGlobal(sizePBI);
                    try
                    {
                        var ret = Win32Native.NtQueryInformationProcess(
                            hProcess, Win32Native.PROCESS_BASIC_INFORMATION, memPBI,
                            (uint)sizePBI, out var len);
                        if (0 == ret)
                        {
                            var pbiInfo = Marshal.PtrToStructure<Win32Native.ProcessBasicInformation>(memPBI);
                            if (pbiInfo.PebBaseAddress != IntPtr.Zero)
                            {
                                if (ReadStructFromProcessMemory<Win32Native.PEB>(hProcess,
                                    pbiInfo.PebBaseAddress, out var pebInfo))
                                {
                                    if (ReadStructFromProcessMemory<Win32Native.RtlUserProcessParameters>(
                                        hProcess, pebInfo.ProcessParameters, out var ruppInfo))
                                    {
                                        var clLen = ruppInfo.CommandLine.MaximumLength;
                                        var memCL = Marshal.AllocHGlobal(clLen);
                                        try
                                        {
                                            if (Win32Native.ReadProcessMemory(hProcess,
                                                ruppInfo.CommandLine.Buffer, memCL, clLen, out len))
                                            {
                                                commandLine = Marshal.PtrToStringUni(memCL)!;
                                                rc = 0;
                                            }
                                            else
                                            {
                                                // couldn't read command line buffer
                                                rc = -6;
                                            }
                                        }
                                        finally
                                        {
                                            Marshal.FreeHGlobal(memCL);
                                        }
                                    }
                                    else
                                    {
                                        // couldn't read ProcessParameters
                                        rc = -5;
                                    }
                                }
                                else
                                {
                                    // couldn't read PEB information
                                    rc = -4;
                                }
                            }
                            else
                            {
                                // PebBaseAddress is null
                                rc = -3;
                            }
                        }
                        else
                        {
                            // NtQueryInformationProcess failed
                            rc = -2;
                        }
                    }
                    finally
                    {
                        Marshal.FreeHGlobal(memPBI);
                    }
                }
                finally
                {
                    Win32Native.CloseHandle(hProcess);
                }
            }
            else
            {
                // couldn't open process for VM read
                rc = -1;
            }
            return rc;
        }

        public static IReadOnlyList<string> CommandLineToArgs(string commandLine)
        {
            if (string.IsNullOrEmpty(commandLine)) { return []; }

            var argv = Win32Native.CommandLineToArgv(commandLine, out var argc);
            if (argv == IntPtr.Zero)
            {
                throw new Win32Exception(Marshal.GetLastWin32Error());
            }
            try
            {
                var args = new string[argc];
                for (var i = 0; i < args.Length; ++i)
                {
                    var p = Marshal.ReadIntPtr(argv, i * IntPtr.Size);
                    args[i] = Marshal.PtrToStringUni(p)!;
                }
                return args.ToList().AsReadOnly();
            }
            finally
            {
                Marshal.FreeHGlobal(argv);
            }
        }
    }
}