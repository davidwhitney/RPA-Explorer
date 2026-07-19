using System;
using System.Collections.Generic;
using System.IO;
using System.Runtime.InteropServices;
using LibVLCSharp.Shared;

namespace RPA_Explorer
{
    // Locates and initialises LibVLC.
    //
    // The VideoLAN.LibVLC.Mac NuGet package only ships a single x86_64 libvlc.dylib with
    // no plugin set, so it is unusable on Apple Silicon (and cannot decode anything even
    // on Intel). Instead we point LibVLC at a system-wide VLC installation, which ships
    // the matching libvlc/libvlccore plus the full plugin set (WebM/VP8/VP9 included).
    public static class VlcSetup
    {
        [DllImport("libc", SetLastError = true)]
        private static extern int setenv(string name, string value, int overwrite);

        public static bool Available { get; private set; }

        public static string UnavailableReason { get; private set; } = string.Empty;

        public const string DownloadUrl = "https://www.videolan.org/vlc/";

        public const string InstallHint =
            "Audio/video preview requires VLC to be installed.\n\n" +
            "Install VLC from " + DownloadUrl + " (on macOS, drag VLC.app into /Applications).";

        public static bool Initialize()
        {
            // Safe to call again: the user may have installed VLC since start-up.
            if (Available)
            {
                return true;
            }

            try
            {
                if (RuntimeInformation.IsOSPlatform(OSPlatform.OSX))
                {
                    var baseDir = FindMacVlc();
                    if (baseDir == null)
                    {
                        UnavailableReason = InstallHint;
                        return false;
                    }

                    // .NET's Environment.SetEnvironmentVariable does not reach the native
                    // getenv() on Unix, so the plugin path has to be set through libc.
                    SetNativeEnv("VLC_PLUGIN_PATH", Path.Combine(baseDir, "plugins"));
                    Core.Initialize(Path.Combine(baseDir, "lib"));
                }
                else if (RuntimeInformation.IsOSPlatform(OSPlatform.Linux))
                {
                    var pluginDir = FindLinuxPlugins();
                    if (pluginDir != null)
                    {
                        SetNativeEnv("VLC_PLUGIN_PATH", pluginDir);
                    }
                    Core.Initialize();
                }
                else
                {
                    // Windows: prefer the natives bundled by VideoLAN.LibVLC.Windows, and
                    // fall back to a system-wide VLC installation when they are absent.
                    var bundled = BundledWindowsVlc();
                    if (bundled != null)
                    {
                        Core.Initialize();
                    }
                    else
                    {
                        var installed = FindWindowsVlc();
                        if (installed == null)
                        {
                            UnavailableReason = WindowsUnavailableReason();
                            return false;
                        }
                        Core.Initialize(installed);
                    }
                }

                Available = true;
                return true;
            }
            catch (Exception ex)
            {
                UnavailableReason = "LibVLC could not be initialised: " + ex.Message +
                                    Environment.NewLine + Environment.NewLine + InstallHint;
                return false;
            }
        }

        private static void SetNativeEnv(string name, string value)
        {
            try
            {
                setenv(name, value, 1);
            }
            catch
            {
                // Not fatal; LibVLC may still find its plugins on its own.
            }
        }

        // Returns the VLC.app "Contents/MacOS" directory (which holds lib/ and plugins/).
        private static string FindMacVlc()
        {
            List<string> candidates =
            [
                "/Applications/VLC.app/Contents/MacOS",
                Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.UserProfile),
                    "Applications/VLC.app/Contents/MacOS"),

                "/Applications/VLC/VLC.app/Contents/MacOS"
            ];

            foreach (var candidate in candidates)
            {
                if (File.Exists(Path.Combine(candidate, "lib", "libvlc.dylib"))
                    && Directory.Exists(Path.Combine(candidate, "plugins")))
                {
                    return candidate;
                }
            }

            return null;
        }

        // Directory holding the natives shipped alongside the app, if any. LibVLCSharp's
        // parameterless Core.Initialize() resolves "libvlc/win-<arch>" relative to the app.
        private static string BundledWindowsVlc()
        {
            var arch = RuntimeInformation.ProcessArchitecture switch
            {
                Architecture.X64 => "win-x64",
                Architecture.X86 => "win-x86",
                Architecture.Arm64 => "win-arm64",
                _ => null
            };

            if (arch == null)
            {
                return null;
            }

            var dir = Path.Combine(AppContext.BaseDirectory, "libvlc", arch);
            return File.Exists(Path.Combine(dir, "libvlc.dll")) ? dir : null;
        }

        // A system-wide VLC install, but only when its architecture matches this process:
        // an arm64 process cannot load the x64 DLLs a normal Windows VLC installs.
        private static string FindWindowsVlc()
        {
            var process = RuntimeInformation.ProcessArchitecture;
            if (process != Architecture.X64 && process != Architecture.X86)
            {
                return null;
            }

            List<string> candidates = [];
            foreach (var variable in new[] { "ProgramFiles", "ProgramFiles(x86)" })
            {
                var root = Environment.GetEnvironmentVariable(variable);
                if (!string.IsNullOrWhiteSpace(root))
                {
                    candidates.Add(Path.Combine(root, "VideoLAN", "VLC"));
                }
            }

            foreach (var candidate in candidates)
            {
                if (File.Exists(Path.Combine(candidate, "libvlc.dll"))
                    && Directory.Exists(Path.Combine(candidate, "plugins")))
                {
                    return candidate;
                }
            }

            return null;
        }

        private static string WindowsUnavailableReason()
        {
            if (RuntimeInformation.ProcessArchitecture == Architecture.Arm64)
            {
                // VideoLAN publishes no Windows arm64 build of VLC, so there is nothing to
                // bundle or to fall back to; an arm64 process cannot load the x64 libraries.
                return "Audio/video preview is not available in the Windows arm64 build, " +
                       "because VLC has no Windows arm64 release.\n\n" +
                       "Use the Windows x64 download instead - Windows on ARM runs it under " +
                       "emulation and media preview works there.";
            }

            return InstallHint;
        }

        private static string FindLinuxPlugins()
        {
            string[] candidates =
            [
                "/usr/lib/x86_64-linux-gnu/vlc/plugins",
                "/usr/lib64/vlc/plugins",
                "/usr/lib/vlc/plugins",
                "/usr/local/lib/vlc/plugins"
            ];

            foreach (var candidate in candidates)
            {
                if (Directory.Exists(candidate))
                {
                    return candidate;
                }
            }

            return null;
        }
    }
}
