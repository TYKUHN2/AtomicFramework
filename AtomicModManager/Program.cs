using AtomicFramework.Communication;
using System.Text;

namespace AtomicFramework
{
    internal static class Program
    {
#pragma warning disable CS8618
        public static Logger Log
        {
            get; private set;
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