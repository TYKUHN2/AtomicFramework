using System.Diagnostics;
using System.IO;

namespace AtomicFramework.Communication
{
    public class Logger(BinaryWriter output)
    {
        private readonly BinaryWriter output = output;

        public void Log(LogPacket.LogLevel level, string data)
        {
#if DEBUG
            if (Debugger.IsAttached)
                Debugger.Log(0, Debugger.DefaultCategory, data + "\n");
#endif

            LogPacket packet = new(level, data);

            output.Write(packet);
            output.Flush();
        }

        public void Log(LogPacket.LogLevel level, object data) => Log(level, data.ToString());

        public void LogDebug(object data) => Log(LogPacket.LogLevel.Debug, data);
        public void LogInfo(object data) => Log(LogPacket.LogLevel.Info, data);
        public void LogWarning(object data) => Log(LogPacket.LogLevel.Warning, data);
        public void LogError(object data) => Log(LogPacket.LogLevel.Error, data);
    }
}
