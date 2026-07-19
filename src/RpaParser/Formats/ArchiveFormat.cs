using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace RpaParser.Formats
{
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

        public virtual bool IsKnown => true;
        public virtual bool HasSeparateIndexFile => false;
        public virtual bool SupportsPadding => true;

        /// <summary>True when index offsets and lengths are XORed with the obfuscation key.</summary>
        public bool UsesObfuscation => KeyFieldIndex >= 0;

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
            // Only version 1 has no header line, and it overrides this.
            var headerFields = (files.FirstLine ?? throw new Exception("Archive has no header line."))
                .Split(' ');

            return IndexFileInfo.InsideArchive(
                files.ArchivePath,
                Convert.ToInt64(headerFields[1], 16),
                ReadObfuscationKey(headerFields));
        }

        public static ArchiveFormat Rpa1 { get; } = new Rpa1Format();
        public static ArchiveFormat Rpa2 { get; } = new Rpa2Format();
        public static ArchiveFormat Rpa3 { get; } = new Rpa3Format();
        public static ArchiveFormat Rpa32 { get; } = new Rpa32Format();
        public static ArchiveFormat Unknown { get; } = new UnknownFormat();
        public static IReadOnlyList<ArchiveFormat> All { get; } = [Rpa32, Rpa3, Rpa2, Rpa1];
        
        public static ArchiveFormat Detect(string? firstLine, bool indexPairExists) =>
            All.FirstOrDefault(format => format.Matches(firstLine ?? string.Empty, indexPairExists)) ?? Unknown;

        public static ArchiveFormat Detect(ArchiveFileInfo files) =>
            Detect(files.FirstLine, files.IndexPairExists);

        public static ArchiveFormat ForVersion(double version) =>
            All.FirstOrDefault(format => format.Version == version) ?? Unknown;

        public override string ToString() => DisplayName;

        private static string Hex(long value, int digits) =>
            value.ToString("x").PadLeft(digits, '0');
    }
}
