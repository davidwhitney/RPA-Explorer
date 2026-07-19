using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using RpaParser.Decompilation;
using RpaParser.Previews;

namespace RpaParser.Content
{
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
        public abstract PreviewResult CreatePreview(byte[] data, Decompiler decompiler);

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
        public static ContentFormat Detect(string? fileName)
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
}
