using System;
using System.Diagnostics;
using System.IO;
using RpaParser.Content;

namespace RpaParser.Decompilation
{
    /// <summary>
    /// Turns compiled RenPy bytecode back into source by shelling out to unrpyc.
    ///
    /// This is deliberately separate from <see cref="Archive"/>: where Python lives is a
    /// property of the machine, not of the archive being read.
    /// </summary>
    public sealed class Decompiler(DecompilerOptions options)
    {
        public const string InfoBanner =
            "RPYC file contains compiled RenPy code. To preview code we need to use an external script called unrpyc for decompilation, plus a Python interpreter to run it. " +
            "Use Python 3 with current unrpyc for Ren'Py 8 games, or Python 2.7 with legacy unrpyc for Ren'Py 7 and older.";

        public DecompilerOptions Options { get; } = options ?? new DecompilerOptions();

        public bool IsAvailable =>
            !string.IsNullOrEmpty(Options.PythonPath) && File.Exists(Options.PythonPath)
            && !string.IsNullOrEmpty(Options.UnrpycPath) && File.Exists(Options.UnrpycPath);

        public string Decompile(byte[] compiled)
        {
            RequireTools();

            // unrpyc writes its output beside the input, with the extension swapped.
            var tempFile = Path.GetTempFileName();
            var decompiledFile = tempFile + ".rpy";
            tempFile += ".rpyc";
            var output = string.Empty;

            try
            {
                File.WriteAllBytes(tempFile, compiled);

                var start = new ProcessStartInfo
                {
                    FileName = Options.PythonPath,
                    Arguments = $"\"{Options.UnrpycPath}\" --try-harder \"{tempFile}\"",
                    UseShellExecute = false,
                    RedirectStandardOutput = true,
                    RedirectStandardError = true,
                    CreateNoWindow = true
                };

                using (var process = Process.Start(start)
                                     ?? throw new Exception($"Could not start {Options.PythonPath}."))
                {
                    using (var reader = process.StandardOutput)
                    {
                        output += reader.ReadToEnd();
                    }
                    using (var reader = process.StandardError)
                    {
                        output += reader.ReadToEnd();
                    }
                }

                return LineEndings.Normalize(File.ReadAllText(decompiledFile));
            }
            catch (Exception ex)
            {
                throw new Exception("ERROR: Decompilation failed with following error:" + Environment.NewLine +
                                    Environment.NewLine + ex.Message + Environment.NewLine + Environment.NewLine +
                                    "Return from unrpyc:" + Environment.NewLine + Environment.NewLine + output);
            }
            finally
            {
                Delete(tempFile);
                Delete(decompiledFile);
            }
        }

        private void RequireTools()
        {
            if (string.IsNullOrEmpty(Options.PythonPath))
            {
                throw Missing("Python environment is not defined.");
            }

            if (!File.Exists(Options.PythonPath))
            {
                throw Missing($"Defined Python environment cannot be found ({Options.PythonPath}).");
            }

            if (string.IsNullOrEmpty(Options.UnrpycPath))
            {
                throw Missing("Location of unrpyc script is not defined.");
            }

            if (!File.Exists(Options.UnrpycPath))
            {
                throw Missing($"Defined location of unrpyc script cannot be found ({Options.UnrpycPath}).");
            }
        }

        // The banner leads every one of these so the UI can recognise a setup problem and
        // offer to fetch the missing tool.
        private static Exception Missing(string detail) =>
            new Exception(InfoBanner + Environment.NewLine + Environment.NewLine + "ERROR: " + detail);

        private static void Delete(string path)
        {
            try
            {
                if (File.Exists(path))
                {
                    File.Delete(path);
                }
            }
            catch
            {
                // Temp file cleanup is best effort.
            }
        }
    }
}
