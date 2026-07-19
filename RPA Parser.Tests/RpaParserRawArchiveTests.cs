using System;
using System.Collections;
using System.IO;
using System.Text;
using Razorvine.Pickle;
using RPA_Parser;
using Shouldly;

namespace RPAParser.Tests;

/// <summary>
/// Archives assembled byte by byte, covering index shapes that real RenPy archives use but
/// that this parser's own writer never emits - notably byte-array prefixes and null entries.
/// </summary>
public class RpaParserRawArchiveTests
{
    private const int Rpa3HeaderLength = 34;

    /// <summary>
    /// Writes a valid RPA-3 archive whose index is supplied verbatim, so a test can control
    /// the exact tuple shape stored for each entry.
    /// </summary>
    private static string WriteRpa3(TempWorkspace workspace, string name, byte[] payload,
        Func<int, long, Hashtable> buildIndex, long key = 0xDEADBEEF, long? headerOffsetOverride = null)
    {
        Hashtable index = buildIndex(Rpa3HeaderLength, key);

        byte[] pickled;
        using (Pickler pickler = new Pickler())
        {
            pickled = pickler.dumps(index);
        }
        byte[] compressedIndex = Zlib.CompressBuffer(pickled);

        int indexOffset = Rpa3HeaderLength + payload.Length;
        string header = "RPA-3.0 " + (headerOffsetOverride ?? indexOffset).ToString("x").PadLeft(16, '0')
                        + " " + key.ToString("x").PadLeft(8, '0') + "\n";
        byte[] headerBytes = Encoding.UTF8.GetBytes(header);
        headerBytes.Length.ShouldBe(Rpa3HeaderLength);

        string path = workspace.Path_(name);
        using FileStream stream = File.Create(path);
        stream.Write(headerBytes, 0, headerBytes.Length);
        stream.Write(payload, 0, payload.Length);
        stream.Write(compressedIndex, 0, compressedIndex.Length);
        return path;
    }

    private static Hashtable SingleEntry(string name, long offset, long length, object prefix, long key)
    {
        // RenPy stores each entry as a list of (offset, length, prefix) segments, with the
        // offset and length XORed by the archive key from version 3 onwards.
        object[] segment = { offset ^ key, length ^ key, prefix };
        ArrayList segments = new ArrayList { segment };
        return new Hashtable { { name, segments } };
    }

    [Fact]
    public void LoadArchive_IndexPrefixStoredAsBytes_PrependsPrefixToContent()
    {
        using TempWorkspace workspace = new TempWorkspace();
        byte[] payload = Encoding.UTF8.GetBytes("BODY");
        byte[] prefix = Encoding.UTF8.GetBytes("PRE");
        string path = WriteRpa3(workspace, "bytes-prefix.rpa", payload,
            (dataOffset, key) => SingleEntry("a.txt", dataOffset, payload.Length + prefix.Length, prefix, key));
        RpaParser parser = new RpaParser();

        parser.LoadArchive(path);

        Encoding.UTF8.GetString(parser.ExtractData("a.txt")).ShouldBe("PREBODY");
    }

    [Fact]
    public void LoadArchive_IndexPrefixStoredAsString_PrependsPrefixToContent()
    {
        using TempWorkspace workspace = new TempWorkspace();
        byte[] payload = Encoding.UTF8.GetBytes("BODY");
        string path = WriteRpa3(workspace, "string-prefix.rpa", payload,
            (dataOffset, key) => SingleEntry("a.txt", dataOffset, payload.Length + 3, "PRE", key));
        RpaParser parser = new RpaParser();

        parser.LoadArchive(path);

        Encoding.UTF8.GetString(parser.ExtractData("a.txt")).ShouldBe("PREBODY");
    }

    [Fact]
    public void LoadArchive_IndexSegmentWithoutPrefix_ReturnsContentUnchanged()
    {
        using TempWorkspace workspace = new TempWorkspace();
        byte[] payload = Encoding.UTF8.GetBytes("BODY");
        string path = WriteRpa3(workspace, "no-prefix.rpa", payload, (dataOffset, key) =>
        {
            // A two element segment: offset and length only.
            object[] segment = { dataOffset ^ key, (long) payload.Length ^ key };
            return new Hashtable { { "a.txt", new ArrayList { segment } } };
        });
        RpaParser parser = new RpaParser();

        parser.LoadArchive(path);

        Encoding.UTF8.GetString(parser.ExtractData("a.txt")).ShouldBe("BODY");
    }

    [Fact]
    public void LoadArchive_IndexContainsNullEntry_SkipsItAndKeepsTheRest()
    {
        using TempWorkspace workspace = new TempWorkspace();
        byte[] payload = Encoding.UTF8.GetBytes("BODY");
        string path = WriteRpa3(workspace, "null-entry.rpa", payload, (dataOffset, key) =>
        {
            Hashtable index = SingleEntry("a.txt", dataOffset, payload.Length, string.Empty, key);
            index.Add("discarded.txt", null);
            return index;
        });
        RpaParser parser = new RpaParser();

        parser.LoadArchive(path);

        parser.Index.ShouldContainKey("a.txt");
        parser.Index.ShouldNotContainKey("discarded.txt");
    }

    [Fact]
    public void LoadArchive_EntrySplitAcrossSegments_ConcatenatesInOrder()
    {
        using TempWorkspace workspace = new TempWorkspace();
        byte[] payload = Encoding.UTF8.GetBytes("HELLOWORLD");
        string path = WriteRpa3(workspace, "segmented.rpa", payload, (dataOffset, key) =>
        {
            object[] first = { (long) dataOffset ^ key, 5L ^ key, string.Empty };
            object[] second = { (long) (dataOffset + 5) ^ key, 5L ^ key, string.Empty };
            return new Hashtable { { "a.txt", new ArrayList { first, second } } };
        });
        RpaParser parser = new RpaParser();

        parser.LoadArchive(path);

        Encoding.UTF8.GetString(parser.ExtractData("a.txt")).ShouldBe("HELLOWORLD");
        parser.Index["a.txt"].Tuples.Count.ShouldBe(2);
        parser.Index["a.txt"].Length.ShouldBe(10);
    }

    [Fact]
    public void LoadArchive_HeaderOffsetBeyondEndOfFile_Throws()
    {
        using TempWorkspace workspace = new TempWorkspace();
        byte[] payload = Encoding.UTF8.GetBytes("BODY");
        // An offset past the end leaves nothing to read, so the index cannot be decompressed.
        string path = WriteRpa3(workspace, "bad-offset.rpa", payload,
            (dataOffset, key) => SingleEntry("a.txt", dataOffset, payload.Length, string.Empty, key),
            headerOffsetOverride: 100_000);
        RpaParser parser = new RpaParser();

        Should.Throw<Exception>(() => parser.LoadArchive(path));
    }

    [Fact]
    public void LoadArchive_IndexIsNotValidZlib_Throws()
    {
        using TempWorkspace workspace = new TempWorkspace();
        string header = "RPA-3.0 " + 34.ToString("x").PadLeft(16, '0') + " " + 0.ToString("x").PadLeft(8, '0') + "\n";
        string path = workspace.Path_("garbage-index.rpa");
        using (FileStream stream = File.Create(path))
        {
            byte[] headerBytes = Encoding.UTF8.GetBytes(header);
            stream.Write(headerBytes, 0, headerBytes.Length);
            stream.Write(new byte[] { 1, 2, 3, 4, 5, 6 }, 0, 6);
        }
        RpaParser parser = new RpaParser();

        Should.Throw<Exception>(() => parser.LoadArchive(path));
    }
}
