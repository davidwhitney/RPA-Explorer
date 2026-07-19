using System;

namespace RpaParser.Formats;

public sealed class UnknownFormat : ArchiveFormat
{
    public override double Version => 0;
    public override string DisplayName => "Unknown";
    protected override string Magic => string.Empty;

    public override bool IsKnown => false;
    public override bool SupportsPadding => false;

    public override bool Matches(string firstLine, bool indexPairExists) => false;

    public override int HeaderLength => throw Unsupported();

    public override string BuildHeader(long indexOffset, long obfuscationKey) => throw Unsupported();

    public override IndexFileInfo LocateIndex(ArchiveFileInfo files) => throw Unsupported();

    private static Exception Unsupported() => new("Specified version is not supported.");
}