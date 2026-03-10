using System;
using System.IO;

namespace AtomicFramework.Communication
{
    public class LogPacket(LogPacket.LogLevel level, string message): Packet(Function.LOG)
    {
        public readonly LogLevel level = level;
        public readonly string message = message;

        public override void Write(BinaryWriter output)
        {
            base.Write(output);
            output.Write((byte)level);
            output.Write(message);
        }

        public static new LogPacket? Read(BinaryReader reader)
        {
            LogLevel level = (LogLevel)reader.ReadByte();
            if (level > LogLevel.All)
                return null;

            return new LogPacket(level, reader.ReadString());
        }

        [Flags]
        public enum LogLevel: byte
        {
            None = 0,
            Fatal = 1,
            Error = 2,
            Warning = 4,
            Message = 8,
            Info = 16,
            Debug = 32,
            All = 63
        }
    }
}
