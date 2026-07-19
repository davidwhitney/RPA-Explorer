using System.Diagnostics;
using System.IO;
using System.Runtime.InteropServices;

namespace RpaExplorer
{
    // Small platform helpers shared by the windows.
    public static class Platform
    {
        public static void OpenUrl(string url)
        {
            try
            {
                if (RuntimeInformation.IsOSPlatform(OSPlatform.OSX))
                {
                    Process.Start("open", url);
                }
                else if (RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
                {
                    Process.Start(new ProcessStartInfo(url) { UseShellExecute = true });
                }
                else
                {
                    Process.Start("xdg-open", url);
                }
            }
            catch
            {
                // Best effort only.
            }
        }

        // True when Homebrew appears to be installed, so we can suggest a one-line install.
        public static bool HasHomebrew =>
            File.Exists("/opt/homebrew/bin/brew") || File.Exists("/usr/local/bin/brew");
    }
}
