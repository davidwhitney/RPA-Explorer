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
    
    public class Parser
    {
        public class Version
        {
            public const double Unknown = -1;
            public const double Rpa1 = 1;
            public const double Rpa2 = 2;
            public const double Rpa3 = 3;
            public const double Rpa32 = 3.2;
        }
        
        private class ArchiveMagic
        {
            public const string Rpa1Rpa = ".rpa";
            public const string Rpa1Rpi = ".rpi";
            public const string Rpa2 = "RPA-2.0 ";
            public const string Rpa3 = "RPA-3.0 ";
            public const string Rpa32 = "RPA-3.2 ";
        }
        
        private class RpcMagic
        {
            public const string Rpc2 = "RENPY RPC2";
        }

        public FileInfo ArchiveInfo;
        public FileInfo IndexInfo;
        public double ArchiveVersion = Version.Unknown;
        public int Padding = 0;
        public long ObfuscationKey = 0xDEADBEEF;
        public bool OptionsConfirmed = false;
        public SortedDictionary<string,ArchiveIndex> Index = new ();

        public string PythonLocation = PythonLocator.Detected;
        public string UnrpycLocation = string.Empty;
        
        private long _offset;
        private string _archivePath;
        private string _indexPath;
        private string _firstLine;
        private string[] _metadata;

        public class Tuples
        {
            public long Offset;
            public long Length;
            public byte[] Prefix;
        }
        
        public class ArchiveIndex
        {
            public readonly SortedDictionary<int, Tuples> Tuples = new ();
            public string FullPath = string.Empty;
            public string TreePath = string.Empty;
            public string ParentPath = string.Empty;
            public bool InArchive;
            public long Length;
        }

        public class PreviewTypes
        {
            public const string Unknown = "unknown";
            public const string Image = "image";
            public const string Text = "text";
            public const string Video = "video";
            public const string Audio = "audio";
        }
        
        /*
        RenPy Supports:
        Images: JPEG/JPG, PNG, WEBP, BMP, GIF
        Sound/Music: OPUS, OGG Vorbis, FLAC, WAV, MP3, MP2
        Movies: WEBM, OGG Theora, VP9, VP8, MPEG 41, MPEG 2, MPEG 1
        */

        public readonly string[] ImageExtList =
        [
            ".jpeg",
            ".jpg",
            ".bmp",
            ".tiff",
            ".png",
            ".webp",
            ".exif",
            ".ico",
            ".gif"
        ];

        public readonly string[] AudioExtList =
        [
            ".aac",
            ".ac3",
            ".flac",
            ".mp3",
            ".wma",
            ".wav",
            ".ogg",
            ".cpc"
        ];

        public readonly string[] VideoExtList =
        [
            ".3gp",
            ".flv",
            ".mov",
            ".mp4",
            ".ogv",
            ".swf",
            ".mpg",
            ".mpeg",
            ".avi",
            ".mkv",
            ".wmv",
            ".webm"
        ];

        public readonly string[] TextExtList =
        [
            ".py",
            ".rpy~",
            ".rpy",
            ".txt",
            ".log",
            ".nfo",
            ".htm",
            ".html",
            ".xml",
            ".json",
            ".yaml",
            ".csv"
        ];

        public readonly string[] CodeExtList =
        [
            ".rpyc~",
            ".rpyc",
            ".rpymc~",
            ".rpymc"
        ];
        
        public void LoadArchive(string filePath)
        {
            _archivePath = filePath;
            GetIndexAndArchive();
            ArchiveInfo = GetArchiveInfo();
            _firstLine = GetFirstLine();
            ArchiveVersion = CheckSupportedVersion(GetVersion());
            
            if (CheckVersion(ArchiveVersion, Version.Rpa2) || CheckVersion(ArchiveVersion, Version.Rpa3) || CheckVersion(ArchiveVersion, Version.Rpa32))
            {
                _metadata = GetMetadata();
                _offset = GetOffset();
                ObfuscationKey = GetObfuscationKey();
            }
            else if (CheckVersion(ArchiveVersion, Version.Rpa1))
            {
                IndexInfo = GetIndexInfo();
            }

            Index = GetIndexes();
        }

        public bool CheckVersion(double version, double check)
        {
            var difference = version - check;
            if (difference == 0)
            {
                return true;
            }

            return false;
        }

        private void GetIndexAndArchive()
        {
            if (_archivePath.ToLower().EndsWith(ArchiveMagic.Rpa1Rpa))
            {
                _indexPath = Regex.Replace(_archivePath, @"\.rpa$", ".rpi", RegexOptions.IgnoreCase);
            }
            if (_archivePath.ToLower().EndsWith(ArchiveMagic.Rpa1Rpi))
            {
                _indexPath = _archivePath;
                _archivePath = Regex.Replace(_archivePath, @"\.rpi$", ".rpa", RegexOptions.IgnoreCase);
            }
        }

        public double CheckSupportedVersion(double version)
        {
            switch (version)
            {
                case Version.Rpa32:
                case Version.Rpa3:
                case Version.Rpa2:
                case Version.Rpa1:
                    // Version is OK
                    break;
                default:
                    throw new Exception("Specified version is not supported.");
            }
            
            return version;
        }

        private FileInfo GetArchiveInfo()
        {
            if (_archivePath == string.Empty)
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
            if (_indexPath == string.Empty)
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

        private double GetVersion()
        {
            if (_firstLine.StartsWith(ArchiveMagic.Rpa32))
            {
                return 3.2;
            }

            if (_firstLine.StartsWith(ArchiveMagic.Rpa3))
            {
                return 3;
            }

            if (_firstLine.StartsWith(ArchiveMagic.Rpa2))
            {
                return 2;
            }

            if (_archivePath.ToLower().EndsWith(ArchiveMagic.Rpa1Rpa) || _archivePath.ToLower().EndsWith(ArchiveMagic.Rpa1Rpi))
            {
                GetIndexAndArchive();
                if (File.Exists(_archivePath) && File.Exists(_indexPath))
                {
                    return 1;
                }
            }

            throw new Exception("File is either not valid RenPy Archive or version is not recognized.");
        }

        private string[] GetMetadata()
        {
            return _firstLine.Split(' ');
        }

        private long GetOffset()
        {
            return Convert.ToInt64(_metadata[1], 16);
        }

        private long GetObfuscationKey()
        {
            long key = 0;
            
            if (CheckVersion(ArchiveVersion, Version.Rpa3))
            {
                for(var i = 2; i < _metadata.Length; i++)
                {
                    key ^= Convert.ToInt64(_metadata[i], 16);
                }
            }
            else if (CheckVersion(ArchiveVersion, Version.Rpa32))
            {
                for(var i = 3; i < _metadata.Length; i++)
                {
                    key ^= Convert.ToInt64(_metadata[i], 16);
                }
            }

            return key;
        }
        
        private SortedDictionary<string,ArchiveIndex> GetIndexes()
        {
            var indexList = new SortedDictionary<string,ArchiveIndex>();
            object unpickledIndexes;

            var filePath = _archivePath;
            if (CheckVersion(ArchiveVersion, Version.Rpa1))
            {
                filePath = _indexPath;
            }
            
            using (var reader = new BinaryReader(File.OpenRead(filePath), Encoding.UTF8))
            {
                if (CheckVersion(ArchiveVersion, Version.Rpa2) || CheckVersion(ArchiveVersion, Version.Rpa3) || CheckVersion(ArchiveVersion, Version.Rpa32))
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

                var indexEntry = new ArchiveIndex
                {
                    TreePath = (string) kvp.Key,
                    ParentPath = Path.GetDirectoryName((string) kvp.Key),
                    InArchive = true
                };
                var counter = 0;
                foreach (object[] value in (ArrayList) kvp.Value)
                { 
                    var index = new Tuples
                    {
                        Offset = Convert.ToInt64(value.GetValue(0)),
                        Length = Convert.ToInt64(value.GetValue(1))
                    };
                    if ((long) value.Length == 3)
                    {
                        if (value.GetValue(2).GetType() == typeof(byte[]))
                        {
                            index.Prefix = (byte[]) value.GetValue(2);
                        }
                        else
                        {
                            index.Prefix = Encoding.UTF8.GetBytes((string) value.GetValue(2));
                        }
                    }
                    else
                    {
                        index.Prefix = [];
                    }

                    indexEntry.Tuples.Add(counter, index);
                    counter++;
                }
                indexList.Add(indexEntry.TreePath, indexEntry);
            }

            foreach (var kvp in indexList)
            {
                foreach (var kvpI in kvp.Value.Tuples)
                {
                    // Deobfuscate index data
                    if (ArchiveVersion >= Version.Rpa3)
                    {
                        kvpI.Value.Offset ^= ObfuscationKey;
                        kvpI.Value.Length ^= ObfuscationKey;
                    }

                    kvp.Value.Length += kvpI.Value.Length;
                }
            }

            return indexList;
        }

        public SortedDictionary<string, ArchiveIndex> DeepCopyIndex(SortedDictionary<string, ArchiveIndex> originalIndex)
        {
            var indexCopy = new SortedDictionary<string, ArchiveIndex>();
            
            foreach (var kvp in originalIndex)
            {
                var archIndex = new ArchiveIndex
                {
                    FullPath = kvp.Value.FullPath,
                    InArchive = kvp.Value.InArchive,
                    TreePath = kvp.Value.TreePath,
                    ParentPath = kvp.Value.ParentPath,
                    Length = kvp.Value.Length
                };
                
                foreach (var kvpI in kvp.Value.Tuples)
                {
                    var index = new Tuples
                    {
                        Length = kvpI.Value.Length,
                        Offset = kvpI.Value.Offset,
                        Prefix = kvpI.Value.Prefix
                    };
                    
                    archIndex.Tuples.Add(kvpI.Key, index);
                }
                
                indexCopy.Add(kvp.Key, archIndex);
            }
            
            return indexCopy;
        }
        
        public string RpycInfoBanner =
            "RPYC file contains compiled RenPy code. To preview code we need to use an external script called unrpyc for decompilation, plus a Python interpreter to run it. " +
            "Use Python 3 with current unrpyc for Ren'Py 8 games, or Python 2.7 with legacy unrpyc for Ren'Py 7 and older.";

        public string ParseRpyc(byte[] file)
        {
            var decompiled = string.Empty;
            if (PythonLocation == string.Empty)
            {
                throw new Exception(RpycInfoBanner + Environment.NewLine + Environment.NewLine + "ERROR: Python environment is not defined.");
            }
            
            if (!File.Exists(PythonLocation))
            {
                throw new Exception(RpycInfoBanner + Environment.NewLine + Environment.NewLine + "ERROR: Defined Python environment cannot be found (" + PythonLocation + ").");
            }

            if (UnrpycLocation == string.Empty)
            {
                throw new Exception(RpycInfoBanner + Environment.NewLine + Environment.NewLine + "ERROR: Location of unrpyc script is not defined.");
            }
            
            if (!File.Exists(UnrpycLocation))
            {
                throw new Exception(RpycInfoBanner + Environment.NewLine + Environment.NewLine + "ERROR: Defined location of unrpyc script cannot be found (" + UnrpycLocation + ").");
            }

            var tmpFile = Path.GetTempFileName();
            var decompiledFile = tmpFile + ".rpy";
            tmpFile += ".rpyc";
            var result = string.Empty;
            
            try
            {
                File.WriteAllBytes(tmpFile, file);
                
                var start = new ProcessStartInfo();
                start.FileName = PythonLocation;
                start.Arguments = string.Format(@"""{0}"" {1} ""{2}""", UnrpycLocation, "--try-harder", tmpFile);
                start.UseShellExecute = false;
                start.RedirectStandardOutput = true;
                start.RedirectStandardError = true;
                start.CreateNoWindow = true;
                using(var process = Process.Start(start))
                {
                    using(var reader = process.StandardOutput)
                    {
                        result += reader.ReadToEnd();
                    }
                    using(var reader = process.StandardError)
                    {
                        result += reader.ReadToEnd();
                    }
                }
                
                decompiled = NormalizeNewLines(File.ReadAllText(decompiledFile));
            }
            catch (Exception ex)
            {
                throw new Exception("ERROR: Decompilation failed with following error:" + Environment.NewLine + 
                                    Environment.NewLine + ex.Message + Environment.NewLine + Environment.NewLine + 
                                    "Return from unrpyc:" + Environment.NewLine + Environment.NewLine + result);
            }
            finally
            {
                if (File.Exists(tmpFile))
                {
                    File.Delete(tmpFile);
                }
                if (File.Exists(decompiledFile))
                {
                    File.Delete(decompiledFile);
                }
            }
            
            return decompiled;
        }

        public KeyValuePair<string, byte[]> GetPreviewRaw(string fileName)
        {
            var data = GetPreview(fileName, true);
            return new KeyValuePair<string, byte[]>(data.Key, (byte[]) data.Value);
        }

        public KeyValuePair<string, object> GetPreview(string fileName, bool returnRaw = false)
        {
            var data = new KeyValuePair<string, object>(PreviewTypes.Unknown, null);

            if (!Index.ContainsKey(fileName))
            {
                return data;
            }

            var fileInfo = new FileInfo(fileName);
            var bytes = ExtractData(fileName);

            if (ImageExtList.Contains(fileInfo.Extension.ToLower()))
            {
                // Return raw bytes; the UI decodes them cross-platform (WebP included).
                data = new KeyValuePair<string, object>(PreviewTypes.Image, bytes);
            }
            else if (TextExtList.Contains(fileInfo.Extension.ToLower()))
            {
                data = new KeyValuePair<string, object>(PreviewTypes.Text, NormalizeNewLines(Encoding.UTF8.GetString(bytes, 0, bytes.Length)));
            }
            else if (CodeExtList.Contains(fileInfo.Extension.ToLower()))
            {
                var decompiledString = ParseRpyc(bytes);

                if (decompiledString == string.Empty)
                {
                    data = new KeyValuePair<string, object>(PreviewTypes.Unknown, bytes);
                }
                else
                {
                    data = new KeyValuePair<string, object>(PreviewTypes.Text, decompiledString);
                }
            }
            else if (AudioExtList.Contains(fileInfo.Extension.ToLower()))
            {
                data = new KeyValuePair<string, object>(PreviewTypes.Audio, bytes);
            }
            else if (VideoExtList.Contains(fileInfo.Extension.ToLower()))
            {
                data = new KeyValuePair<string, object>(PreviewTypes.Video, bytes);
            }
            else
            {
                data = new KeyValuePair<string, object>(PreviewTypes.Unknown, bytes);
            }

            if (returnRaw)
            {
                data = new KeyValuePair<string, object>(data.Key, bytes);
            }

            return data;
        }

        private string NormalizeNewLines(string text)
        {
            const string winNewLine = "\r\n";
            const string linNewLine = "\n";
            const string macNewLine = "\r";
            
            var countWin = Regex.Matches(text, winNewLine).Count;
            var countLinux = Regex.Matches(text, linNewLine).Count;
            var countMac = Regex.Matches(text, macNewLine).Count;
            
            var newLineSymbol = Environment.NewLine;
            
            if (countWin >= countLinux && countWin >= countMac)
            {
                newLineSymbol = winNewLine;
            }
            else if (countLinux >= countWin && countLinux >= countMac)
            {
                newLineSymbol = linNewLine;
            }
            else if (countMac >= countWin && countMac >= countLinux)
            {
                newLineSymbol = macNewLine;
            }

            text = text.Replace(newLineSymbol, Environment.NewLine);
            
            return text;
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

                foreach (var kvpI in Index[fileName].Tuples)
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

        public string SaveArchive(string archivePath)
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
                    int archiveOffset;
                    switch (ArchiveVersion)
                    {
                        // File data starts immediately after the header, so these values are
                        // the exact header lengths. 3.2 carries an extra field, making its
                        // header nine bytes longer than 3.0's.
                        case Version.Rpa32:
                            archiveOffset = 43;
                            break;
                        case Version.Rpa3:
                            archiveOffset = 34;
                            break;
                        case Version.Rpa2:
                            archiveOffset = 25;
                            break;
                        case Version.Rpa1:
                            archiveOffset = 0;
                            break;
                        default:
                            throw new Exception("Specified version is not supported.");
                    }

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
                        if (CheckVersion(ArchiveVersion, Version.Rpa3) ||
                            CheckVersion(ArchiveVersion, Version.Rpa32))
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

                    if (!CheckVersion(ArchiveVersion, Version.Rpa1))
                    {
                        stream.Position = archiveOffset;
                        stream.Write(fileCompressed, 0, fileCompressed.Length);

                        var headerContent = string.Empty;

                        switch (ArchiveVersion)
                        {
                            case Version.Rpa32:
                                // 3.2 carries an extra field between the offset and the key,
                                // which is why GetObfuscationKey starts at index 3 for this
                                // version rather than 2. Writing a 3.0-shaped header here
                                // produced an archive this parser could not read back: the
                                // key landed at index 2, so it was read as 0 and every
                                // obfuscated offset decoded incorrectly.
                                headerContent = ArchiveMagic.Rpa32 + archiveOffset.ToString("x").PadLeft(16, '0') +
                                                " " +
                                                0.ToString("x").PadLeft(8, '0') +
                                                " " +
                                                ObfuscationKey.ToString("x").PadLeft(8, '0') + "\n";
                                break;
                            case Version.Rpa3:
                                headerContent = ArchiveMagic.Rpa3 + archiveOffset.ToString("x").PadLeft(16, '0') +
                                                " " +
                                                ObfuscationKey.ToString("x").PadLeft(8, '0') + "\n";
                                break;
                            case Version.Rpa2:
                                headerContent = ArchiveMagic.Rpa2 + archiveOffset.ToString("x").PadLeft(16, '0') +
                                                "\n";
                                break;
                        }

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
                    var testParse = new Parser();
                    testParse.LoadArchive(tmpPath + ".rpa");
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
