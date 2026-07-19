using System;

namespace RpaParser.Formats;

/// <summary>
/// Version 1: a .rpa/.rpi pair with no header at all. It is recognised by the presence
/// of both halves rather than by any magic bytes, and so overrides most of the shape
/// the later versions share.
/// </summary>
public sealed class Rpa1Format : ArchiveFormat
{
    public override double Version => 1;
    public override string DisplayName => "RPA 1.0";
    protected override string Magic => string.Empty;

    public override bool HasSeparateIndexFile => true;

    // The files are written back to back with no header to offset them.
    public override bool SupportsPadding => false;
    public override int HeaderLength => 0;

    public override bool Matches(string firstLine, bool indexPairExists) => indexPairExists;

    public override string BuildHeader(long indexOffset, long obfuscationKey) =>
        throw new InvalidOperationException(
            "Version 1 archives have no header; the index is written to a separate .rpi file.");

    /// <summary>
    /// The index is the whole of the sibling .rpi file, so there is no offset to seek to
    /// and no key to undo.
    /// </summary>
    public override IndexFileInfo LocateIndex(ArchiveFileInfo files)
    {
        if (string.IsNullOrEmpty(files.IndexPath))
        {
            throw new Exception("No index file provided.");
        }

        if (!files.IndexPairExists)
        {
            throw new Exception("Index file does not exist.");
        }

        return IndexFileInfo.SeparateFile(files.IndexPath);
    }
}