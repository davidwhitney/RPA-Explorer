using System;
using System.IO;
using RpaParser;
using Shouldly;

namespace RpaParser.Tests;

public class ArchiveFileInfoTests
{
    [Fact]
    public void Resolve_ArchivePath_DerivesTheSiblingIndexPath()
    {
        using var workspace = new TempWorkspace();
        var archivePath = workspace.WriteFile("game.rpa", "content");

        var files = ArchiveFileInfo.Resolve(archivePath);

        files.ArchivePath.ShouldBe(archivePath);
        files.IndexPath.ShouldBe(workspace.Path_("game.rpi"));
    }

    [Fact]
    public void Resolve_IndexPath_ResolvesBackToTheArchive()
    {
        using var workspace = new TempWorkspace();
        workspace.WriteFile("game.rpa", "content");
        var indexPath = workspace.Path_("game.rpi");

        var files = ArchiveFileInfo.Resolve(indexPath);

        files.ArchivePath.ShouldBe(workspace.Path_("game.rpa"));
        files.IndexPath.ShouldBe(indexPath);
    }

    [Fact]
    public void Resolve_UppercaseExtension_KeepsTheCasingOnTheDerivedPath()
    {
        using var workspace = new TempWorkspace();
        workspace.WriteFile("GAME.RPA", "content");

        var files = ArchiveFileInfo.Resolve(workspace.Path_("GAME.RPA"));

        // Lower casing here is what breaks on a case sensitive filesystem.
        Path.GetFileName(files.IndexPath).ShouldBe("GAME.RPI");
    }

    [Fact]
    public void Resolve_BothHalvesPresent_ReportsThePair()
    {
        using var workspace = new TempWorkspace();
        workspace.WriteFile("game.rpa", "content");
        workspace.WriteFile("game.rpi", "index");

        ArchiveFileInfo.Resolve(workspace.Path_("game.rpa")).IndexPairExists.ShouldBeTrue();
    }

    [Fact]
    public void Resolve_NoSiblingIndex_ReportsNoPair()
    {
        using var workspace = new TempWorkspace();
        workspace.WriteFile("game.rpa", "content");

        ArchiveFileInfo.Resolve(workspace.Path_("game.rpa")).IndexPairExists.ShouldBeFalse();
    }

    [Fact]
    public void Resolve_NameWithNeitherExtension_NamesNoIndexAtAll()
    {
        using var workspace = new TempWorkspace();
        var path = workspace.WriteFile("archive.bin", "content");

        var files = ArchiveFileInfo.Resolve(path);

        files.ArchivePath.ShouldBe(path);
        files.IndexPath.ShouldBeNull();
        files.IndexPairExists.ShouldBeFalse();
    }

    [Fact]
    public void Resolve_ArchiveDoesNotExist_Throws()
    {
        using var workspace = new TempWorkspace();

        var ex = Should.Throw<Exception>(() => ArchiveFileInfo.Resolve(workspace.Path_("absent.rpa")));

        ex.Message.ShouldContain("does not exist");
    }

    [Fact]
    public void Resolve_EmptyPath_Throws()
    {
        var ex = Should.Throw<Exception>(() => ArchiveFileInfo.Resolve(string.Empty));

        ex.Message.ShouldContain("No archive file provided");
    }

    [Fact]
    public void Archive_ResolvedFile_DescribesTheFileOnDisk()
    {
        using var workspace = new TempWorkspace();
        var archivePath = workspace.WriteFile("game.rpa", "content");

        var files = ArchiveFileInfo.Resolve(archivePath);

        files.Archive.Exists.ShouldBeTrue();
        files.Archive.Length.ShouldBe(new FileInfo(archivePath).Length);
    }

    [Fact]
    public void ReadFirstLine_HeaderLine_IsReturnedWithoutItsNewline()
    {
        using var workspace = new TempWorkspace();
        var archivePath = workspace.WriteFile("game.rpa", "RPA-3.0 0000000000000022 deadbeef\nrest of file");

        ArchiveFileInfo.Resolve(archivePath).ReadFirstLine()
            .ShouldBe("RPA-3.0 0000000000000022 deadbeef");
    }
}
