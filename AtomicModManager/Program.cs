using AtomicFramework.Communication;
using System.Diagnostics;
using System.Reflection;
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
#pragma warning restore CS8618

        [STAThread]
        static void Main()
        {
            using Communication.ModManager comm = new();
            Log = comm.CreateLogger();

            WarningWriter writer = new();
            Console.SetOut(writer);
            Console.SetError(writer);

            try
            {
                ApplicationConfiguration.Initialize();

                ModManager window = new();

                Application.Run(window);

                Plugin[] disabled = window.GetDisabled();

                string[] NATIVE_DISABLE = [.. disabled.Where(p => p.atomicVersion == null).Select(p => p.guid)];
                string[] ATOMIC_DISABLE = [.. disabled.Where(p => p.atomicVersion != null).Select(p => p.guid)];

                comm.WritePacket(new DisablePluginsPacket(NATIVE_DISABLE, ATOMIC_DISABLE));
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
}