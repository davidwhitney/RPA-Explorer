using System;
using RpaParser.Content;

namespace RpaParser.Previews;

public sealed class PreviewResult(ContentFormat format, object content)
{
    public static PreviewResult Missing { get; } = new(ContentFormat.Unknown, Array.Empty<byte>());
    public ContentFormat Format { get; } = format;
    public object Content { get; } = content;
    public string AsText() => Content as string ?? string.Empty;
    public byte[] AsBytes() => Content as byte[] ?? [];
}