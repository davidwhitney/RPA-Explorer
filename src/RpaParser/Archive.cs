using System;
using System.Collections;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;
using Razorvine.Pickle;

namespace RpaParser
{
    // Inspired by: https://github.com/Shizmob/rpatool
    // Inspired by: https://github.com/CensoredUsername/unrpyc
    
    public sealed class Archive
    {
        
        private static class FileExtension
        {
            public const string Rpa = ".rpa";
            public const string Rpi = ".rpi";
            public const string Rpa2 = "RPA-2.0 ";
            public const string Rpa3 = "RPA-3.0 ";
            public const string Rpa32 = "RPA-3.2 ";
        }
        
        public FileInfo ArchiveInfo;
        public FileInfo IndexInfo;
        /// <summary>
        /// The archive format: detected when loading, chosen by the caller when saving.
        /// Null until one or the other has happened.
        /// </summary>
        public ArchiveFormat Format { get; set; }
        public int Padding = 0;
        public long ObfuscationKey = 0xDEADBEEF;
        public bool OptionsConfirmed = false;
        public SortedDictionary<string,ArchiveEntry> Index = new ();

        
        private long _offset;
        private string _archivePath;
        private string _indexPath;
        private string _firstLine;
        private string[] _metadata;

        
        
        
        private void Read(string filePath)
        {
            _archivePath = filePath;
            GetIndexAndArchive();
            ArchiveInfo = GetArchiveInfo();
            _firstLine = GetFirstLine();
            Format = DetectFormat();

            if (Format.HasSeparateIndexFile)
            {
                IndexInfo = GetIndexInfo();
            }
            else
            {
                _metadata = GetMetadata();
                _offset = GetOffset();
                ObfuscationKey = Format.ReadObfuscationKey(_metadata);
            }

            Index = GetIndexes();
        }

        /// <summary>
        /// A version 1 archive is a .rpa/.rpi pair, so given either half the other is derived.
        /// The two cases are mutually exclusive - a path cannot end in both extensions.
        /// </summary>
        /// <summary>Opens an archive from disk.</summary>
        public static Archive Load(string path)
        {
            var archive = new Archive();
            archive.Read(path);
            return archive;
        }

        /// <summary>Starts a new, empty archive in the given format.</summary>
        public static Archive Create(ArchiveFormat format) => new() { Format = format };

        /// <summary>
        /// Starts a new, empty archive whose format is chosen later, when it is saved.
        /// </summary>
        public static Archive Create() => new();

        private void GetIndexAndArchive()
        {
            if (_archivePath.EndsWith(FileExtension.Rpa, StringComparison.OrdinalIgnoreCase))
            {
                _indexPath = SwapExtension(_archivePath, FileExtension.Rpi);
            }
            else if (_archivePath.EndsWith(FileExtension.Rpi, StringComparison.OrdinalIgnoreCase))
            {
                _indexPath = _archivePath;
                _archivePath = SwapExtension(_archivePath, FileExtension.Rpa);
            }
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

        private FileInfo GetArchiveInfo()
        {
            if (string.IsNullOrEmpty(_archivePath))
            {
                throw new Exception("No archive file provided.");
            }

            if (!File.Exists(_archivePath))
            {
                throw new Exception("Archive file does not exist.");
            }

            return new FileInfo(_archivePath);
        }

        private FileInfo GetIndexInfo()
        {
            if (string.IsNullOrEmpty(_indexPath))
            {
                throw new Exception("No index file provided.");
            }

            if (!File.Exists(_indexPath))
            {
                throw new Exception("Index file does not exist.");
            }

            return new FileInfo(_indexPath);
        }

        private string GetFirstLine()
        {
            using var streamReader = new StreamReader(_archivePath, Encoding.UTF8);
            return streamReader.ReadLine();
        }

        private ArchiveFormat DetectFormat()
        {
            // Version 1 carries no magic bytes; it is recognised by both halves of the pair
            // being present, which GetIndexAndArchive has already resolved.
            var indexPairExists = !string.IsNullOrEmpty(_indexPath)
                                  && File.Exists(_archivePath)
                                  && File.Exists(_indexPath);

            return ArchiveFormat.Detect(_firstLine, indexPairExists)
                   ?? throw new Exception("File is either not valid RenPy Archive or version is not recognized.");
        }

        private string[] GetMetadata()
        {
            return _firstLine.Split(' ');
        }

        private long GetOffset()
        {
            return Convert.ToInt64(_metadata[1], 16);
        }

        
        private SortedDictionary<string,ArchiveEntry> GetIndexes()
        {
            var indexList = new SortedDictionary<string,ArchiveEntry>();
            object unpickledIndexes;

            var filePath = Format.HasSeparateIndexFile ? _indexPath : _archivePath;
            
            using (var reader = new BinaryReader(File.OpenRead(filePath), Encoding.UTF8))
            {
                if (!Format.HasSeparateIndexFile)
                {
                    reader.BaseStream.Seek(_offset, SeekOrigin.Begin);
                }

                var blockOffset = _offset;
                long blockSize = 2046;
                var payloadSize = reader.BaseStream.Length;
                byte[] fileCompressed = [];

                while (blockSize > 0)
                {
                    //long remaining = payloadSize - blockOffset;
                    if (blockOffset + blockSize > payloadSize)
                    {
                        blockSize = payloadSize - blockOffset;

                        if (blockSize < 0)
                        {
                            blockSize = 0;
                        }
                    }

                    if (blockSize != 0)
                    {
                        var buffer = reader.ReadBytes((int) blockSize);
                        fileCompressed = fileCompressed.Concat(buffer).ToArray();

                        blockOffset += blockSize;
                        reader.BaseStream.Seek(blockOffset, SeekOrigin.Begin);
                    }
                }

                var fileUncompressed = Zlib.UncompressBuffer(fileCompressed);
                using (var unpickler = new Unpickler())
                {
                    unpickledIndexes = unpickler.loads(fileUncompressed);
                }
            }
            
            // Standardize output
            foreach (DictionaryEntry kvp in (Hashtable) unpickledIndexes)
            {
                if (kvp.Value == null)
                {
                    continue;
                }

                var indexEntry = new ArchiveEntry
                {
                    TreePath = (string) kvp.Key,
                    ParentPath = Path.GetDirectoryName((string) kvp.Key),
                    InArchive = true
                };
                var key = Format.UsesObfuscation ? ObfuscationKey : 0;
                var counter = 0;
                foreach (object[] value in (ArrayList) kvp.Value)
                {
                    indexEntry.Segments.Add(counter, ArchiveSegment.FromIndexData(value, key));
                    counter++;
                }
                indexList.Add(indexEntry.TreePath, indexEntry);
            }

            foreach (var kvp in indexList)
            {
                foreach (var segment in kvp.Value.Segments.Values)
                {
                    kvp.Value.Length += segment.Length;
                }
            }

            return indexList;
        }

        public SortedDictionary<string, ArchiveEntry> CopyIndex(SortedDictionary<string, ArchiveEntry> originalIndex)
        {
            var indexCopy = new SortedDictionary<string, ArchiveEntry>();
            
            foreach (var kvp in originalIndex)
            {
                var archIndex = new ArchiveEntry
                {
                    FullPath = kvp.Value.FullPath,
                    InArchive = kvp.Value.InArchive,
                    TreePath = kvp.Value.TreePath,
                    ParentPath = kvp.Value.ParentPath,
                    Length = kvp.Value.Length
                };
                
                foreach (var kvpI in kvp.Value.Segments)
                {
                    // Segments are immutable, so the copy can share them.
                    archIndex.Segments.Add(kvpI.Key, kvpI.Value);
                }
                
                indexCopy.Add(kvp.Key, archIndex);
            }
            
            return indexCopy;
        }
        
        public byte[] ExtractData(string fileName)
        {
            if (!Index.ContainsKey(fileName))
            {
                throw new Exception("Specified file does not exist in RenPy Archive.");
            }

            if (Index[fileName].InArchive)
            {
                using var reader = new BinaryReader(File.OpenRead(_archivePath), Encoding.UTF8);
                byte[] finalData = [];

                foreach (var kvpI in Index[fileName].Segments)
                {
                    reader.BaseStream.Seek(kvpI.Value.Offset, SeekOrigin.Begin);
                    var prefixData = kvpI.Value.Prefix;
                    var fileData = reader.ReadBytes((int) kvpI.Value.Length - kvpI.Value.Prefix.Length); // Exported file max size ~2.14 GB
                    var partData = new byte[finalData.Length + prefixData.Length + fileData.Length];
                    Buffer.BlockCopy(finalData, 0, partData, 0, finalData.Length);
                    Buffer.BlockCopy(prefixData, 0, partData, finalData.Length, prefixData.Length);
                    Buffer.BlockCopy(fileData, 0, partData, finalData.Length + prefixData.Length, fileData.Length);
                    finalData = partData;
                }

                return finalData;
            }
            
            return File.ReadAllBytes(Index[fileName].FullPath);
        }

        public string Extract(string fileName, string exportPath)
        {
            var finalData = ExtractData(fileName);
            // Archive tree paths always use '/'; convert to the local separator.
            var relativePath = fileName.Replace('/', Path.DirectorySeparatorChar);
            string baseDir;
            if (exportPath.Trim() == string.Empty)
            {
                baseDir = ArchiveInfo.DirectoryName;
            }
            else
            {
                if (!Directory.Exists(exportPath.Trim()))
                {
                    throw new Exception("Selected export path does not exist.");
                }
                baseDir = exportPath.Trim();
            }

            var finalPath = Path.Combine(baseDir, relativePath);

            Directory.CreateDirectory(Path.GetDirectoryName(finalPath) ?? throw new InvalidOperationException());
            File.WriteAllBytes(finalPath, finalData);

            return finalPath;
        }

        public string Save(string archivePath)
        {
            if (archivePath.ToLower().EndsWith(".rpi"))
            {
                archivePath = Regex.Replace(archivePath, @"\.rpi$", ".rpa", RegexOptions.IgnoreCase);
            }
            
            if (!archivePath.ToLower().EndsWith(".rpa"))
            {
                archivePath += ".rpa";
            }

            var tmpPath = Regex.Replace(archivePath, @"\.rpa$", "", RegexOptions.IgnoreCase);
            tmpPath = tmpPath.Substring(0, Math.Min(100, tmpPath.Length - 1)) + "_" +
                      DateTime.Now.ToString("yyyyMMddHHmmss") + "_" + Guid.NewGuid().ToString("N");

            /*if (archivePath == _archivePath && _archivePath != String.Empty)
            {
                throw new Exception("Cannot overwrite same archive file that is loaded.");
            }*/

            var indexPath = Regex.Replace(archivePath, @"\.rpa$", ".rpi", RegexOptions.IgnoreCase);

            /*if (indexPath == _indexPath && _indexPath != String.Empty)
            {
                throw new Exception("Cannot overwrite same index file that is loaded.");
            }*/
            
            BuildArchive(archivePath, indexPath, tmpPath);

            return archivePath;
        }

        private void BuildArchive(string archivePath, string indexPath, string tmpPath)
        {
            try
            {
                if (!File.Exists(tmpPath + ".rpa"))
                {
                    File.WriteAllBytes(tmpPath + ".rpa", []);
                }

                using (Stream stream = File.Open(tmpPath + ".rpa", FileMode.Truncate))
                {
                    var format = Format
                                 ?? throw new Exception("Specified version is not supported.");

                    // File data starts immediately after the header.
                    var archiveOffset = format.HeaderLength;

                    stream.Position = archiveOffset;

                    var rnd = new Random();

                    // Update indexes
                    var indexes = new Hashtable();
                    foreach (var index in Index)
                    {
                        var content = ExtractData(index.Key);

                        if (Padding > 0)
                        {
                            var paddingStr = string.Empty;
                            var paddingLength = rnd.Next(1, Padding);

                            while (paddingLength > 0)
                            {
                                paddingStr += Encoding.ASCII.GetString(new[] {(byte) rnd.Next(1, 255)});
                                paddingLength--;
                            }

                            var paddingBytes = Encoding.ASCII.GetBytes(paddingStr);
                            archiveOffset += paddingBytes.Length;
                        }

                        stream.Position = archiveOffset;
                        stream.Write(content, 0, content.Length);

                        List<object[]> indexData = [];
                        if (format.UsesObfuscation)
                        {
                            indexData.Add([archiveOffset ^ ObfuscationKey, content.Length ^ ObfuscationKey, ""]); // Last is prefix
                        }
                        else
                        {
                            indexData.Add([archiveOffset, content.Length]);
                        }

                        archiveOffset += content.Length;

                        indexes.Add(index.Value.TreePath, indexData);
                    }

                    byte[] pickledIndexes;
                    using (var pickler = new Pickler())
                    {
                        pickledIndexes = pickler.dumps(indexes);
                    }

                    var fileCompressed = Zlib.CompressBuffer(pickledIndexes);

                    if (!format.HasSeparateIndexFile)
                    {
                        stream.Position = archiveOffset;
                        stream.Write(fileCompressed, 0, fileCompressed.Length);

                        var headerContent = format.BuildHeader(archiveOffset, ObfuscationKey);

                        var headerContentByte = Encoding.UTF8.GetBytes(headerContent);

                        stream.Position = 0;
                        stream.Write(headerContentByte, 0, headerContentByte.Length);
                    }
                    else
                    {
                        File.WriteAllBytes(tmpPath + ".rpi", fileCompressed);
                    }
                }

                try
                {
                    // Test if archive is corrupted or not
                    var testParse = new Archive();
                    testParse.Read(tmpPath + ".rpa");
                }
                catch (Exception ex)
                {
                    throw new Exception("Validation of newly created archive failed. This usually means corrupted archive file after creation. No harm was done to original archive. Parser failed with following error during validation: " + ex.Message);
                }

                File.Copy(tmpPath + ".rpa", archivePath, true);
                File.Delete(tmpPath + ".rpa");
                if (File.Exists(tmpPath + ".rpi"))
                {
                    File.Copy(tmpPath + ".rpi", indexPath, true);
                    File.Delete(tmpPath + ".rpi");
                }
            }
            catch
            {
                if (File.Exists(tmpPath + ".rpa"))
                {
                    File.Delete(tmpPath + ".rpa");
                }
                if (File.Exists(tmpPath + ".rpi"))
                {
                    File.Delete(tmpPath + ".rpi");
                }

                throw;
            }
        }
    }
}
