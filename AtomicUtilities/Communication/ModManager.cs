using System;
using System.IO;

namespace AtomicFramework.Communication
{
    public class ModManager: IDisposable
    {
        private readonly Stream inStream;
        private readonly Stream outStream;

        private readonly BinaryReader input;
        private readonly BinaryWriter output;

        public ModManager()
        {
            inStream = Console.OpenStandardInput();
            outStream = Console.OpenStandardOutput();

            input = new(inStream);
            output = new(outStream);
        }

        public ModManager(StreamReader input, StreamWriter output)
        {
            inStream = input.BaseStream;
            outStream = output.BaseStream;

            this.input = new(inStream);
            this.output = new(outStream);
        }

        ~ModManager()
        {
            Dispose();
        }

        public void Dispose()
        {
            input.Dispose();
            output.Dispose();

            inStream.Dispose();
            outStream.Dispose();
        }

        public Packet? ReadPackets()
        {
            return Packet.Read(input);
        }

        public void WritePacket(Packet packet)
        {
            output.Write(packet);
            output.Flush();
        }

        public Logger CreateLogger()
        {
            return new Logger(output);
        }

        private enum Functions
        {
            LOG,
            DISABLE_PLUGINS
        }
    }
}
