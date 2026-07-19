using System.Collections.Generic;
using RpaParser.Decompilation;
using RpaParser.Previews;

namespace RpaParser.Content;

public sealed class AudioContent : ContentFormat
{
    public override string DisplayName => "Audio";

    public override IReadOnlyList<string> Extensions { get; } =
        [".aac", ".ac3", ".flac", ".mp3", ".wma", ".wav", ".ogg", ".cpc"];

    public override PreviewResult CreatePreview(byte[] data, Decompiler decompiler) => new(this, data);
}