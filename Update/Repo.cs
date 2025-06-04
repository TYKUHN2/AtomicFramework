using System;
using System.IO;
using System.Text.RegularExpressions;
using System.Threading.Tasks;

namespace AtomicFramework.Update
{
    internal class Repo
    {
        private static readonly Regex REPO_PATTERN = new(@"^[\w\.-]+/[\w\.-]+$", RegexOptions.Compiled);

        private readonly Mod mod;

        private readonly Task<Release?> latest;

        internal Repo(Mod mod)
        {
            if (!REPO_PATTERN.IsMatch(mod.options.repository))
                throw new ArgumentException("Repository Uri is not valid");

            this.mod = mod;

            latest = GetLatest(mod);
        }

        internal async Task<bool> Download()
        {
            Release? target = await latest;

            if (target == null)
                return false;

            Stream? download = await Github.Download(mod.options.repository, target);

            if (download == null) 
                return false;

            string path = mod.Info.Location;

            Task result;
            using (FileStream output = File.Open(path + ".download", FileMode.Create, FileAccess.Write, FileShare.None))
            {
                result = download.CopyToAsync(output);
                await result;
            }

            if (result.IsCompletedSuccessfully)
                File.Replace(path + ".download", path, path + ".bak");

            File.Delete(path + ".download");

            return result.IsCompletedSuccessfully;
        }

        private async Task<Release?> GetLatest(Mod mod)
        {
            Release? latest = await Github.GetLatest(mod.options.repository, false);

            if (latest == null || mod.Info.Metadata.Version > latest.version)
                latest = await Github.GetLatest(mod.options.repository, true);

            if (latest == null || mod.Info.Metadata.Version > latest.version)
                return null;
            else
                return latest;
        }
    }
}
