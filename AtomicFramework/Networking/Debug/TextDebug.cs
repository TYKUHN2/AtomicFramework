using BepInEx.Configuration;
using System;
using System.IO;
using System.Linq;
using System.Text;
using UnityEngine;

namespace AtomicFramework.Networking.Debug
{
    internal static class TextDebug
    {
        internal static readonly ConfigEntry<string> configCapture;

        internal static readonly string[] filters;

        private static readonly FileStream? filestream;
        private static readonly StreamWriter? output;

        static TextDebug()
        {
            configCapture = Plugin.Instance.Config.Bind("TextDebug", "Capture", "None", "Which plugins to capture networking from, none, or all.");

            string captureString = configCapture.Value.Trim().Normalize().ToLower();

            if (captureString == "" || captureString == "none")
            {
                filters = [];
                return;
            }
            else
            {
                string[] list = [.. captureString.Split(';').Select(a => a.Trim())];
                if (list.Contains("all"))
                    filters = [];
                else
                    filters = list;
            }

            string dir = Path.Combine(Application.persistentDataPath, "AtomicFramework/TextDebug/");
            if (!Directory.Exists(dir))
                Directory.CreateDirectory(dir);

            string basename = Path.Combine(dir, DateTime.Now.ToString("s").Replace(":", "-"));

            int postfix = 0;
            string filename = basename + ".txt";
            while (File.Exists(filename))
                filename = basename + $".{postfix}.txt";

            filestream = new(filename, FileMode.CreateNew, FileAccess.ReadWrite, FileShare.Read);
            output = new(filestream);

            Application.quitting += Close;
        }

        internal static void Close()
        {
            output?.Close();
            filestream?.Close();
        }

        internal static void WriteConnecting(string guid, ushort channel, ulong player, bool fail)
        {
            if (filestream == null) return;

            if (filters.Length > 0 && !filters.Contains(guid)) return;

            output!.WriteLine($"[{DateTime.Now:s}]: CONNECTING{(fail ? " FAILED" : "")} {player} ({guid}:{channel})");

            output.Flush();

            Plugin.Logger.LogDebug($"CONNECTING{(fail ? " FAILED" : "")} {player} ({guid}:{channel})");
        }

        internal static void WriteConnectStatus(string guid, ushort channel, ulong player, bool disconnect)
        {
            if (filestream == null) return;

            if (filters.Length > 0 && !filters.Contains(guid)) return;

            output!.WriteLine($"[{DateTime.Now:s}]: {(disconnect ? "DIS" : "")}CONNECT {player} ({guid}:{channel})");

            output.Flush();

            Plugin.Logger.LogDebug($"{(disconnect ? "DIS" : "")}CONNECT {player} ({guid}:{channel})");
        }

        internal static void WriteChannelStatus(string guid, ushort channel, bool close)
        {
            if (filestream == null) return;

            if (filters.Length > 0 && !filters.Contains(guid)) return;

            output!.WriteLine($"[{DateTime.Now:s}]: {(close ? "CLOSE" : "OPEN")} {guid}:{channel}");

            output.Flush();

            Plugin.Logger.LogDebug($"{(close ? "CLOSE" : "OPEN")} {guid}:{channel}");
        }

        internal static void WritePacket(string guid, ushort channel, NetworkMessage message, bool outbound)
        {
            if (filestream == null) return;

            if (filters.Length > 0 && !filters.Contains(guid)) return;

            output!.WriteLine($"[{DateTime.Now:s}]: {(outbound ? "<" : ">")} {message.player} ({guid}:{channel}) {Encoding.Default.GetString(message.data)} ({BitConverter.ToString(message.data)})");

            output.Flush();

            Plugin.Logger.LogDebug($"{(outbound ? "<" : ">")} {message.player} ({guid}:{channel})");
        }
    }
}
