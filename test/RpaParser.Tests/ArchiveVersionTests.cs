using System;
using System.Linq;
using System.Collections.Generic;
using System.IO;
using System.Text;
using RpaParser;
using Shouldly;

namespace RpaParser.Tests;

public class ArchiveVersionTests
{
    private static TheoryData<ArchiveFormat> Formats(params ArchiveFormat[] formats)
    {
        var data = new TheoryData<ArchiveFormat>();
        foreach (var format in formats)
        {
            data.Add(format);
        }
        return data;
    }

    public static TheoryData<ArchiveFormat> AllFormats => Formats(ArchiveFormat.All.ToArray());

    public static TheoryData<ArchiveFormat> EmbeddedIndexFormats =>
        Formats(ArchiveFormat.All.Where(f => !f.HasSeparateIndexFile).ToArray());

    [Fact]
    public void Format_NewParser_IsNotYetKnown()
    {
        var archive = new Archive();

        archive.Format.ShouldBeNull();
        archive.Index.ShouldBeEmpty();
        archive.OptionsConfirmed.ShouldBeFalse();
        archive.Padding.ShouldBe(0);
    }

    [Theory]
    [MemberData(nameof(AllFormats))]
    public void LoadArchive_ArchiveOfEachVersion_DetectsVersionAndContents(ArchiveFormat format)
    {
        using var workspace = new TempWorkspace();
        Dictionary<string, byte[]> entries = new()
        {
            ["script.rpy"] = Encoding.UTF8.GetBytes("label start:\n    return\n"),
            ["images/logo.bin"] = new byte[] { 1, 2, 3, 4, 5 }
        };

        var archive = workspace.LoadArchive(format, entries);

        archive.Format.ShouldBeSameAs(format);
        archive.Index.Count.ShouldBe(2);
        archive.Index.Keys.ShouldContain("script.rpy");
        archive.Index.Keys.ShouldContain("images/logo.bin");
        archive.ExtractData("images/logo.bin").ShouldBe(new byte[] { 1, 2, 3, 4, 5 });
    }

    [Fact]
    public void LoadArchive_Version1Archive_ExposesIndexFileInfo()
    {
        using var workspace = new TempWorkspace();
        Dictionary<string, byte[]> entries = new() { ["a.txt"] = Encoding.UTF8.GetBytes("a") };

        // version 1 keeps its index in a separate .rpi file
        var archive = workspace.LoadArchive(ArchiveFormat.Rpa1, entries);

        archive.Files.IndexFile.File.ShouldNotBeNull();
        archive.Files.IndexFile.File.Exists.ShouldBeTrue();
        archive.Files.Archive.ShouldNotBeNull();
    }

    [Theory]
    [MemberData(nameof(EmbeddedIndexFormats))]
    public void LoadArchive_VersionWithEmbeddedIndex_HasNoSeparateIndexFile(ArchiveFormat format)
    {
        using var workspace = new TempWorkspace();
        Dictionary<string, byte[]> entries = new() { ["a.txt"] = Encoding.UTF8.GetBytes("a") };

        var archive = workspace.LoadArchive(format, entries);

        archive.Files.IndexFile.File.ShouldBeNull();
    }

    [Fact]
    public void LoadArchive_Version1IndexPathGiven_ResolvesArchiveFromIndex()
    {
        using var workspace = new TempWorkspace();
        Dictionary<string, byte[]> entries = new() { ["a.txt"] = Encoding.UTF8.GetBytes("a") };
        var archivePath = workspace.CreateArchive(ArchiveFormat.Rpa1, entries);
        var indexPath = Path.ChangeExtension(archivePath, ".rpi");
        // pointing at the .rpi must locate the matching .rpa
        var archive = Archive.Load(indexPath);

        archive.Format.ShouldBeSameAs(ArchiveFormat.Rpa1);
        archive.Index.Keys.ShouldContain("a.txt");
    }

    [Fact]
    public void LoadArchive_Rpa3WithObfuscationKey_DeobfuscatesOffsets()
    {
        using var workspace = new TempWorkspace();
        var payload = Encoding.UTF8.GetBytes("obfuscated payload");
        Dictionary<string, byte[]> entries = new() { ["a.txt"] = payload };
        var archivePath = workspace.CreateArchive(
            ArchiveFormat.Rpa3, entries, "keyed.rpa", obfuscationKey: 0x1234ABCD);
        var archive = Archive.Load(archivePath);

        archive.ObfuscationKey.ShouldBe(0x1234ABCD);
        archive.ExtractData("a.txt").ShouldBe(payload);
    }

    [Fact]
    public void LoadArchive_ArchiveWithPadding_StillReadsContents()
    {
        using var workspace = new TempWorkspace();
        var payload = Encoding.UTF8.GetBytes("padded payload");
        Dictionary<string, byte[]> entries = new() { ["a.txt"] = payload };
        var archivePath = workspace.CreateArchive(
            ArchiveFormat.Rpa3, entries, "padded.rpa", padding: 32);
        var archive = Archive.Load(archivePath);

        archive.ExtractData("a.txt").ShouldBe(payload);
    }

    [Fact]
    public void LoadArchive_FileDoesNotExist_Throws()
    {
        using var workspace = new TempWorkspace();
        var archive = new Archive();

        var ex = Should.Throw<Exception>(() => Archive.Load(workspace.Path_("missing.rpa")));
        ex.Message.ShouldContain("does not exist");
    }

    [Fact]
    public void LoadArchive_EmptyPath_Throws()
    {

        Should.Throw<Exception>(() => Archive.Load(string.Empty));
    }

    [Fact]
    public void LoadArchive_FileIsNotAnArchive_Throws()
    {
        using var workspace = new TempWorkspace();
        var path = workspace.WriteFile("not-an-archive.rpa", "just some text, no RPA header");
        var archive = new Archive();

        var ex = Should.Throw<Exception>(() => Archive.Load(path));
        ex.Message.ShouldContain("not valid RenPy Archive");
    }

    [Fact]
    public void LoadArchive_Version1ArchiveMissingIndexFile_Throws()
    {
        using var workspace = new TempWorkspace();
        Dictionary<string, byte[]> entries = new() { ["a.txt"] = Encoding.UTF8.GetBytes("a") };
        var archivePath = workspace.CreateArchive(ArchiveFormat.Rpa1, entries);
        File.Delete(Path.ChangeExtension(archivePath, ".rpi"));
        var archive = new Archive();

        // without the .rpi the version can no longer be recognised
        Should.Throw<Exception>(() => Archive.Load(archivePath));
    }
}
