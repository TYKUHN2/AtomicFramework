using System.IO;

namespace AtomicFramework.Communication
{
    public class DisablePluginsPacket(string[] native, string[] atomic): Packet(Function.DISABLE_PLUGINS)
    {
        public readonly string[] native = native;
        public readonly string[] atomic = atomic;

        public override void Write(BinaryWriter output)
        {
            base.Write(output);
            WriteArray(output, native);
            WriteArray(output, atomic);
        }

        public static new DisablePluginsPacket Read(BinaryReader reader)
        {
            string[] native = ReadArray(reader);
            string[] atomic = ReadArray(reader);

            return new DisablePluginsPacket(native, atomic);
        }

        private static void WriteArray(BinaryWriter output, string[] arr)
        {
            output.Write((ushort)arr.Length);
            foreach (string s in arr)
                output.Write(s);
        }

        private static string[] ReadArray(BinaryReader input)
        {
            ushort length = input.ReadUInt16();
            string[] arr = new string[length];
            for (ushort i = 0; i < length; i++)
                arr[i] = input.ReadString();

            return arr;
        }
    }
}
