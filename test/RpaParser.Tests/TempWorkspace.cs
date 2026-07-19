using System;
using System.Collections.Generic;
using System.IO;
using RpaParser;

namespace RpaParser.Tests;

/// <summary>
/// An isolated temporary directory for a test, plus helpers for producing real RenPy
/// archives. Tests build their fixtures through the parser itself rather than checking in
/// binary files, so the archives always match the format the parser writes.
/// </summary>
public sealed class TempWorkspace : IDisposable
{
    public string Root { get; }

    public TempWorkspace()
    {
        Root = Path.Combine(Path.GetTempPath(), "rpa-tests-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(Root);
    }

    public string Path_(params string[] parts) => System.IO.Path.Combine(Root, System.IO.Path.Combine(parts));

    public string WriteFile(string relativePath, byte[] content)
    {
        var full = Path_(relativePath.Replace('/', System.IO.Path.DirectorySeparatorChar));
        Directory.CreateDirectory(System.IO.Path.GetDirectoryName(full)!);
        File.WriteAllBytes(full, content);
        return full;
    }

    public string WriteFile(string relativePath, string content) =>
        WriteFile(relativePath, System.Text.Encoding.UTF8.GetBytes(content));

    /// <summary>
    /// Builds an archive of the given version containing <paramref name="entries"/>, keyed by
    /// the path the entry should have inside the archive. Returns the archive path.
    /// </summary>
    public string CreateArchive(
        ArchiveFormat format,
        IDictionary<string, byte[]> entries,
        string archiveName = "test.rpa",
        int padding = 0,
        long? obfuscationKey = null)
    {
        var parser = new Parser { Format = format, Padding = padding };
        if (obfuscationKey.HasValue)
        {
            parser.ObfuscationKey = obfuscationKey.Value;
        }

        var sourceDir = Path_("src-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(sourceDir);

        foreach (var entry in entries)
        {
            var onDisk = System.IO.Path.Combine(
                sourceDir, entry.Key.Replace('/', System.IO.Path.DirectorySeparatorChar));
            Directory.CreateDirectory(System.IO.Path.GetDirectoryName(onDisk)!);
            File.WriteAllBytes(onDisk, entry.Value);

            parser.Index.Add(entry.Key, new Parser.ArchiveIndex
            {
                InArchive = false,
                FullPath = onDisk.Replace('\\', '/'),
                TreePath = entry.Key,
                ParentPath = System.IO.Path.GetDirectoryName(entry.Key),
                Length = entry.Value.Length
            });
        }

        return parser.SaveArchive(Path_(archiveName));
    }

    /// <summary>Loads an archive created by <see cref="CreateArchive"/>.</summary>
    public Parser LoadArchive(ArchiveFormat format, IDictionary<string, byte[]> entries, string archiveName = "test.rpa")
    {
        var path = CreateArchive(format, entries, archiveName);
        var parser = new Parser();
        parser.LoadArchive(path);
        return parser;
    }

    public void Dispose()
    {
        try
        {
            if (Directory.Exists(Root))
            {
                Directory.Delete(Root, true);
            }
        }
        catch
        {
            // A leftover temp directory must never fail a test run.
        }
    }
}
