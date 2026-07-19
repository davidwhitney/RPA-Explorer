using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices;
using System.Text.RegularExpressions;

namespace RPA_Parser
{
    // Best-effort detection of a Python interpreter for running unrpyc.
    //
    // Python 3 is preferred: current unrpyc releases require it and Ren'Py 8 games produce
    // Python 3 .rpyc files. Python 2.7 remains a fallback for legacy unrpyc with Ren'Py 7
    // and older archives. The user can always override the result via Options in the UI.
    //
    // pyenv is included as a search location: a GUI application launched from Finder/Dock
    // does not inherit the shell PATH, so pyenv's shims are otherwise invisible even though
    // "python3" works fine in the user's terminal.
    public static class PythonLocator
    {
        private static readonly Lazy<string> CachedPath = new(Detect);

        // Detected interpreter, or an empty string when none was found. Computed once.
        public static string Detected => CachedPath.Value;

        private static bool IsWindows => RuntimeInformation.IsOSPlatform(OSPlatform.Windows);

        private static string[] ExecutableNames => IsWindows
            ? new[] { "python3.exe", "python.exe", "python2.7.exe", "python2.exe" }
            : new[] { "python3", "python", "python2.7", "python2" };

        private static string Detect()
        {
            List<string> directories = EnumerateDirectories();

            // Names are the outer loop so an explicit python3 anywhere wins over a bare
            // "python" (which is still Python 2 on many setups).
            foreach (string name in ExecutableNames)
            {
                foreach (string directory in directories)
                {
                    string full;
                    try
                    {
                        full = Path.Combine(directory, name);
                    }
                    catch
                    {
                        continue; // malformed PATH entry
                    }

                    try
                    {
                        if (File.Exists(full))
                        {
                            return full;
                        }
                    }
                    catch
                    {
                        // Unreadable location, keep looking
                    }
                }
            }

            return string.Empty;
        }

        private static List<string> EnumerateDirectories()
        {
            HashSet<string> seen = new();
            List<string> dirs = new();

            void Add(string dir)
            {
                if (!string.IsNullOrWhiteSpace(dir) && seen.Add(dir))
                {
                    dirs.Add(dir);
                }
            }

            // 1. The process PATH: correct when the app was started from a shell.
            string pathEnv = Environment.GetEnvironmentVariable("PATH") ?? string.Empty;
            foreach (string dir in pathEnv.Split(Path.PathSeparator))
            {
                Add(dir.Trim());
            }

            // 2. pyenv, which a Finder/Dock launch would not expose through PATH.
            foreach (string dir in PyenvDirectories())
            {
                Add(dir);
            }

            // 3. Common install locations a GUI app's PATH usually misses.
            if (IsWindows)
            {
                Add(Path.Combine(
                    Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
                    "Programs", "Python"));
            }
            else
            {
                Add("/opt/homebrew/bin");
                Add("/usr/local/bin");
                Add("/usr/bin");
                Add("/opt/local/bin");
            }

            return dirs;
        }

        // pyenv shims first (they honour the user's selected global/local version), then the
        // concrete installs, newest version first.
        private static IEnumerable<string> PyenvDirectories()
        {
            string root = Environment.GetEnvironmentVariable("PYENV_ROOT");
            if (string.IsNullOrWhiteSpace(root))
            {
                root = Path.Combine(
                    Environment.GetFolderPath(Environment.SpecialFolder.UserProfile), ".pyenv");
            }

            if (!SafeDirectoryExists(root))
            {
                yield break;
            }

            string shims = Path.Combine(root, "shims");
            if (SafeDirectoryExists(shims))
            {
                yield return shims;
            }

            string versions = Path.Combine(root, "versions");
            if (!SafeDirectoryExists(versions))
            {
                yield break;
            }

            List<(Version Version, string Dir)> ordered = new();
            List<string> unversioned = new();

            foreach (string versionDir in SafeEnumerateDirectories(versions))
            {
                string bin = IsWindows ? versionDir : Path.Combine(versionDir, "bin");
                if (!SafeDirectoryExists(bin))
                {
                    continue;
                }

                // Names look like "3.12.1", "2.7.18", "pypy3.10-7.3.15", "miniconda3-4.7.12".
                Match match = Regex.Match(Path.GetFileName(versionDir) ?? string.Empty, @"(\d+(?:\.\d+)+)");
                if (match.Success && Version.TryParse(match.Groups[1].Value, out Version parsed))
                {
                    ordered.Add((parsed, bin));
                }
                else
                {
                    unversioned.Add(bin);
                }
            }

            foreach ((Version _, string dir) in ordered.OrderByDescending(entry => entry.Version))
            {
                yield return dir;
            }

            foreach (string dir in unversioned)
            {
                yield return dir;
            }
        }

        private static bool SafeDirectoryExists(string path)
        {
            try
            {
                return Directory.Exists(path);
            }
            catch
            {
                return false;
            }
        }

        private static IEnumerable<string> SafeEnumerateDirectories(string path)
        {
            try
            {
                return Directory.GetDirectories(path);
            }
            catch
            {
                return Array.Empty<string>();
            }
        }
    }
}
