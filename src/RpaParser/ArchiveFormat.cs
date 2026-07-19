using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;

namespace RpaParser
{
    /// <summary>
    /// The file an archive's index lives in, and what is needed to decode it: where in that
    /// file it starts and the key its offsets are XORed with.
    /// </summary>
    public sealed record IndexFileInfo
    {
        private IndexFileInfo()
        {
        }

        public string FilePath { get; private init; }

        /// <summary>Where the index starts in that file. Zero when it is the whole file.</summary>
        public long Offset { get; private init; }

        /// <summary>Zero for formats that do not obfuscate.</summary>
        public long ObfuscationKey { get; private init; }

        public bool IsSeparateFile { get; private init; }

        /// <summary>
        /// The index's own file, or null when the index sits inside the archive and so has
        /// no file of its own to describe.
        /// </summary>
        public FileInfo File { get; private init; }

        public static IndexFileInfo InsideArchive(string archivePath, long offset, long obfuscationKey) => new()
        {
            FilePath = archivePath,
            Offset = offset,
            ObfuscationKey = obfuscationKey
        };

        /// <summary>An index that is the whole of a separate file, so unkeyed and unoffset.</summary>
        public static IndexFileInfo SeparateFile(string indexPath) => new()
        {
            FilePath = indexPath,
            IsSeparateFile = true,
            File = new FileInfo(indexPath)
        };
    }

    /// <summary>
    /// One archive format, recognised once and thereafter trusted.
    ///
    /// Everything that differs between RenPy archive versions - how an archive is
    /// recognised, where the index lives, how long the header is, whether offsets are
    /// obfuscated and how the header is written - is answered by the format object rather
    /// than by version comparisons spread through the parser. Detection returns the format
    /// for the bytes in hand, so code holding one already knows which rules apply.
    ///
    /// Versions 2 onwards share a shape: magic, then the index offset as sixteen hex
    /// digits, then zero or more eight digit fields. That shape lives here, so a format
    /// only states what is particular to it.
    /// </summary>
    public abstract class ArchiveFormat
    {
        private const int OffsetDigits = 16;
        private const int FieldDigits = 8;

        public abstract double Version { get; }

        public abstract string DisplayName { get; }

        protected abstract string Magic { get; }

        /// <summary>
        /// Index of the first header field forming the obfuscation key. Negative when the
        /// format does not obfuscate, which is what <see cref="UsesObfuscation"/> reports.
        /// </summary>
        protected virtual int KeyFieldIndex => -1;

        /// <summary>
        /// Header fields written after the offset, in order. Version 3.2 puts a spare field
        /// ahead of the key, which is why its key is read one position further along.
        /// </summary>
        protected virtual IEnumerable<long> FieldsAfterOffset(long obfuscationKey) => [];

        public virtual bool HasSeparateIndexFile => false;

        public virtual bool SupportsPadding => true;

        /// <summary>True when index offsets and lengths are XORed with the obfuscation key.</summary>
        public bool UsesObfuscation => KeyFieldIndex >= 0;

        /// <summary>
        /// Bytes reserved before the first file. Derived from the header the format actually
        /// writes, so the two cannot drift apart - a header longer than the space reserved
        /// for it overwrites the first file, which is what corrupted 3.2 archives.
        /// </summary>
        public virtual int HeaderLength => Encoding.UTF8.GetByteCount(BuildHeader(0, 0));

        public virtual bool Matches(string firstLine, bool indexPairExists) =>
            firstLine.StartsWith(Magic, StringComparison.Ordinal);

        public virtual string BuildHeader(long indexOffset, long obfuscationKey)
        {
            var fields = FieldsAfterOffset(obfuscationKey)
                .Select(field => " " + Hex(field, FieldDigits));

            return Magic + Hex(indexOffset, OffsetDigits) + string.Concat(fields) + "\n";
        }

        /// <summary>
        /// Reads the obfuscation key out of the whitespace separated header fields, by
        /// XORing everything from <see cref="KeyFieldIndex"/> onwards.
        /// </summary>
        public long ReadObfuscationKey(string[] headerFields)
        {
            if (!UsesObfuscation)
            {
                return 0;
            }

            long key = 0;
            for (var i = KeyFieldIndex; i < headerFields.Length; i++)
            {
                key ^= Convert.ToInt64(headerFields[i], 16);
            }
            return key;
        }

        /// <summary>
        /// Locates the index of an archive that has just been recognised. Formats that
        /// embed their index take its position and key from the header line; only version 1
        /// looks elsewhere.
        /// </summary>
        public virtual IndexFileInfo LocateIndex(ArchiveFileInfo files)
        {
            var headerFields = files.FirstLine.Split(' ');

            return IndexFileInfo.InsideArchive(
                files.ArchivePath,
                Convert.ToInt64(headerFields[1], 16),
                ReadObfuscationKey(headerFields));
        }

        public static ArchiveFormat Rpa1 { get; } = new Rpa1Format();
        public static ArchiveFormat Rpa2 { get; } = new Rpa2Format();
        public static ArchiveFormat Rpa3 { get; } = new Rpa3Format();
        public static ArchiveFormat Rpa32 { get; } = new Rpa32Format();

        /// <summary>Every known format, most specific first.</summary>
        public static IReadOnlyList<ArchiveFormat> All { get; } = [Rpa32, Rpa3, Rpa2, Rpa1];

        /// <summary>
        /// The format matching the archive, or null when nothing recognises it.
        /// </summary>
        public static ArchiveFormat Detect(string firstLine, bool indexPairExists) =>
            All.FirstOrDefault(format => format.Matches(firstLine ?? string.Empty, indexPairExists));

        public static ArchiveFormat Detect(ArchiveFileInfo files) =>
            Detect(files.FirstLine, files.IndexPairExists);

        /// <summary>The format for a numeric version, or null when it is not supported.</summary>
        public static ArchiveFormat ForVersion(double version) =>
            All.FirstOrDefault(format => format.Version == version);

        public override string ToString() => DisplayName;

        private static string Hex(long value, int digits) =>
            value.ToString("x").PadLeft(digits, '0');
    }

    /// <summary>
    /// Version 1: a .rpa/.rpi pair with no header at all. It is recognised by the presence
    /// of both halves rather than by any magic bytes, and so overrides most of the shape
    /// the later versions share.
    /// </summary>
    public sealed class Rpa1Format : ArchiveFormat
    {
        public override double Version => 1;
        public override string DisplayName => "RPA 1.0";
        protected override string Magic => string.Empty;

        public override bool HasSeparateIndexFile => true;

        // The files are written back to back with no header to offset them.
        public override bool SupportsPadding => false;
        public override int HeaderLength => 0;

        public override bool Matches(string firstLine, bool indexPairExists) => indexPairExists;

        public override string BuildHeader(long indexOffset, long obfuscationKey) =>
            throw new InvalidOperationException(
                "Version 1 archives have no header; the index is written to a separate .rpi file.");

        /// <summary>
        /// The index is the whole of the sibling .rpi file, so there is no offset to seek to
        /// and no key to undo.
        /// </summary>
        public override IndexFileInfo LocateIndex(ArchiveFileInfo files)
        {
            if (string.IsNullOrEmpty(files.IndexPath))
            {
                throw new Exception("No index file provided.");
            }

            if (!files.IndexPairExists)
            {
                throw new Exception("Index file does not exist.");
            }

            return IndexFileInfo.SeparateFile(files.IndexPath);
        }
    }

    /// <summary>Version 2: magic and index offset, nothing more.</summary>
    public sealed class Rpa2Format : ArchiveFormat
    {
        public override double Version => 2;
        public override string DisplayName => "RPA 2.0";
        protected override string Magic => "RPA-2.0 ";
    }

    /// <summary>Version 3: adds an obfuscation key in the field after the offset.</summary>
    public sealed class Rpa3Format : ArchiveFormat
    {
        public override double Version => 3;
        public override string DisplayName => "RPA 3.0";
        protected override string Magic => "RPA-3.0 ";

        protected override int KeyFieldIndex => 2;

        protected override IEnumerable<long> FieldsAfterOffset(long obfuscationKey) => [obfuscationKey];
    }

    /// <summary>
    /// Version 3.2: as version 3, but with a spare field between the offset and the key,
    /// which is why the key is read one field further along.
    /// </summary>
    public sealed class Rpa32Format : ArchiveFormat
    {
        public override double Version => 3.2;
        public override string DisplayName => "RPA 3.2";
        protected override string Magic => "RPA-3.2 ";

        protected override int KeyFieldIndex => 3;

        protected override IEnumerable<long> FieldsAfterOffset(long obfuscationKey) => [0, obfuscationKey];
    }
}
