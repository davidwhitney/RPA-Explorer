using System;
using System.Linq;
using System.Collections.Generic;
using System.IO;
using System.Text;
using RpaParser;
using Shouldly;

namespace RpaParser.Tests;

public class RpaParserVersionTests
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
        var parser = new Parser();

        parser.Format.ShouldBeNull();
        parser.Index.ShouldBeEmpty();
        parser.OptionsConfirmed.ShouldBeFalse();
        parser.Padding.ShouldBe(0);
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

        var parser = workspace.LoadArchive(format, entries);

        parser.Format.ShouldBeSameAs(format);
        parser.Index.Count.ShouldBe(2);
        parser.Index.Keys.ShouldContain("script.rpy");
        parser.Index.Keys.ShouldContain("images/logo.bin");
        parser.ExtractData("images/logo.bin").ShouldBe(new byte[] { 1, 2, 3, 4, 5 });
    }

    [Fact]
    public void LoadArchive_Version1Archive_ExposesIndexFileInfo()
    {
        using var workspace = new TempWorkspace();
        Dictionary<string, byte[]> entries = new() { ["a.txt"] = Encoding.UTF8.GetBytes("a") };

        // version 1 keeps its index in a separate .rpi file
        var parser = workspace.LoadArchive(ArchiveFormat.Rpa1, entries);

        parser.IndexInfo.ShouldNotBeNull();
        parser.IndexInfo.Exists.ShouldBeTrue();
        parser.ArchiveInfo.ShouldNotBeNull();
    }

    [Theory]
    [MemberData(nameof(EmbeddedIndexFormats))]
    public void LoadArchive_VersionWithEmbeddedIndex_HasNoSeparateIndexFile(ArchiveFormat format)
    {
        using var workspace = new TempWorkspace();
        Dictionary<string, byte[]> entries = new() { ["a.txt"] = Encoding.UTF8.GetBytes("a") };

        var parser = workspace.LoadArchive(format, entries);

        parser.IndexInfo.ShouldBeNull();
    }

    [Fact]
    public void LoadArchive_Version1IndexPathGiven_ResolvesArchiveFromIndex()
    {
        using var workspace = new TempWorkspace();
        Dictionary<string, byte[]> entries = new() { ["a.txt"] = Encoding.UTF8.GetBytes("a") };
        var archivePath = workspace.CreateArchive(ArchiveFormat.Rpa1, entries);
        var indexPath = Path.ChangeExtension(archivePath, ".rpi");
        var parser = new Parser();

        // pointing at the .rpi must locate the matching .rpa
        parser.LoadArchive(indexPath);

        parser.Format.ShouldBeSameAs(ArchiveFormat.Rpa1);
        parser.Index.Keys.ShouldContain("a.txt");
    }

    [Fact]
    public void LoadArchive_Rpa3WithObfuscationKey_DeobfuscatesOffsets()
    {
        using var workspace = new TempWorkspace();
        var payload = Encoding.UTF8.GetBytes("obfuscated payload");
        Dictionary<string, byte[]> entries = new() { ["a.txt"] = payload };
        var archivePath = workspace.CreateArchive(
            ArchiveFormat.Rpa3, entries, "keyed.rpa", obfuscationKey: 0x1234ABCD);
        var parser = new Parser();

        parser.LoadArchive(archivePath);

        parser.ObfuscationKey.ShouldBe(0x1234ABCD);
        parser.ExtractData("a.txt").ShouldBe(payload);
    }

    [Fact]
    public void LoadArchive_ArchiveWithPadding_StillReadsContents()
    {
        using var workspace = new TempWorkspace();
        var payload = Encoding.UTF8.GetBytes("padded payload");
        Dictionary<string, byte[]> entries = new() { ["a.txt"] = payload };
        var archivePath = workspace.CreateArchive(
            ArchiveFormat.Rpa3, entries, "padded.rpa", padding: 32);
        var parser = new Parser();

        parser.LoadArchive(archivePath);

        parser.ExtractData("a.txt").ShouldBe(payload);
    }

    [Fact]
    public void LoadArchive_FileDoesNotExist_Throws()
    {
        using var workspace = new TempWorkspace();
        var parser = new Parser();

        var ex = Should.Throw<Exception>(() => parser.LoadArchive(workspace.Path_("missing.rpa")));
        ex.Message.ShouldContain("does not exist");
    }

    [Fact]
    public void LoadArchive_EmptyPath_Throws()
    {
        var parser = new Parser();

        Should.Throw<Exception>(() => parser.LoadArchive(string.Empty));
    }

    [Fact]
    public void LoadArchive_FileIsNotAnArchive_Throws()
    {
        using var workspace = new TempWorkspace();
        var path = workspace.WriteFile("not-an-archive.rpa", "just some text, no RPA header");
        var parser = new Parser();

        var ex = Should.Throw<Exception>(() => parser.LoadArchive(path));
        ex.Message.ShouldContain("not valid RenPy Archive");
    }

    [Fact]
    public void LoadArchive_Version1ArchiveMissingIndexFile_Throws()
    {
        using var workspace = new TempWorkspace();
        Dictionary<string, byte[]> entries = new() { ["a.txt"] = Encoding.UTF8.GetBytes("a") };
        var archivePath = workspace.CreateArchive(ArchiveFormat.Rpa1, entries);
        File.Delete(Path.ChangeExtension(archivePath, ".rpi"));
        var parser = new Parser();

        // without the .rpi the version can no longer be recognised
        Should.Throw<Exception>(() => parser.LoadArchive(archivePath));
    }
}
