using System;
using System.IO;
using RpaParser;
using Shouldly;

namespace RpaParser.Tests;

public class ArchiveFileInfoTests
{
    [Fact]
    public void Constructor_ArchivePath_DerivesTheSiblingIndexPath()
    {
        using var workspace = new TempWorkspace();
        var archivePath = workspace.WriteFile("game.rpa", "content");

        var files = new ArchiveFileInfo(archivePath);

        files.ArchivePath.ShouldBe(archivePath);
        files.IndexPath.ShouldBe(workspace.Path_("game.rpi"));
    }

    [Fact]
    public void Constructor_IndexPath_ResolvesBackToTheArchive()
    {
        using var workspace = new TempWorkspace();
        workspace.WriteFile("game.rpa", "content");
        var indexPath = workspace.Path_("game.rpi");

        var files = new ArchiveFileInfo(indexPath);

        files.ArchivePath.ShouldBe(workspace.Path_("game.rpa"));
        files.IndexPath.ShouldBe(indexPath);
    }

    [Fact]
    public void Constructor_UppercaseExtension_KeepsTheCasingOnTheDerivedPath()
    {
        using var workspace = new TempWorkspace();
        workspace.WriteFile("GAME.RPA", "content");

        var files = new ArchiveFileInfo(workspace.Path_("GAME.RPA"));

        // Lower casing here is what breaks on a case sensitive filesystem.
        Path.GetFileName(files.IndexPath).ShouldBe("GAME.RPI");
    }

    [Fact]
    public void Constructor_BothHalvesPresent_ReportsThePair()
    {
        using var workspace = new TempWorkspace();
        workspace.WriteFile("game.rpa", "content");
        workspace.WriteFile("game.rpi", "index");

        new ArchiveFileInfo(workspace.Path_("game.rpa")).IndexPairExists.ShouldBeTrue();
    }

    [Fact]
    public void Constructor_NoSiblingIndex_ReportsNoPair()
    {
        using var workspace = new TempWorkspace();
        workspace.WriteFile("game.rpa", "content");

        new ArchiveFileInfo(workspace.Path_("game.rpa")).IndexPairExists.ShouldBeFalse();
    }

    [Fact]
    public void Constructor_NameWithNeitherExtension_NamesNoIndexAtAll()
    {
        using var workspace = new TempWorkspace();
        var path = workspace.WriteFile("archive.bin", "content");

        var files = new ArchiveFileInfo(path);

        files.ArchivePath.ShouldBe(path);
        files.IndexPath.ShouldBeNull();
        files.IndexPairExists.ShouldBeFalse();
    }

    [Fact]
    public void Constructor_ArchiveDoesNotExist_Throws()
    {
        using var workspace = new TempWorkspace();

        var ex = Should.Throw<Exception>(() => new ArchiveFileInfo(workspace.Path_("absent.rpa")));

        ex.Message.ShouldContain("does not exist");
    }

    [Fact]
    public void Constructor_EmptyPath_Throws()
    {
        var ex = Should.Throw<Exception>(() => new ArchiveFileInfo(string.Empty));

        ex.Message.ShouldContain("No archive file provided");
    }

    [Fact]
    public void Archive_ResolvedFile_DescribesTheFileOnDisk()
    {
        using var workspace = new TempWorkspace();
        var archivePath = workspace.WriteFile("game.rpa", "content");

        var files = new ArchiveFileInfo(archivePath);

        files.Archive.Exists.ShouldBeTrue();
        files.Archive.Length.ShouldBe(new FileInfo(archivePath).Length);
    }

    [Fact]
    public void ReadFirstLine_HeaderLine_IsReturnedWithoutItsNewline()
    {
        using var workspace = new TempWorkspace();
        var archivePath = workspace.WriteFile("game.rpa", "RPA-3.0 0000000000000022 deadbeef\nrest of file");

        new ArchiveFileInfo(archivePath).ReadFirstLine()
            .ShouldBe("RPA-3.0 0000000000000022 deadbeef");
    }
}
