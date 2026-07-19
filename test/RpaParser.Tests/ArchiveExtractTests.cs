using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using RpaParser;
using Shouldly;

namespace RpaParser.Tests;

public class ArchiveExtractTests
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
        var archive = workspace.LoadArchive(ArchiveFormat.Rpa3, SampleEntries());

        var result = archive.ExtractData("images/nested/pic.bin");

        result.ShouldBe(new byte[] { 9, 8, 7, 6 });
    }

    [Fact]
    public void ExtractData_FileNotInIndex_Throws()
    {
        using var workspace = new TempWorkspace();
        var archive = workspace.LoadArchive(ArchiveFormat.Rpa3, SampleEntries());

        var ex = Should.Throw<Exception>(() => archive.ExtractData("nope.txt"));

        ex.Message.ShouldContain("does not exist in RenPy Archive");
    }

    [Fact]
    public void ExtractData_EntryNotYetInArchive_ReadsFromDisk()
    {
        using var workspace = new TempWorkspace();
        var onDisk = workspace.WriteFile("pending/new.txt", "not yet archived");
        var archive = new Archive();
        archive.Index.Add("new.txt", ArchiveEntry.FromFilename(onDisk, workspace.Path_("pending")));

        var result = archive.ExtractData("new.txt");

        Encoding.UTF8.GetString(result).ShouldBe("not yet archived");
    }

    [Fact]
    public void ExtractData_MultiSegmentEntry_ConcatenatesSegments()
    {
        using var workspace = new TempWorkspace();
        var payload = Encoding.UTF8.GetBytes("segment payload");
        var archive = workspace.LoadArchive(
            ArchiveFormat.Rpa3, new Dictionary<string, byte[]> { ["a.txt"] = payload });

        var result = archive.ExtractData("a.txt");

        result.ShouldBe(payload);
        archive.Index["a.txt"].Segments.Count.ShouldBeGreaterThan(0);
    }

    [Fact]
    public void Extract_ExportPathGiven_WritesFileUnderThatPath()
    {
        using var workspace = new TempWorkspace();
        var archive = workspace.LoadArchive(ArchiveFormat.Rpa3, SampleEntries());
        var exportDir = workspace.Path_("export");
        Directory.CreateDirectory(exportDir);

        var written = archive.Extract("readme.txt", exportDir);

        File.Exists(written).ShouldBeTrue();
        File.ReadAllText(written).ShouldBe("readme contents");
        written.ShouldStartWith(exportDir);
    }

    [Fact]
    public void Extract_NestedEntry_CreatesIntermediateDirectories()
    {
        using var workspace = new TempWorkspace();
        var archive = workspace.LoadArchive(ArchiveFormat.Rpa3, SampleEntries());
        var exportDir = workspace.Path_("export-nested");
        Directory.CreateDirectory(exportDir);

        var written = archive.Extract("images/nested/pic.bin", exportDir);

        File.Exists(written).ShouldBeTrue();
        File.ReadAllBytes(written).ShouldBe(new byte[] { 9, 8, 7, 6 });
    }

    [Fact]
    public void Extract_EmptyExportPath_WritesNextToArchive()
    {
        using var workspace = new TempWorkspace();
        var archive = workspace.LoadArchive(ArchiveFormat.Rpa3, SampleEntries());

        var written = archive.Extract("readme.txt", string.Empty);

        File.Exists(written).ShouldBeTrue();
        Path.GetDirectoryName(written).ShouldBe(archive.Files.Archive.DirectoryName);
    }

    [Fact]
    public void Extract_WhitespaceExportPath_WritesNextToArchive()
    {
        using var workspace = new TempWorkspace();
        var archive = workspace.LoadArchive(ArchiveFormat.Rpa3, SampleEntries());

        var written = archive.Extract("readme.txt", "   ");

        File.Exists(written).ShouldBeTrue();
        Path.GetDirectoryName(written).ShouldBe(archive.Files.Archive.DirectoryName);
    }

    [Fact]
    public void Extract_ExportPathDoesNotExist_Throws()
    {
        using var workspace = new TempWorkspace();
        var archive = workspace.LoadArchive(ArchiveFormat.Rpa3, SampleEntries());

        var ex = Should.Throw<Exception>(
            () => archive.Extract("readme.txt", workspace.Path_("no-such-dir")));

        ex.Message.ShouldContain("export path does not exist");
    }

    [Fact]
    public void Extract_FileNotInIndex_Throws()
    {
        using var workspace = new TempWorkspace();
        var archive = workspace.LoadArchive(ArchiveFormat.Rpa3, SampleEntries());

        Should.Throw<Exception>(() => archive.Extract("missing.txt", string.Empty));
    }
}
