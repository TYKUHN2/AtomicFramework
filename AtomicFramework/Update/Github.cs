using System;
using System.IO;
using System.Linq;
using System.Net.Http;
using System.Threading.Tasks;
using UnityEngine;

namespace AtomicFramework.Update
{
    internal static class Github
    {
        private static readonly HttpClient client = new()
        {
            BaseAddress = new Uri("https://github.com/repos/")
        };

        static Github()
        {
            client.DefaultRequestHeaders.Accept.Add(new("application/vnd.github+json"));
        }

        internal static async Task<Release?> GetLatest(string GUID, string target, bool prerelease)
        {
            JSONRelease release;

            if (prerelease)
            {
                HttpResponseMessage resp = await client.GetAsync(target + "/releases?per_page=1");
                if (!resp.IsSuccessStatusCode)
                    return null;

                string body = await resp.Content.ReadAsStringAsync();

                release = JsonUtility.FromJson<JSONRelease[]>(body)[0];

                if (release == null)
                    return null;
            }
            else
            {
                HttpResponseMessage resp = await client.GetAsync(target + "/releases/latest");
                if (!resp.IsSuccessStatusCode)
                    return null;

                string body = await resp.Content.ReadAsStringAsync();

                release = JsonUtility.FromJson<JSONRelease>(body);

                if (release == null) 
                    return null;
            }

            JSONMod? mod = await FindFromManifest(target, GUID, release);
            if (mod != null)
            {
                JSONAsset? assetMatch = release.assets.FirstOrDefault(a => a.name == mod.asset);
                if (assetMatch == null)
                {
                    Plugin.Logger.LogWarning(GUID + " has malformed release");
                    return null;
                }

                return new Release(release.id, new(mod.version), assetMatch.id);
            }

#if BEP6
            SemanticVersioning.Version version;
#else
            Version version;
#endif

            try
            {
                version = new(release.tag_name);
            }
            catch (ArgumentException)
            {
                return null;
            }

            string repo = target.Split("/")[1].ToLower();
            JSONAsset[] plugins = [.. release.assets.Where(asset =>
                asset.name.StartsWith(GUID, StringComparison.OrdinalIgnoreCase) &&
                asset.name.EndsWith(".dll", StringComparison.OrdinalIgnoreCase)
            )];

            if (plugins.Length == 0)
                return null;

            if (plugins.Length == 1)
                return new Release(release.id, version, plugins[0].id);

#if BEP6
            JSONAsset? selected = plugins.FirstOrDefault(asset =>
                asset.name.Contains("Bep5", StringComparison.OrdinalIgnoreCase) ||
                asset.name.Contains("BepInEx5", StringComparison.OrdinalIgnoreCase)
            );
#elif BEP5
        JSONAsset? selected = plugins.FirstOrDefault(asset =>
            asset.name.Contains("Bep5", StringComparison.OrdinalIgnoreCase) ||
            asset.name.Contains("BepInEx5", StringComparison.OrdinalIgnoreCase)
        );
#else
#error Unknown BepInEx Version
#endif

            if (selected == null)
                return null;

            return new Release(release.id, version, selected.id);
        }

        internal static async Task<Stream?> Download(string target, Release release)
        {
            return await DownloadAsset(target, release.asset);
        }

        private static async Task<JSONMod?> FindFromManifest(string target, string GUID, JSONRelease release)
        {
            JSONAsset? asset = release.assets.FirstOrDefault(a => a.name == "manifest.json");
            if (asset != null)
            {
                Stream? src = await DownloadAsset(target, asset.id);

                if (src != null)
                {
                    StreamReader reader = new(src);
                    string content = await reader.ReadToEndAsync();

                    JSONManifest? manifest = JsonUtility.FromJson<JSONManifest>(content);

                    if (manifest != null)
                        if (manifest.mods.Length > 0)
                        {
                            manifest.mods.FirstOrDefault(a => a.id == GUID);
                        }
                        else
                            Plugin.Logger.LogWarning("Failed to read manifest for " + target);
                }
                else
                    Plugin.Logger.LogWarning("Failed to download manifest for " + target);
            }

            return null;
        }

        private static async Task<Stream?> DownloadAsset(string target, int asset)
        {
            using HttpRequestMessage request = new(HttpMethod.Get, target + "/releases/assets/" + asset);
            request.Headers.Accept.Add(new("application/octet-stream"));

            HttpResponseMessage resp = await client.SendAsync(request);

            if (resp.IsSuccessStatusCode)
                return await resp.Content.ReadAsStreamAsync();
            else
                return null;
        }
    }
}
