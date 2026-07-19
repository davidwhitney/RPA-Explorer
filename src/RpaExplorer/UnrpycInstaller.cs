using System;
using System.IO;
using System.IO.Compression;
using System.Linq;
using System.Net.Http;
using System.Threading.Tasks;

namespace RpaExplorer
{
    // Downloads unrpyc so the user does not have to install and locate it manually.
    //
    // unrpyc is the external Ren'Py script decompiler the preview shells out to. The
    // download is pinned to a specific release tag rather than a moving branch, and it is
    // only ever started from an explicit user action.
    public static class UnrpycInstaller
    {
        public const string Version = "2.0.4";

        public const string DownloadUrl =
            "https://github.com/CensoredUsername/unrpyc/archive/refs/tags/v" + Version + ".zip";

        // unrpyc 2.x is a Python 3 tool.
        public const string MinimumPython = "3.9";

        private static readonly HttpClient Http = CreateClient();

        private static HttpClient CreateClient()
        {
            var client = new HttpClient { Timeout = TimeSpan.FromMinutes(5) };
            client.DefaultRequestHeaders.UserAgent.ParseAdd("RPA-Explorer");
            return client;
        }

        public static string ToolsDirectory => Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
            "RPA Explorer", "tools");

        private static string ExpectedScriptPath =>
            Path.Combine(ToolsDirectory, "unrpyc-" + Version, "unrpyc.py");

        // Path to an already-downloaded unrpyc.py, or null.
        public static string FindExisting()
        {
            try
            {
                if (File.Exists(ExpectedScriptPath))
                {
                    return ExpectedScriptPath;
                }

                if (Directory.Exists(ToolsDirectory))
                {
                    return Directory
                        .EnumerateFiles(ToolsDirectory, "unrpyc.py", SearchOption.AllDirectories)
                        .FirstOrDefault();
                }
            }
            catch
            {
                // Treat an unreadable tools directory as "not installed".
            }

            return null;
        }

        // Re-uses a previous download when one is already present.
        public static async Task<string> EnsureAsync(IProgress<string> progress = null)
        {
            var existing = FindExisting();
            if (existing != null)
            {
                return existing;
            }

            Directory.CreateDirectory(ToolsDirectory);

            var tempZip = Path.Combine(Path.GetTempPath(),
                "unrpyc-" + Version + "-" + Guid.NewGuid().ToString("N") + ".zip");

            try
            {
                progress?.Report(string.Format("Downloading unrpyc v{0}...", Version));

                using (var response =
                       await Http.GetAsync(DownloadUrl, HttpCompletionOption.ResponseHeadersRead))
                {
                    response.EnsureSuccessStatusCode();

                    using var file = File.Create(tempZip);
                    await response.Content.CopyToAsync(file);
                }

                progress?.Report("Extracting unrpyc...");

                var target = Path.Combine(ToolsDirectory, "unrpyc-" + Version);
                if (Directory.Exists(target))
                {
                    Directory.Delete(target, true);
                }

                // ExtractToDirectory refuses entries that would escape the destination.
                ZipFile.ExtractToDirectory(tempZip, ToolsDirectory);

                var script = FindExisting();
                if (script == null)
                {
                    throw new Exception("unrpyc.py was not found in the downloaded archive.");
                }

                return script;
            }
            finally
            {
                try
                {
                    if (File.Exists(tempZip))
                    {
                        File.Delete(tempZip);
                    }
                }
                catch
                {
                    // Temp file cleanup is best effort.
                }
            }
        }
    }
}
