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

        public ModManager(StreamWriter input, StreamReader output)
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

        public void WritePlugins(string[] plugins)
        {
            output.Write((ushort)plugins.Length);

            foreach (string plugin in plugins)
                output.Write(plugin);
        }

        public string[] ReadPlugins()
        {
            ushort len = input.ReadUInt16();
            string[] plugins = new string[len];

            for (ushort i = 0; i < len; i++)
                plugins[i] = input.ReadString();

            return plugins;
        }
    }
}
