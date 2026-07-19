using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using RpaParser;
using Shouldly;

namespace RpaParser.Tests;

public class RpaParserVersionTests
{
    [Theory]
    [InlineData(Parser.Version.Rpa1)]
    [InlineData(Parser.Version.Rpa2)]
    [InlineData(Parser.Version.Rpa3)]
    [InlineData(Parser.Version.Rpa32)]
    public void CheckSupportedVersion_SupportedVersion_ReturnsSameVersion(double version)
    {
        Parser parser = new Parser();

        double result = parser.CheckSupportedVersion(version);

        result.ShouldBe(version);
    }

    [Theory]
    [InlineData(Parser.Version.Unknown)]
    [InlineData(0)]
    [InlineData(4)]
    [InlineData(2.5)]
    public void CheckSupportedVersion_UnsupportedVersion_Throws(double version)
    {
        Parser parser = new Parser();

        Exception ex = Should.Throw<Exception>(() => parser.CheckSupportedVersion(version));
        ex.Message.ShouldContain("not supported");
    }

    [Fact]
    public void CheckVersion_VersionsMatch_ReturnsTrue()
    {
        Parser parser = new Parser();

        bool result = parser.CheckVersion(Parser.Version.Rpa3, Parser.Version.Rpa3);

        result.ShouldBeTrue();
    }

    [Fact]
    public void CheckVersion_VersionsDiffer_ReturnsFalse()
    {
        Parser parser = new Parser();

        bool result = parser.CheckVersion(Parser.Version.Rpa3, Parser.Version.Rpa2);

        result.ShouldBeFalse();
    }

    [Fact]
    public void ArchiveVersion_NewParser_DefaultsToUnknown()
    {
        Parser parser = new Parser();

        parser.ArchiveVersion.ShouldBe(Parser.Version.Unknown);
        parser.Index.ShouldBeEmpty();
        parser.OptionsConfirmed.ShouldBeFalse();
        parser.Padding.ShouldBe(0);
    }

    [Theory]
    [InlineData(Parser.Version.Rpa1)]
    [InlineData(Parser.Version.Rpa2)]
    [InlineData(Parser.Version.Rpa3)]
    [InlineData(Parser.Version.Rpa32)]
    public void LoadArchive_ArchiveOfEachVersion_DetectsVersionAndContents(double version)
    {
        using TempWorkspace workspace = new TempWorkspace();
        Dictionary<string, byte[]> entries = new()
        {
            ["script.rpy"] = Encoding.UTF8.GetBytes("label start:\n    return\n"),
            ["images/logo.bin"] = new byte[] { 1, 2, 3, 4, 5 }
        };

        Parser parser = workspace.LoadArchive(version, entries);

        parser.ArchiveVersion.ShouldBe(version);
        parser.Index.Count.ShouldBe(2);
        parser.Index.Keys.ShouldContain("script.rpy");
        parser.Index.Keys.ShouldContain("images/logo.bin");
        parser.ExtractData("images/logo.bin").ShouldBe(new byte[] { 1, 2, 3, 4, 5 });
    }

    [Fact]
    public void LoadArchive_Version1Archive_ExposesIndexFileInfo()
    {
        using TempWorkspace workspace = new TempWorkspace();
        Dictionary<string, byte[]> entries = new() { ["a.txt"] = Encoding.UTF8.GetBytes("a") };

        // version 1 keeps its index in a separate .rpi file
        Parser parser = workspace.LoadArchive(Parser.Version.Rpa1, entries);

        parser.IndexInfo.ShouldNotBeNull();
        parser.IndexInfo.Exists.ShouldBeTrue();
        parser.ArchiveInfo.ShouldNotBeNull();
    }

    [Theory]
    [InlineData(Parser.Version.Rpa2)]
    [InlineData(Parser.Version.Rpa3)]
    public void LoadArchive_VersionWithEmbeddedIndex_HasNoSeparateIndexFile(double version)
    {
        using TempWorkspace workspace = new TempWorkspace();
        Dictionary<string, byte[]> entries = new() { ["a.txt"] = Encoding.UTF8.GetBytes("a") };

        Parser parser = workspace.LoadArchive(version, entries);

        parser.IndexInfo.ShouldBeNull();
    }

    [Fact]
    public void LoadArchive_Version1IndexPathGiven_ResolvesArchiveFromIndex()
    {
        using TempWorkspace workspace = new TempWorkspace();
        Dictionary<string, byte[]> entries = new() { ["a.txt"] = Encoding.UTF8.GetBytes("a") };
        string archivePath = workspace.CreateArchive(Parser.Version.Rpa1, entries);
        string indexPath = Path.ChangeExtension(archivePath, ".rpi");
        Parser parser = new Parser();

        // pointing at the .rpi must locate the matching .rpa
        parser.LoadArchive(indexPath);

        parser.ArchiveVersion.ShouldBe(Parser.Version.Rpa1);
        parser.Index.Keys.ShouldContain("a.txt");
    }

    [Fact]
    public void LoadArchive_Rpa3WithObfuscationKey_DeobfuscatesOffsets()
    {
        using TempWorkspace workspace = new TempWorkspace();
        byte[] payload = Encoding.UTF8.GetBytes("obfuscated payload");
        Dictionary<string, byte[]> entries = new() { ["a.txt"] = payload };
        string archivePath = workspace.CreateArchive(
            Parser.Version.Rpa3, entries, "keyed.rpa", obfuscationKey: 0x1234ABCD);
        Parser parser = new Parser();

        parser.LoadArchive(archivePath);

        parser.ObfuscationKey.ShouldBe(0x1234ABCD);
        parser.ExtractData("a.txt").ShouldBe(payload);
    }

    [Fact]
    public void LoadArchive_ArchiveWithPadding_StillReadsContents()
    {
        using TempWorkspace workspace = new TempWorkspace();
        byte[] payload = Encoding.UTF8.GetBytes("padded payload");
        Dictionary<string, byte[]> entries = new() { ["a.txt"] = payload };
        string archivePath = workspace.CreateArchive(
            Parser.Version.Rpa3, entries, "padded.rpa", padding: 32);
        Parser parser = new Parser();

        parser.LoadArchive(archivePath);

        parser.ExtractData("a.txt").ShouldBe(payload);
    }

    [Fact]
    public void LoadArchive_FileDoesNotExist_Throws()
    {
        using TempWorkspace workspace = new TempWorkspace();
        Parser parser = new Parser();

        Exception ex = Should.Throw<Exception>(() => parser.LoadArchive(workspace.Path_("missing.rpa")));
        ex.Message.ShouldContain("does not exist");
    }

    [Fact]
    public void LoadArchive_EmptyPath_Throws()
    {
        Parser parser = new Parser();

        Should.Throw<Exception>(() => parser.LoadArchive(string.Empty));
    }

    [Fact]
    public void LoadArchive_FileIsNotAnArchive_Throws()
    {
        using TempWorkspace workspace = new TempWorkspace();
        string path = workspace.WriteFile("not-an-archive.rpa", "just some text, no RPA header");
        Parser parser = new Parser();

        Exception ex = Should.Throw<Exception>(() => parser.LoadArchive(path));
        ex.Message.ShouldContain("not valid RenPy Archive");
    }

    [Fact]
    public void LoadArchive_Version1ArchiveMissingIndexFile_Throws()
    {
        using TempWorkspace workspace = new TempWorkspace();
        Dictionary<string, byte[]> entries = new() { ["a.txt"] = Encoding.UTF8.GetBytes("a") };
        string archivePath = workspace.CreateArchive(Parser.Version.Rpa1, entries);
        File.Delete(Path.ChangeExtension(archivePath, ".rpi"));
        Parser parser = new Parser();

        // without the .rpi the version can no longer be recognised
        Should.Throw<Exception>(() => parser.LoadArchive(archivePath));
    }
}
