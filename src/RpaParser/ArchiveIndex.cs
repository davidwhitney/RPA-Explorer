using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using Razorvine.Pickle;
using RpaParser.Formats;

namespace RpaParser
{
    /// <summary>
    /// The table of files in an archive, keyed by their path inside it.
    ///
    /// RenPy stores this as a zlib compressed Python pickle, either at an offset inside the
    /// archive or as the whole of a sibling .rpi file. Reading and writing that form lives
    /// here rather than in <see cref="Archive"/>, which is left to deal in files and bytes.
    /// </summary>
    public sealed class ArchiveIndex : SortedDictionary<string, ArchiveEntry>
    {
        public ArchiveIndex()
        {
        }

        private ArchiveIndex(IDictionary<string, ArchiveEntry> entries) : base(entries)
        {
        }

        /// <summary>
        /// Reads the index from wherever the format said it lives. The location carries the
        /// obfuscation key, which is zero for formats that do not obfuscate, so the entries
        /// come back already decoded.
        /// </summary>
        public static ArchiveIndex Read(IndexFileInfo location)
        {
            var unpickled = Unpickle(ReadCompressed(location));
            var index = new ArchiveIndex();

            foreach (DictionaryEntry entry in (Hashtable) unpickled)
            {
                // RenPy has been known to leave null entries behind; they name no bytes.
                if (entry.Value == null)
                {
                    continue;
                }

                var segments = ((ArrayList) entry.Value)
                    .Cast<object[]>()
                    .Select(data => ArchiveSegment.FromIndexData(data, location.ObfuscationKey))
                    .ToList();

                var archiveEntry = ArchiveEntry.FromIndex((string) entry.Key, segments);
                index.Add(archiveEntry.TreePath, archiveEntry);
            }

            return index;
        }

        /// <summary>
        /// The compressed, pickled form written into an archive. Offsets and lengths are
        /// XORed with the key, which is zero for formats that do not obfuscate.
        /// </summary>
        public static byte[] Serialize(IEnumerable<StoredFile> storedFiles, long obfuscationKey)
        {
            var indexes = new Hashtable();

            foreach (var file in storedFiles)
            {
                // The third element is a prefix, which this writer never uses.
                object[] segment = obfuscationKey == 0
                    ? [file.Offset, file.Length]
                    : [file.Offset ^ obfuscationKey, file.Length ^ obfuscationKey, ""];

                indexes.Add(file.TreePath, new List<object[]> { segment });
            }

            byte[] pickled;
            using (var pickler = new Pickler())
            {
                pickled = pickler.dumps(indexes);
            }

            return Zlib.CompressBuffer(pickled);
        }

        /// <summary>
        /// A snapshot of the index. Entries are immutable, so the copy shares them and only
        /// the table itself is new.
        /// </summary>
        public ArchiveIndex Copy() => new(this);

        /// <summary>Files staged to be added, which are not yet stored in the archive.</summary>
        public IEnumerable<ArchiveEntry> Unsaved => Values.Where(entry => !entry.InArchive);

        private static byte[] ReadCompressed(IndexFileInfo location)
        {
            using var reader = new BinaryReader(File.OpenRead(location.FilePath), Encoding.UTF8);
            reader.BaseStream.Seek(location.Offset, SeekOrigin.Begin);

            var payloadSize = reader.BaseStream.Length;
            var remaining = payloadSize - location.Offset;

            return remaining > 0 ? reader.ReadBytes((int) remaining) : [];
        }

        private static object Unpickle(byte[] compressed)
        {
            var uncompressed = Zlib.UncompressBuffer(compressed);

            using var unpickler = new Unpickler();
            return unpickler.loads(uncompressed);
        }
    }
}
