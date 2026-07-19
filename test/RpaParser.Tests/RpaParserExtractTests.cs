using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using RpaParser;
using Shouldly;

namespace RpaParser.Tests;

public class RpaParserExtractTests
{
    private static Dictionary<string, byte[]> SampleEntries() => new()
    {
        ["readme.txt"] = Encoding.UTF8.GetBytes("readme contents"),
        ["images/nested/pic.bin"] = new byte[] { 9, 8, 7, 6 }
    };

    [Fact]
    public void ExtractData_FileStoredInArchive_ReturnsOriginalBytes()
    {
        using var workspace = new TempWorkspace();
        var parser = workspace.LoadArchive(ArchiveFormat.Rpa3, SampleEntries());

        var result = parser.ExtractData("images/nested/pic.bin");

        result.ShouldBe(new byte[] { 9, 8, 7, 6 });
    }

    [Fact]
    public void ExtractData_FileNotInIndex_Throws()
    {
        using var workspace = new TempWorkspace();
        var parser = workspace.LoadArchive(ArchiveFormat.Rpa3, SampleEntries());

        var ex = Should.Throw<Exception>(() => parser.ExtractData("nope.txt"));

        ex.Message.ShouldContain("does not exist in RenPy Archive");
    }

    [Fact]
    public void ExtractData_EntryNotYetInArchive_ReadsFromDisk()
    {
        using var workspace = new TempWorkspace();
        var onDisk = workspace.WriteFile("pending/new.txt", "not yet archived");
        var parser = new Archive();
        parser.Index.Add("new.txt", new ArchiveEntry
        {
            InArchive = false,
            FullPath = onDisk.Replace('\\', '/'),
            TreePath = "new.txt",
            Length = new FileInfo(onDisk).Length
        });

        var result = parser.ExtractData("new.txt");

        Encoding.UTF8.GetString(result).ShouldBe("not yet archived");
    }

    [Fact]
    public void ExtractData_MultiSegmentEntry_ConcatenatesSegments()
    {
        using var workspace = new TempWorkspace();
        var payload = Encoding.UTF8.GetBytes("segment payload");
        var parser = workspace.LoadArchive(
            ArchiveFormat.Rpa3, new Dictionary<string, byte[]> { ["a.txt"] = payload });

        var result = parser.ExtractData("a.txt");

        result.ShouldBe(payload);
        parser.Index["a.txt"].Segments.Count.ShouldBeGreaterThan(0);
    }

    [Fact]
    public void Extract_ExportPathGiven_WritesFileUnderThatPath()
    {
        using var workspace = new TempWorkspace();
        var parser = workspace.LoadArchive(ArchiveFormat.Rpa3, SampleEntries());
        var exportDir = workspace.Path_("export");
        Directory.CreateDirectory(exportDir);

        var written = parser.Extract("readme.txt", exportDir);

        File.Exists(written).ShouldBeTrue();
        File.ReadAllText(written).ShouldBe("readme contents");
        written.ShouldStartWith(exportDir);
    }

    [Fact]
    public void Extract_NestedEntry_CreatesIntermediateDirectories()
    {
        using var workspace = new TempWorkspace();
        var parser = workspace.LoadArchive(ArchiveFormat.Rpa3, SampleEntries());
        var exportDir = workspace.Path_("export-nested");
        Directory.CreateDirectory(exportDir);

        var written = parser.Extract("images/nested/pic.bin", exportDir);

        File.Exists(written).ShouldBeTrue();
        File.ReadAllBytes(written).ShouldBe(new byte[] { 9, 8, 7, 6 });
    }

    [Fact]
    public void Extract_EmptyExportPath_WritesNextToArchive()
    {
        using var workspace = new TempWorkspace();
        var parser = workspace.LoadArchive(ArchiveFormat.Rpa3, SampleEntries());

        var written = parser.Extract("readme.txt", string.Empty);

        File.Exists(written).ShouldBeTrue();
        Path.GetDirectoryName(written).ShouldBe(parser.ArchiveInfo.DirectoryName);
    }

    [Fact]
    public void Extract_WhitespaceExportPath_WritesNextToArchive()
    {
        using var workspace = new TempWorkspace();
        var parser = workspace.LoadArchive(ArchiveFormat.Rpa3, SampleEntries());

        var written = parser.Extract("readme.txt", "   ");

        File.Exists(written).ShouldBeTrue();
        Path.GetDirectoryName(written).ShouldBe(parser.ArchiveInfo.DirectoryName);
    }

    [Fact]
    public void Extract_ExportPathDoesNotExist_Throws()
    {
        using var workspace = new TempWorkspace();
        var parser = workspace.LoadArchive(ArchiveFormat.Rpa3, SampleEntries());

        var ex = Should.Throw<Exception>(
            () => parser.Extract("readme.txt", workspace.Path_("no-such-dir")));

        ex.Message.ShouldContain("export path does not exist");
    }

    [Fact]
    public void Extract_FileNotInIndex_Throws()
    {
        using var workspace = new TempWorkspace();
        var parser = workspace.LoadArchive(ArchiveFormat.Rpa3, SampleEntries());

        Should.Throw<Exception>(() => parser.Extract("missing.txt", string.Empty));
    }
}
