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
        using TempWorkspace workspace = new TempWorkspace();
        Parser parser = workspace.LoadArchive(Parser.Version.Rpa3, SampleEntries());

        byte[] result = parser.ExtractData("images/nested/pic.bin");

        result.ShouldBe(new byte[] { 9, 8, 7, 6 });
    }

    [Fact]
    public void ExtractData_FileNotInIndex_Throws()
    {
        using TempWorkspace workspace = new TempWorkspace();
        Parser parser = workspace.LoadArchive(Parser.Version.Rpa3, SampleEntries());

        Exception ex = Should.Throw<Exception>(() => parser.ExtractData("nope.txt"));

        ex.Message.ShouldContain("does not exist in RenPy Archive");
    }

    [Fact]
    public void ExtractData_EntryNotYetInArchive_ReadsFromDisk()
    {
        using TempWorkspace workspace = new TempWorkspace();
        string onDisk = workspace.WriteFile("pending/new.txt", "not yet archived");
        Parser parser = new Parser();
        parser.Index.Add("new.txt", new Parser.ArchiveIndex
        {
            InArchive = false,
            FullPath = onDisk.Replace('\\', '/'),
            TreePath = "new.txt",
            Length = new FileInfo(onDisk).Length
        });

        byte[] result = parser.ExtractData("new.txt");

        Encoding.UTF8.GetString(result).ShouldBe("not yet archived");
    }

    [Fact]
    public void ExtractData_MultiSegmentEntry_ConcatenatesSegments()
    {
        using TempWorkspace workspace = new TempWorkspace();
        byte[] payload = Encoding.UTF8.GetBytes("segment payload");
        Parser parser = workspace.LoadArchive(
            Parser.Version.Rpa3, new Dictionary<string, byte[]> { ["a.txt"] = payload });

        byte[] result = parser.ExtractData("a.txt");

        result.ShouldBe(payload);
        parser.Index["a.txt"].Tuples.Count.ShouldBeGreaterThan(0);
    }

    [Fact]
    public void Extract_ExportPathGiven_WritesFileUnderThatPath()
    {
        using TempWorkspace workspace = new TempWorkspace();
        Parser parser = workspace.LoadArchive(Parser.Version.Rpa3, SampleEntries());
        string exportDir = workspace.Path_("export");
        Directory.CreateDirectory(exportDir);

        string written = parser.Extract("readme.txt", exportDir);

        File.Exists(written).ShouldBeTrue();
        File.ReadAllText(written).ShouldBe("readme contents");
        written.ShouldStartWith(exportDir);
    }

    [Fact]
    public void Extract_NestedEntry_CreatesIntermediateDirectories()
    {
        using TempWorkspace workspace = new TempWorkspace();
        Parser parser = workspace.LoadArchive(Parser.Version.Rpa3, SampleEntries());
        string exportDir = workspace.Path_("export-nested");
        Directory.CreateDirectory(exportDir);

        string written = parser.Extract("images/nested/pic.bin", exportDir);

        File.Exists(written).ShouldBeTrue();
        File.ReadAllBytes(written).ShouldBe(new byte[] { 9, 8, 7, 6 });
    }

    [Fact]
    public void Extract_EmptyExportPath_WritesNextToArchive()
    {
        using TempWorkspace workspace = new TempWorkspace();
        Parser parser = workspace.LoadArchive(Parser.Version.Rpa3, SampleEntries());

        string written = parser.Extract("readme.txt", string.Empty);

        File.Exists(written).ShouldBeTrue();
        Path.GetDirectoryName(written).ShouldBe(parser.ArchiveInfo.DirectoryName);
    }

    [Fact]
    public void Extract_WhitespaceExportPath_WritesNextToArchive()
    {
        using TempWorkspace workspace = new TempWorkspace();
        Parser parser = workspace.LoadArchive(Parser.Version.Rpa3, SampleEntries());

        string written = parser.Extract("readme.txt", "   ");

        File.Exists(written).ShouldBeTrue();
        Path.GetDirectoryName(written).ShouldBe(parser.ArchiveInfo.DirectoryName);
    }

    [Fact]
    public void Extract_ExportPathDoesNotExist_Throws()
    {
        using TempWorkspace workspace = new TempWorkspace();
        Parser parser = workspace.LoadArchive(Parser.Version.Rpa3, SampleEntries());

        Exception ex = Should.Throw<Exception>(
            () => parser.Extract("readme.txt", workspace.Path_("no-such-dir")));

        ex.Message.ShouldContain("export path does not exist");
    }

    [Fact]
    public void Extract_FileNotInIndex_Throws()
    {
        using TempWorkspace workspace = new TempWorkspace();
        Parser parser = workspace.LoadArchive(Parser.Version.Rpa3, SampleEntries());

        Should.Throw<Exception>(() => parser.Extract("missing.txt", string.Empty));
    }
}
