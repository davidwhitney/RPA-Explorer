using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices;
using System.Text.RegularExpressions;

namespace RpaParser
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
        /// <summary>
        /// The environment the search runs against. Introduced so the search can be exercised
        /// against a controlled filesystem and OS rather than whichever machine runs the tests.
        /// </summary>
        internal interface IEnvironmentProbe
        {
            bool IsWindows { get; }
            string GetEnvironmentVariable(string name);
            string UserProfile { get; }
            bool FileExists(string path);
            bool DirectoryExists(string path);
            string[] GetDirectories(string path);
        }

        private sealed class SystemProbe : IEnvironmentProbe
        {
            public static readonly SystemProbe Instance = new();

            public bool IsWindows => RuntimeInformation.IsOSPlatform(OSPlatform.Windows);

            public string GetEnvironmentVariable(string name) => Environment.GetEnvironmentVariable(name);

            public string UserProfile =>
                Environment.GetFolderPath(Environment.SpecialFolder.UserProfile);

            public bool FileExists(string path)
            {
                try
                {
                    return File.Exists(path);
                }
                catch
                {
                    return false;
                }
            }

            public bool DirectoryExists(string path)
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

            public string[] GetDirectories(string path)
            {
                try
                {
                    return Directory.GetDirectories(path);
                }
                catch
                {
                    return [];
                }
            }
        }

        private static readonly Lazy<string> CachedPath = new(() => Detect(SystemProbe.Instance));

        // Detected interpreter, or an empty string when none was found. Computed once.
        public static string Detected => CachedPath.Value;

        private static string[] ExecutableNames(IEnvironmentProbe probe) => probe.IsWindows
            ? ["python3.exe", "python.exe", "python2.7.exe", "python2.exe"]
            : ["python3", "python", "python2.7", "python2"];

        internal static string Detect(IEnvironmentProbe probe)
        {
            var directories = EnumerateDirectories(probe);

            // Names are the outer loop so an explicit python3 anywhere wins over a bare
            // "python" (which is still Python 2 on many setups).
            foreach (var name in ExecutableNames(probe))
            {
                foreach (var directory in directories)
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

                    if (probe.FileExists(full))
                    {
                        return full;
                    }
                }
            }

            return string.Empty;
        }

        internal static List<string> EnumerateDirectories(IEnvironmentProbe probe)
        {
            HashSet<string> seen = [];
            List<string> dirs = [];

            void Add(string dir)
            {
                if (!string.IsNullOrWhiteSpace(dir) && seen.Add(dir))
                {
                    dirs.Add(dir);
                }
            }

            // 1. The process PATH: correct when the app was started from a shell.
            var pathEnv = probe.GetEnvironmentVariable("PATH") ?? string.Empty;
            foreach (var dir in pathEnv.Split(Path.PathSeparator))
            {
                Add(dir.Trim());
            }

            // 2. pyenv, which a Finder/Dock launch would not expose through PATH.
            foreach (var dir in PyenvDirectories(probe))
            {
                Add(dir);
            }

            // 3. Common install locations a GUI app's PATH usually misses.
            if (probe.IsWindows)
            {
                Add(Path.Combine(probe.UserProfile, "AppData", "Local", "Programs", "Python"));
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
        internal static IEnumerable<string> PyenvDirectories(IEnvironmentProbe probe)
        {
            var root = probe.GetEnvironmentVariable("PYENV_ROOT");
            if (string.IsNullOrWhiteSpace(root))
            {
                root = Path.Combine(probe.UserProfile, ".pyenv");
            }

            if (!probe.DirectoryExists(root))
            {
                yield break;
            }

            var shims = Path.Combine(root, "shims");
            if (probe.DirectoryExists(shims))
            {
                yield return shims;
            }

            var versions = Path.Combine(root, "versions");
            if (!probe.DirectoryExists(versions))
            {
                yield break;
            }

            List<(Version Version, string Dir)> ordered = [];
            List<string> unversioned = [];

            foreach (var versionDir in probe.GetDirectories(versions))
            {
                var bin = probe.IsWindows ? versionDir : Path.Combine(versionDir, "bin");
                if (!probe.DirectoryExists(bin))
                {
                    continue;
                }

                // Names look like "3.12.1", "2.7.18", "pypy3.10-7.3.15", "miniconda3-4.7.12".
                var match = Regex.Match(Path.GetFileName(versionDir) ?? string.Empty, @"(\d+(?:\.\d+)+)");
                if (match.Success && Version.TryParse(match.Groups[1].Value, out var parsed))
                {
                    ordered.Add((parsed, bin));
                }
                else
                {
                    unversioned.Add(bin);
                }
            }

            foreach (var (_, dir) in ordered.OrderByDescending(entry => entry.Version))
            {
                yield return dir;
            }

            foreach (var dir in unversioned)
            {
                yield return dir;
            }
        }
    }
}
