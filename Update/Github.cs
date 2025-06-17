using AtomicFramework.Update;
using System;
using System.IO;
using System.Linq;
using System.Net.Http;
using System.Threading.Tasks;
using UnityEngine;

namespace AtomicFramework
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

        internal static async Task<Release?> GetLatest(string target, bool prerelease)
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
            } else
            {
                HttpResponseMessage resp = await client.GetAsync(target + "/releases/latest");
                if (!resp.IsSuccessStatusCode)
                    return null;

                string body = await resp.Content.ReadAsStringAsync();

                release = JsonUtility.FromJson<JSONRelease>(body);

                if (release == null) 
                    return null;
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
            JSONAsset[] plugins = release.assets.Where(asset =>
                asset.name.StartsWith(repo, StringComparison.OrdinalIgnoreCase) &&
                asset.name.EndsWith(".dll", StringComparison.OrdinalIgnoreCase)
            ).ToArray();

            if (plugins.Length == 0)
                return null;

            if (plugins.Length == 1)
                return new Release(release.id, version, plugins[0].name, plugins[0].id);

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

            return new Release(release.id, version, selected.name, selected.id);
        }

        internal static async Task<Stream?> Download(string target, Release release)
        {
            using HttpRequestMessage request = new(HttpMethod.Get, target + "/releases/assets/" + release.asset);
            request.Headers.Accept.Add(new("application/octet-stream"));

            HttpResponseMessage resp = await client.SendAsync(request);

            if (resp.IsSuccessStatusCode)
                return await resp.Content.ReadAsStreamAsync();
            else
                return null;
        }
    }
}
