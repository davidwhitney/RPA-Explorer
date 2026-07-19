using System.Collections.Generic;
using System.IO;
using System.Linq;

namespace RpaParser
{
    public sealed record ArchiveEntry
    {
        public string TreePath { get; init; } = string.Empty;
        public string ParentPath { get; private init; } = string.Empty;
        public string FullPath { get; init; } = string.Empty;
        public bool InArchive { get; init; }

        public long Length { get; init; }

        public IReadOnlyList<ArchiveSegment> Segments { get; private init; } = [];

        public static ArchiveEntry FromIndex(string treePath, IReadOnlyList<ArchiveSegment> segments) => new()
        {
            TreePath = treePath,
            ParentPath = Path.GetDirectoryName(treePath) ?? string.Empty,
            InArchive = true,
            Segments = segments,
            Length = segments.Sum(segment => segment.Length)
        };

        public static ArchiveEntry FromFilename(string path, string? rootPath)
        {
            var fullPath = path.Replace('\\', '/');
            var root = (rootPath ?? string.Empty).Replace('\\', '/');
            var treePath = fullPath.Replace(root + "/", string.Empty);

            return new ArchiveEntry
            {
                FullPath = fullPath,
                TreePath = treePath,
                ParentPath = Path.GetDirectoryName(treePath) ?? string.Empty,
                InArchive = false,
                Length = new FileInfo(path).Length
            };
        }
    }
}
