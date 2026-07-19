using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;

namespace RpaParser
{
    /// <summary>
    /// One kind of file the archive can hold, recognised once and thereafter trusted.
    ///
    /// Each format knows the extensions it claims and how to turn raw bytes into a preview,
    /// so the parser asks which format applies instead of walking a chain of extension list
    /// lookups. Detection always yields a format: unrecognised files get
    /// <see cref="UnknownContent"/> rather than a null.
    /// </summary>
    public abstract class ContentFormat
    {
        /// <summary>The preview kind reported to callers.</summary>
        public abstract string PreviewType { get; }

        /// <summary>Lower case extensions this format claims, including the leading dot.</summary>
        public abstract IReadOnlyList<string> Extensions { get; }

        public virtual bool Matches(string extension) =>
            Extensions.Contains(extension, StringComparer.OrdinalIgnoreCase);

        /// <summary>
        /// Turns the extracted bytes into a preview. The returned kind is usually
        /// <see cref="PreviewType"/>, but a format may downgrade it - a compiled script that
        /// cannot be decompiled is reported as unknown.
        /// </summary>
        public abstract KeyValuePair<string, object> CreatePreview(byte[] data, Parser parser);

        /// <summary>Recognised formats, in the order they are offered a file.</summary>
        public static IReadOnlyList<ContentFormat> All { get; } =
        [
            new ImageContent(),
            new TextContent(),
            new CompiledScriptContent(),
            new AudioContent(),
            new VideoContent()
        ];

        /// <summary>
        /// The format claiming this file name, falling back to <see cref="UnknownContent"/>.
        /// </summary>
        public static ContentFormat Detect(string fileName)
        {
            var extension = Path.GetExtension(fileName ?? string.Empty);
            return All.FirstOrDefault(format => format.Matches(extension)) ?? Unknown;
        }

        public static ContentFormat Unknown { get; } = new UnknownContent();

        public override string ToString() => PreviewType;
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
        public override string PreviewType => Parser.PreviewTypes.Image;

        public override IReadOnlyList<string> Extensions { get; } =
            [".jpeg", ".jpg", ".bmp", ".tiff", ".png", ".webp", ".exif", ".ico", ".gif"];

        public override KeyValuePair<string, object> CreatePreview(byte[] data, Parser parser) =>
            new(PreviewType, data);
    }

    public sealed class TextContent : ContentFormat
    {
        public override string PreviewType => Parser.PreviewTypes.Text;

        public override IReadOnlyList<string> Extensions { get; } =
            [".py", ".rpy~", ".rpy", ".txt", ".log", ".nfo", ".htm", ".html", ".xml", ".json", ".yaml", ".csv"];

        public override KeyValuePair<string, object> CreatePreview(byte[] data, Parser parser) =>
            new(PreviewType, Parser.NormalizeNewLines(Encoding.UTF8.GetString(data, 0, data.Length)));
    }

    /// <summary>
    /// Compiled RenPy scripts, which have to go through the external decompiler. When it
    /// produces nothing the file is reported as unknown rather than as empty text.
    /// </summary>
    public sealed class CompiledScriptContent : ContentFormat
    {
        public override string PreviewType => Parser.PreviewTypes.Text;

        public override IReadOnlyList<string> Extensions { get; } =
            [".rpyc~", ".rpyc", ".rpymc~", ".rpymc"];

        public override KeyValuePair<string, object> CreatePreview(byte[] data, Parser parser)
        {
            var decompiled = parser.ParseRpyc(data);

            return decompiled == string.Empty
                ? new KeyValuePair<string, object>(Parser.PreviewTypes.Unknown, data)
                : new KeyValuePair<string, object>(Parser.PreviewTypes.Text, decompiled);
        }
    }

    public sealed class AudioContent : ContentFormat
    {
        public override string PreviewType => Parser.PreviewTypes.Audio;

        public override IReadOnlyList<string> Extensions { get; } =
            [".aac", ".ac3", ".flac", ".mp3", ".wma", ".wav", ".ogg", ".cpc"];

        public override KeyValuePair<string, object> CreatePreview(byte[] data, Parser parser) =>
            new(PreviewType, data);
    }

    public sealed class VideoContent : ContentFormat
    {
        public override string PreviewType => Parser.PreviewTypes.Video;

        public override IReadOnlyList<string> Extensions { get; } =
            [".3gp", ".flv", ".mov", ".mp4", ".ogv", ".swf", ".mpg", ".mpeg", ".avi", ".mkv", ".wmv", ".webm"];

        public override KeyValuePair<string, object> CreatePreview(byte[] data, Parser parser) =>
            new(PreviewType, data);
    }

    /// <summary>Anything unrecognised: offered to the caller as raw bytes.</summary>
    public sealed class UnknownContent : ContentFormat
    {
        public override string PreviewType => Parser.PreviewTypes.Unknown;

        public override IReadOnlyList<string> Extensions { get; } = [];

        public override bool Matches(string extension) => false;

        public override KeyValuePair<string, object> CreatePreview(byte[] data, Parser parser) =>
            new(PreviewType, data);
    }
}
