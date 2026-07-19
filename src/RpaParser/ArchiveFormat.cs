using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;

namespace RpaParser
{
    /// <summary>Where an archive's index lives and what is needed to decode it.</summary>
    public sealed record IndexLocation(string FilePath, long Offset, long ObfuscationKey)
    {
        /// <summary>True when the index is a file of its own rather than part of the archive.</summary>
        public bool IsSeparateFile { get; init; }
    }

    /// <summary>
    /// One archive format, recognised once and thereafter trusted.
    ///
    /// Everything that differs between RenPy archive versions - how an archive is
    /// recognised, where the index lives, how long the header is, whether offsets are
    /// obfuscated and how the header is written - is answered by the format object rather
    /// than by version comparisons spread through the parser. Detection returns the format
    /// for the bytes in hand, so code holding one already knows which rules apply.
    /// </summary>
    public abstract class ArchiveFormat
    {
        /// <summary>Numeric version, as reported by <see cref="Parser.ArchiveVersion"/>.</summary>
        public abstract double Version { get; }

        /// <summary>True when the index is a separate .rpi file rather than part of the archive.</summary>
        public abstract bool HasSeparateIndexFile { get; }

        /// <summary>True when index offsets and lengths are XORed with the obfuscation key.</summary>
        public abstract bool UsesObfuscation { get; }

        /// <summary>True when random padding may be inserted between stored files.</summary>
        public virtual bool SupportsPadding => true;

        /// <summary>Name shown to the user.</summary>
        public abstract string DisplayName { get; }

        /// <summary>
        /// Bytes reserved before the first file. File data starts immediately after the
        /// header, so this is exactly the length <see cref="BuildHeader"/> produces.
        /// </summary>
        public abstract int HeaderLength { get; }

        /// <summary>Whether this format recognises the archive in hand.</summary>
        public abstract bool Matches(string firstLine, bool indexPairExists);

        /// <summary>The header written ahead of the file data.</summary>
        public abstract string BuildHeader(long indexOffset, long obfuscationKey);

        /// <summary>
        /// Reads the obfuscation key out of the whitespace separated header fields.
        /// Formats without obfuscation have no key.
        /// </summary>
        public virtual long ReadObfuscationKey(string[] headerFields) => 0;

        /// <summary>
        /// Locates the index of an archive that has just been recognised. Formats that
        /// embed their index take its position and key from the header line; only version 1
        /// looks elsewhere.
        /// </summary>
        public virtual IndexLocation LocateIndex(string archivePath, string firstLine, string indexPath)
        {
            var headerFields = firstLine.Split(' ');

            return new IndexLocation(
                archivePath,
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

        /// <summary>The format for a numeric version, or null when it is not supported.</summary>
        public static ArchiveFormat ForVersion(double version) =>
            All.FirstOrDefault(format => format.Version == version);

        public override string ToString() => DisplayName;

        // Offsets are written as 16 hex digits and keys as 8, which is what fixes the
        // header lengths below.
        private protected static string Hex(long value, int digits) =>
            value.ToString("x").PadLeft(digits, '0');
    }

    /// <summary>
    /// Version 1: a .rpa/.rpi pair with no header at all. It is recognised by the presence
    /// of both halves rather than by any magic bytes.
    /// </summary>
    public sealed class Rpa1Format : ArchiveFormat
    {
        public override double Version => 1;
        public override string DisplayName => "RPA 1.0";
        public override bool HasSeparateIndexFile => true;
        public override bool UsesObfuscation => false;

        // Version 1 writes the files back to back with no header to offset them.
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
        public override IndexLocation LocateIndex(string archivePath, string firstLine, string indexPath)
        {
            if (string.IsNullOrEmpty(indexPath))
            {
                throw new Exception("No index file provided.");
            }

            if (!File.Exists(indexPath))
            {
                throw new Exception("Index file does not exist.");
            }

            return new IndexLocation(indexPath, 0, 0) { IsSeparateFile = true };
        }
    }

    /// <summary>Version 2: magic and index offset, no obfuscation.</summary>
    public sealed class Rpa2Format : ArchiveFormat
    {
        private const string Magic = "RPA-2.0 ";

        public override double Version => 2;
        public override string DisplayName => "RPA 2.0";
        public override bool HasSeparateIndexFile => false;
        public override bool UsesObfuscation => false;
        public override int HeaderLength => Magic.Length + 16 + 1;

        public override bool Matches(string firstLine, bool indexPairExists) =>
            firstLine.StartsWith(Magic, StringComparison.Ordinal);

        public override string BuildHeader(long indexOffset, long obfuscationKey) =>
            Magic + Hex(indexOffset, 16) + "\n";
    }

    /// <summary>Version 3: adds an obfuscation key, held in the field after the offset.</summary>
    public sealed class Rpa3Format : ArchiveFormat
    {
        private const string Magic = "RPA-3.0 ";

        /// <summary>Header fields from this index onwards are XORed together to form the key.</summary>
        private const int KeyFieldIndex = 2;

        public override double Version => 3;
        public override string DisplayName => "RPA 3.0";
        public override bool HasSeparateIndexFile => false;
        public override bool UsesObfuscation => true;
        public override int HeaderLength => Magic.Length + 16 + 1 + 8 + 1;

        public override bool Matches(string firstLine, bool indexPairExists) =>
            firstLine.StartsWith(Magic, StringComparison.Ordinal);

        public override string BuildHeader(long indexOffset, long obfuscationKey) =>
            Magic + Hex(indexOffset, 16) + " " + Hex(obfuscationKey, 8) + "\n";

        public override long ReadObfuscationKey(string[] headerFields) =>
            XorFrom(headerFields, KeyFieldIndex);

        internal static long XorFrom(string[] headerFields, int firstField)
        {
            long key = 0;
            for (var i = firstField; i < headerFields.Length; i++)
            {
                key ^= Convert.ToInt64(headerFields[i], 16);
            }
            return key;
        }
    }

    /// <summary>
    /// Version 3.2: as version 3, but with an extra field between the offset and the key,
    /// which is why the key is read from one field further along and the header is nine
    /// bytes longer.
    /// </summary>
    public sealed class Rpa32Format : ArchiveFormat
    {
        private const string Magic = "RPA-3.2 ";
        private const int KeyFieldIndex = 3;

        public override double Version => 3.2;
        public override string DisplayName => "RPA 3.2";
        public override bool HasSeparateIndexFile => false;
        public override bool UsesObfuscation => true;
        public override int HeaderLength => Magic.Length + 16 + 1 + 8 + 1 + 8 + 1;

        public override bool Matches(string firstLine, bool indexPairExists) =>
            firstLine.StartsWith(Magic, StringComparison.Ordinal);

        public override string BuildHeader(long indexOffset, long obfuscationKey) =>
            Magic + Hex(indexOffset, 16) + " " + Hex(0, 8) + " " + Hex(obfuscationKey, 8) + "\n";

        public override long ReadObfuscationKey(string[] headerFields) =>
            Rpa3Format.XorFrom(headerFields, KeyFieldIndex);
    }
}
