using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;

namespace RpaParser
{
    /// <summary>
    /// One run of bytes belonging to a file. RenPy may store a file as several runs, and
    /// each may carry a prefix that is prepended to the bytes read from the archive.
    ///
    /// Immutable: a segment is created already usable, with any obfuscation undone, so no
    /// caller has to remember to decode it afterwards.
    /// </summary>
    public sealed record ArchiveSegment(long Offset, long Length, byte[] Prefix)
    {
        /// <summary>
        /// Builds a segment from one entry of the unpickled index, which is a two or three
        /// element list of offset, length and an optional prefix. The prefix arrives as
        /// either bytes or a string depending on which tool wrote the archive.
        ///
        /// From version 3 onwards the offset and length are XORed with the archive key;
        /// passing it here means the segment is never observed still obfuscated. The key
        /// defaults to zero, which leaves the values untouched for older formats.
        /// </summary>
        public static ArchiveSegment FromIndexData(object[] indexData, long obfuscationKey = 0)
        {
            var offset = Convert.ToInt64(indexData.GetValue(0)) ^ obfuscationKey;
            var length = Convert.ToInt64(indexData.GetValue(1)) ^ obfuscationKey;

            return new ArchiveSegment(offset, length, ReadPrefix(indexData));
        }

        private static byte[] ReadPrefix(object[] indexData)
        {
            if (indexData.Length < 3)
            {
                return [];
            }

            var prefix = indexData.GetValue(2);

            return prefix as byte[] ?? Encoding.UTF8.GetBytes((string) prefix);
        }
    }

    /// <summary>
    /// One file in an archive: where its bytes live and what it is called.
    ///
    /// An entry arrives one of two ways - read out of an archive index, or staged from a
    /// file on disk that is to be added - and each has a factory, so an entry is complete
    /// and consistent from the moment it exists.
    /// </summary>
    public sealed record ArchiveEntry
    {
        /// <summary>Path inside the archive.</summary>
        public string TreePath { get; init; } = string.Empty;

        public string ParentPath { get; init; } = string.Empty;

        /// <summary>Source path on disk, for a file staged to be added to the archive.</summary>
        public string FullPath { get; init; } = string.Empty;

        /// <summary>False while a file is staged but not yet written into an archive.</summary>
        public bool InArchive { get; init; }

        public long Length { get; init; }

        /// <summary>The runs making up this file, in order. Empty for a staged file.</summary>
        public IReadOnlyList<ArchiveSegment> Segments { get; init; } = [];

        /// <summary>An entry read from an archive's index.</summary>
        public static ArchiveEntry FromIndex(string treePath, IReadOnlyList<ArchiveSegment> segments) => new()
        {
            TreePath = treePath,
            ParentPath = Path.GetDirectoryName(treePath),
            InArchive = true,
            Segments = segments,
            Length = segments.Sum(segment => segment.Length)
        };

        /// <summary>
        /// A file on disk staged to be added to an archive. Its path inside the archive is
        /// taken relative to <paramref name="rootPath"/>, the directory the file was added
        /// from. Archive paths always use '/', whatever the local separator is.
        /// </summary>
        public static ArchiveEntry FromFilename(string path, string rootPath)
        {
            var fullPath = path.Replace('\\', '/');
            var root = (rootPath ?? string.Empty).Replace('\\', '/');
            var treePath = fullPath.Replace(root + "/", string.Empty);

            return new ArchiveEntry
            {
                FullPath = fullPath,
                TreePath = treePath,
                ParentPath = Path.GetDirectoryName(treePath),
                InArchive = false,
                Length = new FileInfo(path).Length
            };
        }
    }
}
