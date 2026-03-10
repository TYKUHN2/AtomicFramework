using System.IO;

namespace AtomicFramework.Communication
{
    public abstract class Packet(Packet.Function function)
    {
        public readonly Function function = function;

        public virtual void Write(BinaryWriter output)
        {
            output.Write((byte)function);
        }

        public static Packet? Read(BinaryReader reader)
        {
            try
            {
                byte command = reader.ReadByte();

                return (Function)command switch
                {
                    Function.LOG => LogPacket.Read(reader),
                    Function.DISABLE_PLUGINS => DisablePluginsPacket.Read(reader),
                    _ => null,
                };
            } catch (EndOfStreamException)
            {
                return null;
            }
        }

        public enum Function: byte
        {
            LOG,
            DISABLE_PLUGINS
        }
    }

    public static class PacketExtensions
    {
        public static BinaryWriter Write(this BinaryWriter writer, Packet packet)
        {
            packet.Write(writer);
            return writer;
        }
    }
}
