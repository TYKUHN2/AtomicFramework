using System.Diagnostics;
using System.Runtime.InteropServices;
using System.Text;

namespace AtomicFramework
{
    internal static class Program
    {
        [STAThread]
        static void Main()
        {
#if DEBUG
            if (GetStdHandle(StdInputHandle) == int.MaxValue)
            {
                SetStdHandle(StdOutputHandle, 0);
                AllocConsole();
            }
            else
            {
                AllocConsole();

                FileStream fs = new("CONOUT$", FileMode.Open, FileAccess.ReadWrite, FileShare.Write);
                Console.SetOut(new StreamWriter(fs) { AutoFlush = true });

                Console.WriteLine("Detected stdin communication.");
            }
#else
            Console.SetOut(new NullWriter());
            Console.SetError(new NullWriter());
#endif

            try
            {
                ApplicationConfiguration.Initialize();

                ModManager window = new();

                Application.Run(window);

                Plugin[] disabled = window.GetDisabled();

                using (Communication.ModManager comm = new())
                {
                    string[] NATIVE_DISABLE = [.. disabled.Where(p => p.atomicVersion == null).Select(p => p.guid)];
                    string[] ATOMIC_DISABLE = [.. disabled.Where(p => p.atomicVersion != null).Select(p => p.guid)];

#if DEBUG
                    Console.WriteLine("Dump");
                    Console.Out.WriteLine(string.Join(", ", NATIVE_DISABLE));
                    Console.Out.WriteLine(string.Join(", ", ATOMIC_DISABLE));
                    Console.Out.Flush();
#endif

                    comm.WritePlugins(NATIVE_DISABLE);
                    comm.WritePlugins(ATOMIC_DISABLE);
                }
            }
            catch (Exception ex)
            {
#if DEBUG
                Console.Error.WriteLine(ex);
                Process.GetCurrentProcess().WaitForExit();
#endif
            }
            
        }

        private const UInt32 StdOutputHandle = 0xFFFFFFF5;
        private const UInt32 StdInputHandle = 0xFFFFFFF6;

        [DllImport("kernel32.dll", SetLastError = true)]
        [return: MarshalAs(UnmanagedType.Bool)]
        static extern bool AllocConsole();
        [DllImport("kernel32.dll")]
        private static extern IntPtr GetStdHandle(UInt32 nStdHandle);
        [DllImport("kernel32.dll")]
        private static extern void SetStdHandle(UInt32 nStdHandle, IntPtr handle);

        private class NullWriter : TextWriter
        {
            public override Encoding Encoding => Encoding.UTF8;
        }
    }
}