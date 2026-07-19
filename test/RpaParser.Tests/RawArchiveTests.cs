using System.Collections;
using System.Text;
using Razorvine.Pickle;
using Shouldly;

namespace RpaParser.Tests;

/// <summary>
/// Archives assembled byte by byte, covering index shapes that real RenPy archives use but
/// that this archive's own writer never emits - notably byte-array prefixes and null entries.
/// </summary>
public class RawArchiveTests
{
    private const int Rpa3HeaderLength = 34;

    /// <summary>
    /// Writes a valid RPA-3 archive whose index is supplied verbatim, so a test can control
    /// the exact tuple shape stored for each entry.
    /// </summary>
    private static string WriteRpa3(TempWorkspace workspace, string name, byte[] payload,
        Func<int, long, Hashtable> buildIndex, long key = 0xDEADBEEF, long? headerOffsetOverride = null)
    {
        var index = buildIndex(Rpa3HeaderLength, key);

        byte[] pickled;
        using (var pickler = new Pickler())
        {
            pickled = pickler.dumps(index);
        }
        var compressedIndex = Zlib.CompressBuffer(pickled);

        var indexOffset = Rpa3HeaderLength + payload.Length;
        var header = "RPA-3.0 " + (headerOffsetOverride ?? indexOffset).ToString("x").PadLeft(16, '0')
                                + " " + key.ToString("x").PadLeft(8, '0') + "\n";
        var headerBytes = Encoding.UTF8.GetBytes(header);
        headerBytes.Length.ShouldBe(Rpa3HeaderLength);

        var path = workspace.Path_(name);
        using var stream = File.Create(path);
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
        var segments = new ArrayList { segment };
        return new Hashtable { { name, segments } };
    }

    [Fact]
    public void Constructor_IndexPrefixStoredAsBytes_PrependsPrefixToContent()
    {
        using var workspace = new TempWorkspace();
        var payload = Encoding.UTF8.GetBytes("BODY");
        var prefix = Encoding.UTF8.GetBytes("PRE");
        var path = WriteRpa3(workspace, "bytes-prefix.rpa", payload,
            (dataOffset, key) => SingleEntry("a.txt", dataOffset, payload.Length + prefix.Length, prefix, key));
        var archive = new Archive(path);

        Encoding.UTF8.GetString(archive.Read("a.txt")).ShouldBe("PREBODY");
    }

    [Fact]
    public void Constructor_IndexPrefixStoredAsString_PrependsPrefixToContent()
    {
        using var workspace = new TempWorkspace();
        var payload = Encoding.UTF8.GetBytes("BODY");
        var path = WriteRpa3(workspace, "string-prefix.rpa", payload,
            (dataOffset, key) => SingleEntry("a.txt", dataOffset, payload.Length + 3, "PRE", key));
        var archive = new Archive(path);

        Encoding.UTF8.GetString(archive.Read("a.txt")).ShouldBe("PREBODY");
    }

    [Fact]
    public void Constructor_IndexSegmentWithoutPrefix_ReturnsContentUnchanged()
    {
        using var workspace = new TempWorkspace();
        var payload = Encoding.UTF8.GetBytes("BODY");
        var path = WriteRpa3(workspace, "no-prefix.rpa", payload, (dataOffset, key) =>
        {
            // A two element segment: offset and length only.
            object[] segment = { dataOffset ^ key, (long) payload.Length ^ key };
            return new Hashtable { { "a.txt", new ArrayList { segment } } };
        });
        var archive = new Archive(path);

        Encoding.UTF8.GetString(archive.Read("a.txt")).ShouldBe("BODY");
    }

    [Fact]
    public void Constructor_IndexContainsNullEntry_SkipsItAndKeepsTheRest()
    {
        using var workspace = new TempWorkspace();
        var payload = Encoding.UTF8.GetBytes("BODY");
        var path = WriteRpa3(workspace, "null-entry.rpa", payload, (dataOffset, key) =>
        {
            var index = SingleEntry("a.txt", dataOffset, payload.Length, string.Empty, key);
            index.Add("discarded.txt", null);
            return index;
        });
        var archive = new Archive(path);

        archive.Index.ShouldContainKey("a.txt");
        archive.Index.ShouldNotContainKey("discarded.txt");
    }

    [Fact]
    public void Constructor_EntrySplitAcrossSegments_ConcatenatesInOrder()
    {
        using var workspace = new TempWorkspace();
        var payload = Encoding.UTF8.GetBytes("HELLOWORLD");
        var path = WriteRpa3(workspace, "segmented.rpa", payload, (dataOffset, key) =>
        {
            object[] first = { (long) dataOffset ^ key, 5L ^ key, string.Empty };
            object[] second = { (long) (dataOffset + 5) ^ key, 5L ^ key, string.Empty };
            return new Hashtable { { "a.txt", new ArrayList { first, second } } };
        });
        var archive = new Archive(path);

        Encoding.UTF8.GetString(archive.Read("a.txt")).ShouldBe("HELLOWORLD");
        archive.Index["a.txt"].Segments.Count.ShouldBe(2);
        archive.Index["a.txt"].Length.ShouldBe(10);
    }

    [Fact]
    public void Constructor_HeaderOffsetBeyondEndOfFile_Throws()
    {
        using var workspace = new TempWorkspace();
        var payload = Encoding.UTF8.GetBytes("BODY");
        // An offset past the end leaves nothing to read, so the index cannot be decompressed.
        var path = WriteRpa3(workspace, "bad-offset.rpa", payload,
            (dataOffset, key) => SingleEntry("a.txt", dataOffset, payload.Length, string.Empty, key),
            headerOffsetOverride: 100_000);

        Should.Throw<Exception>(() => new Archive(path));
    }

    [Fact]
    public void Constructor_IndexIsNotValidZlib_Throws()
    {
        using var workspace = new TempWorkspace();
        var header = "RPA-3.0 " + 34.ToString("x").PadLeft(16, '0') + " " + 0.ToString("x").PadLeft(8, '0') + "\n";
        var path = workspace.Path_("garbage-index.rpa");
        using (var stream = File.Create(path))
        {
            var headerBytes = Encoding.UTF8.GetBytes(header);
            stream.Write(headerBytes, 0, headerBytes.Length);
            stream.Write(new byte[] { 1, 2, 3, 4, 5, 6 }, 0, 6);
        }

        Should.Throw<Exception>(() => new Archive(path));
    }
}
