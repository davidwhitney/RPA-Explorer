using System;
using System.IO;
using System.Text;

namespace RpaParser
{
    /// <summary>
    /// The files an archive is made of on disk.
    ///
    /// Version 1 archives are a .rpa/.rpi pair and may be opened by either half, so the
    /// other is derived here and the archive is checked to exist. Everything downstream
    /// therefore starts from a resolved, present set of files rather than a path that might
    /// be anything.
    /// </summary>
    public sealed record ArchiveFileInfo
    {
        private const string ArchiveExtension = ".rpa";
        private const string IndexExtension = ".rpi";

        /// <summary>
        /// Resolves the pair from either half and checks the archive is there, so an
        /// instance always names files that are present.
        /// </summary>
        public ArchiveFileInfo(string path)
        {
            if (string.IsNullOrEmpty(path))
            {
                throw new Exception("No archive file provided.");
            }

            var (archivePath, indexPath) = ResolvePair(path);

            if (!File.Exists(archivePath))
            {
                throw new Exception("Archive file does not exist.");
            }

            ArchivePath = archivePath;
            IndexPath = indexPath;
            Archive = new FileInfo(archivePath);
            IndexPairExists = !string.IsNullOrEmpty(indexPath) && File.Exists(indexPath);
            FirstLine = ReadFirstLine(archivePath);
        }

        /// <summary>The .rpa file, whichever half of the pair was asked for.</summary>
        public string ArchivePath { get; }

        /// <summary>
        /// The sibling .rpi, or null when the name carried neither extension and so names
        /// no pair.
        /// </summary>
        public string IndexPath { get; }

        public FileInfo Archive { get; }

        /// <summary>
        /// True when both halves are present, which is how a version 1 archive is
        /// recognised - it carries no magic bytes of its own.
        /// </summary>
        public bool IndexPairExists { get; }

        /// <summary>
        /// The archive's header line, which is what identifies its format. Null for an
        /// empty file.
        /// </summary>
        public string FirstLine { get; }

        private static string ReadFirstLine(string archivePath)
        {
            using var reader = new StreamReader(archivePath, Encoding.UTF8);
            return reader.ReadLine();
        }

        /// <summary>
        /// Given either half of the pair, works out the other. The two cases are mutually
        /// exclusive - a path cannot end in both extensions.
        /// </summary>
        private static (string ArchivePath, string IndexPath) ResolvePair(string path)
        {
            if (path.EndsWith(ArchiveExtension, StringComparison.OrdinalIgnoreCase))
            {
                return (path, SwapExtension(path, IndexExtension));
            }

            if (path.EndsWith(IndexExtension, StringComparison.OrdinalIgnoreCase))
            {
                return (SwapExtension(path, ArchiveExtension), path);
            }

            // Neither extension: there is no pair to name, and only the formats that carry
            // magic bytes can recognise it.
            return (path, null);
        }

        /// <summary>
        /// Replaces the trailing extension while keeping the casing it is given. The
        /// extensions are matched case insensitively, so writing a lower case one back would
        /// derive "GAME.rpa" from "GAME.RPI" and find nothing on a case sensitive filesystem.
        /// Both extensions are the same length, so the casing can be copied per character.
        /// </summary>
        private static string SwapExtension(string path, string replacement)
        {
            var existing = path[^replacement.Length..];
            var swapped = new char[replacement.Length];

            for (var i = 0; i < replacement.Length; i++)
            {
                swapped[i] = char.IsUpper(existing[i])
                    ? char.ToUpperInvariant(replacement[i])
                    : replacement[i];
            }

            return path[..^replacement.Length] + new string(swapped);
        }
    }
}
