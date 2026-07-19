using System.Collections.Generic;
using RpaParser.Decompilation;
using RpaParser.Previews;

namespace RpaParser.Content;

/// <summary>Images are handed over as raw bytes; the UI decodes them cross-platform.</summary>
public sealed class ImageContent : ContentFormat
{
    public override string DisplayName => "Image";

    public override IReadOnlyList<string> Extensions { get; } =
        [".jpeg", ".jpg", ".bmp", ".tiff", ".png", ".webp", ".exif", ".ico", ".gif"];

    public override PreviewResult CreatePreview(byte[] data, Decompiler decompiler) => new(this, data);
}