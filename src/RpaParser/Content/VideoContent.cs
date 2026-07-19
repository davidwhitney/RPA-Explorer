using System.Collections.Generic;
using RpaParser.Decompilation;
using RpaParser.Previews;

namespace RpaParser.Content;

public sealed class VideoContent : ContentFormat
{
    public override string DisplayName => "Video";

    public override IReadOnlyList<string> Extensions { get; } =
        [".3gp", ".flv", ".mov", ".mp4", ".ogv", ".swf", ".mpg", ".mpeg", ".avi", ".mkv", ".wmv", ".webm"];

    public override PreviewResult CreatePreview(byte[] data, Decompiler decompiler) => new(this, data);
}