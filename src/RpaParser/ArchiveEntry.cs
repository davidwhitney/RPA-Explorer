using System.Collections.Generic;

namespace RpaParser
{
    /// <summary>
    /// One run of bytes belonging to a file. RenPy may store a file as several runs, and
    /// each may carry a prefix that is prepended to the bytes read from the archive.
    /// </summary>
    public class ArchiveSegment
    {
        public long Offset;
        public long Length;
        public byte[] Prefix;
    }

    /// <summary>One file in an archive: where its bytes live and what it is called.</summary>
    public class ArchiveEntry
    {
        /// <summary>The runs making up this file, in order.</summary>
        public readonly SortedDictionary<int, ArchiveSegment> Segments = new();

        /// <summary>Source path on disk, for a file staged to be added to the archive.</summary>
        public string FullPath = string.Empty;

        /// <summary>Path inside the archive.</summary>
        public string TreePath = string.Empty;

        public string ParentPath = string.Empty;

        /// <summary>False while a file is staged but not yet written into an archive.</summary>
        public bool InArchive;

        public long Length;
    }
}
