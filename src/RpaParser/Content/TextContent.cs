using System.Collections.Generic;
using System.Text;
using RpaParser.Decompilation;
using RpaParser.Previews;

namespace RpaParser.Content;

public sealed class TextContent : ContentFormat
{
    public override string DisplayName => "Text";

    public override IReadOnlyList<string> Extensions { get; } =
        [".py", ".rpy~", ".rpy", ".txt", ".log", ".nfo", ".htm", ".html", ".xml", ".json", ".yaml", ".csv"];

    public override PreviewResult CreatePreview(byte[] data, Decompiler decompiler) =>
        new(this, LineEndings.Normalize(Encoding.UTF8.GetString(data, 0, data.Length)));
}