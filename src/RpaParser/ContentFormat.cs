using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;

namespace RpaParser
{
    /// <summary>A previewable file, together with whatever the format made of its bytes.</summary>
    public sealed class PreviewResult(ContentFormat format, object content)
    {
        /// <summary>The format the content is presented as. Never null.</summary>
        public ContentFormat Format { get; } = format;

        /// <summary>
        /// Decoded text for text formats, the raw bytes for everything else, and null when
        /// the file is not in the archive.
        /// </summary>
        public object Content { get; } = content;

        public string AsText() => Content as string;

        public byte[] AsBytes() => Content as byte[];
    }

    /// <summary>
    /// One kind of file the archive can hold, recognised once and thereafter trusted.
    ///
    /// Each format knows the extensions it claims and how to turn raw bytes into a preview,
    /// so the parser asks which format applies instead of walking a chain of extension list
    /// lookups, and callers switch on the format rather than comparing magic strings.
    /// Detection always yields a format: unrecognised files get <see cref="UnknownContent"/>.
    /// </summary>
    public abstract class ContentFormat
    {
        /// <summary>Name shown to the user.</summary>
        public abstract string DisplayName { get; }

        /// <summary>Lower case extensions this format claims, including the leading dot.</summary>
        public abstract IReadOnlyList<string> Extensions { get; }

        public virtual bool Matches(string extension) =>
            Extensions.Contains(extension, StringComparer.OrdinalIgnoreCase);

        /// <summary>
        /// Turns the extracted bytes into a preview. The result usually carries this format,
        /// but a format may hand back another - a compiled script that cannot be decompiled
        /// is presented as unknown.
        /// </summary>
        public abstract PreviewResult CreatePreview(byte[] data, Parser parser);

        public static ContentFormat Image { get; } = new ImageContent();
        public static ContentFormat Text { get; } = new TextContent();
        public static ContentFormat CompiledScript { get; } = new CompiledScriptContent();
        public static ContentFormat Audio { get; } = new AudioContent();
        public static ContentFormat Video { get; } = new VideoContent();
        public static ContentFormat Unknown { get; } = new UnknownContent();

        /// <summary>Recognised formats, in the order they are offered a file.</summary>
        public static IReadOnlyList<ContentFormat> All { get; } =
            [Image, Text, CompiledScript, Audio, Video];

        /// <summary>
        /// The format claiming this file name, falling back to <see cref="Unknown"/>.
        /// </summary>
        public static ContentFormat Detect(string fileName)
        {
            var extension = Path.GetExtension(fileName ?? string.Empty);
            return All.FirstOrDefault(format => format.Matches(extension)) ?? Unknown;
        }

        public override string ToString() => DisplayName;
    }

    /*
    RenPy Supports:
    Images: JPEG/JPG, PNG, WEBP, BMP, GIF
    Sound/Music: OPUS, OGG Vorbis, FLAC, WAV, MP3, MP2
    Movies: WEBM, OGG Theora, VP9, VP8, MPEG 41, MPEG 2, MPEG 1
    */

    /// <summary>Images are handed over as raw bytes; the UI decodes them cross-platform.</summary>
    public sealed class ImageContent : ContentFormat
    {
        public override string DisplayName => "Image";

        public override IReadOnlyList<string> Extensions { get; } =
            [".jpeg", ".jpg", ".bmp", ".tiff", ".png", ".webp", ".exif", ".ico", ".gif"];

        public override PreviewResult CreatePreview(byte[] data, Parser parser) => new(this, data);
    }

    public sealed class TextContent : ContentFormat
    {
        public override string DisplayName => "Text";

        public override IReadOnlyList<string> Extensions { get; } =
            [".py", ".rpy~", ".rpy", ".txt", ".log", ".nfo", ".htm", ".html", ".xml", ".json", ".yaml", ".csv"];

        public override PreviewResult CreatePreview(byte[] data, Parser parser) =>
            new(this, Parser.NormalizeNewLines(Encoding.UTF8.GetString(data, 0, data.Length)));
    }

    /// <summary>
    /// Compiled RenPy scripts, which have to go through the external decompiler. When it
    /// produces nothing the file is presented as unknown rather than as empty text.
    /// </summary>
    public sealed class CompiledScriptContent : ContentFormat
    {
        public override string DisplayName => "Compiled script";

        public override IReadOnlyList<string> Extensions { get; } =
            [".rpyc~", ".rpyc", ".rpymc~", ".rpymc"];

        public override PreviewResult CreatePreview(byte[] data, Parser parser)
        {
            var decompiled = parser.ParseRpyc(data);

            return decompiled == string.Empty
                ? new PreviewResult(Unknown, data)
                : new PreviewResult(Text, decompiled);
        }
    }

    public sealed class AudioContent : ContentFormat
    {
        public override string DisplayName => "Audio";

        public override IReadOnlyList<string> Extensions { get; } =
            [".aac", ".ac3", ".flac", ".mp3", ".wma", ".wav", ".ogg", ".cpc"];

        public override PreviewResult CreatePreview(byte[] data, Parser parser) => new(this, data);
    }

    public sealed class VideoContent : ContentFormat
    {
        public override string DisplayName => "Video";

        public override IReadOnlyList<string> Extensions { get; } =
            [".3gp", ".flv", ".mov", ".mp4", ".ogv", ".swf", ".mpg", ".mpeg", ".avi", ".mkv", ".wmv", ".webm"];

        public override PreviewResult CreatePreview(byte[] data, Parser parser) => new(this, data);
    }

    /// <summary>Anything unrecognised: offered to the caller as raw bytes.</summary>
    public sealed class UnknownContent : ContentFormat
    {
        public override string DisplayName => "Unknown";

        public override IReadOnlyList<string> Extensions { get; } = [];

        public override bool Matches(string extension) => false;

        public override PreviewResult CreatePreview(byte[] data, Parser parser) => new(this, data);
    }
}
