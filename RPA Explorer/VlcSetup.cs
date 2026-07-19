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

        public const string InstallHint =
            "Audio/video preview requires VLC to be installed.\n\n" +
            "Install VLC from https://www.videolan.org/vlc/ (on macOS, drag VLC.app into /Applications) and restart RPA Explorer.";

        public static bool Initialize()
        {
            try
            {
                if (RuntimeInformation.IsOSPlatform(OSPlatform.OSX))
                {
                    string baseDir = FindMacVlc();
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
                    string pluginDir = FindLinuxPlugins();
                    if (pluginDir != null)
                    {
                        SetNativeEnv("VLC_PLUGIN_PATH", pluginDir);
                    }
                    Core.Initialize();
                }
                else
                {
                    // Windows: native libraries come from the VideoLAN.LibVLC.Windows package.
                    Core.Initialize();
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
            List<string> candidates = new()
            {
                "/Applications/VLC.app/Contents/MacOS",
                Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.UserProfile),
                    "Applications/VLC.app/Contents/MacOS"),
                "/Applications/VLC/VLC.app/Contents/MacOS"
            };

            foreach (string candidate in candidates)
            {
                if (File.Exists(Path.Combine(candidate, "lib", "libvlc.dylib"))
                    && Directory.Exists(Path.Combine(candidate, "plugins")))
                {
                    return candidate;
                }
            }

            return null;
        }

        private static string FindLinuxPlugins()
        {
            string[] candidates =
            {
                "/usr/lib/x86_64-linux-gnu/vlc/plugins",
                "/usr/lib64/vlc/plugins",
                "/usr/lib/vlc/plugins",
                "/usr/local/lib/vlc/plugins"
            };

            foreach (string candidate in candidates)
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
