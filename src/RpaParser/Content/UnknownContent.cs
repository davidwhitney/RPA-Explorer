using System.Collections.Generic;
using RpaParser.Decompilation;
using RpaParser.Previews;

namespace RpaParser.Content;

/// <summary>Anything unrecognised: offered to the caller as raw bytes.</summary>
public sealed class UnknownContent : ContentFormat
{
    public override string DisplayName => "Unknown";

    public override IReadOnlyList<string> Extensions { get; } = [];

    public override bool Matches(string extension) => false;

    public override PreviewResult CreatePreview(byte[] data, Decompiler decompiler) => new(this, data);
}