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
        public ArchiveIndex Index = new();

        
        private IndexLocation _indexLocation;
        private ArchiveFileInfo _files;
        
        private void Read(string filePath)
        {
            _files = ArchiveFileInfo.Resolve(filePath);
            ArchiveInfo = _files.Archive;

            var firstLine = _files.ReadFirstLine();
            Format = ArchiveFormat.Detect(firstLine, _files.IndexPairExists)
                     ?? throw new Exception("File is either not valid RenPy Archive or version is not recognized.");

            _indexLocation = Format.LocateIndex(_files.ArchivePath, firstLine, _files.IndexPath);
            ObfuscationKey = _indexLocation.ObfuscationKey;
            IndexInfo = _indexLocation.IsSeparateFile ? new FileInfo(_indexLocation.FilePath) : null;

            Index = ArchiveIndex.Read(_indexLocation);
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

        public byte[] ExtractData(string fileName)
        {
            if (!Index.ContainsKey(fileName))
            {
                throw new Exception("Specified file does not exist in RenPy Archive.");
            }

            if (Index[fileName].InArchive)
            {
                using var reader = new BinaryReader(File.OpenRead(_files.ArchivePath), Encoding.UTF8);
                byte[] finalData = [];

                foreach (var segment in Index[fileName].Segments)
                {
                    reader.BaseStream.Seek(segment.Offset, SeekOrigin.Begin);
                    var prefixData = segment.Prefix;
                    var fileData = reader.ReadBytes((int) segment.Length - segment.Prefix.Length); // Exported file max size ~2.14 GB
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

            var indexPath = Regex.Replace(archivePath, @"\.rpa$", ".rpi", RegexOptions.IgnoreCase);
            
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

                    // Place each file, remembering where it landed so the index can be
                    // written once every offset is known.
                    var storedFiles = new List<StoredFile>();
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

                        storedFiles.Add(new StoredFile(index.Value.TreePath, archiveOffset, content.Length));

                        archiveOffset += content.Length;
                    }

                    var key = format.UsesObfuscation ? ObfuscationKey : 0;
                    var fileCompressed = ArchiveIndex.Serialize(storedFiles, key);

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
