using System.IO;

namespace RpaParser.Formats;

/// <summary>
/// The file an archive's index lives in, and what is needed to decode it: where in that
/// file it starts and the key its offsets are XORed with.
/// </summary>
public sealed record IndexFileInfo
{
    private IndexFileInfo(string filePath)
    {
        FilePath = filePath;
    }

    public string FilePath { get; }

    /// <summary>Where the index starts in that file. Zero when it is the whole file.</summary>
    public long Offset { get; private init; }

    /// <summary>Zero for formats that do not obfuscate.</summary>
    public long ObfuscationKey { get; private init; }

    public bool IsSeparateFile { get; private init; }

    /// <summary>
    /// The index's own file, or null when the index sits inside the archive and so has
    /// no file of its own to describe.
    /// </summary>
    public FileInfo? File { get; private init; }

    public static IndexFileInfo InsideArchive(string archivePath, long offset, long obfuscationKey) =>
        new(archivePath)
        {
            Offset = offset,
            ObfuscationKey = obfuscationKey
        };

    /// <summary>An index that is the whole of a separate file, so unkeyed and unoffset.</summary>
    public static IndexFileInfo SeparateFile(string indexPath) =>
        new(indexPath)
        {
            IsSeparateFile = true,
            File = new FileInfo(indexPath)
        };
}