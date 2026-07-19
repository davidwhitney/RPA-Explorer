using System.Collections.Generic;
using System.IO;
using System.Text;
using RpaParser;
using Shouldly;

namespace RpaParser.Tests;

/// <summary>
/// Version 1 archives are a .rpa/.rpi pair, and the archive derives one half from the other.
/// The derived path has to keep the casing it was given: on a case sensitive filesystem
/// (any Linux user) "GAME.RPI" sits beside "GAME.RPA", not "GAME.rpa".
/// </summary>
public class ArchivePathResolutionTests
{
    private static Dictionary<string, byte[]> Entries() => new()
    {
        ["a.txt"] = Encoding.UTF8.GetBytes("content")
    };

    /// <summary>Builds a version 1 pair and renames both halves to the given base name.</summary>
    private static (string Archive, string Index) BuildPair(TempWorkspace workspace, string archiveName, string indexName)
    {
        string created = workspace.CreateArchive(ArchiveFormat.Rpa1, Entries(), "source.rpa");
        string createdIndex = Path.ChangeExtension(created, ".rpi");

        string archivePath = workspace.Path_(archiveName);
        string indexPath = workspace.Path_(indexName);
        File.Move(created, archivePath);
        File.Move(createdIndex, indexPath);
        return (archivePath, indexPath);
    }

    [Fact]
    public void LoadArchive_UppercaseIndexPath_DerivesArchivePathWithMatchingCase()
    {
        using TempWorkspace workspace = new TempWorkspace();
        (string archivePath, string indexPath) = BuildPair(workspace, "GAME.RPA", "GAME.RPI");
        Archive archive = Archive.Load(indexPath);

        // Lower casing the extension here is what breaks on a case sensitive filesystem.
        Path.GetFileName(archive.Files.Archive.FullName).ShouldBe("GAME.RPA");
        archive.Index.Keys.ShouldContain("a.txt");
    }

    [Fact]
    public void LoadArchive_UppercaseArchivePath_DerivesIndexPathWithMatchingCase()
    {
        using TempWorkspace workspace = new TempWorkspace();
        (string archivePath, string indexPath) = BuildPair(workspace, "GAME.RPA", "GAME.RPI");
        Archive archive = Archive.Load(archivePath);

        Path.GetFileName(archive.Files.IndexFile.File.FullName).ShouldBe("GAME.RPI");
    }

    [Fact]
    public void LoadArchive_LowercaseIndexPath_StillResolvesLowercaseArchive()
    {
        using TempWorkspace workspace = new TempWorkspace();
        (string archivePath, string indexPath) = BuildPair(workspace, "game.rpa", "game.rpi");
        Archive archive = Archive.Load(indexPath);

        Path.GetFileName(archive.Files.Archive.FullName).ShouldBe("game.rpa");
        Path.GetFileName(archive.Files.IndexFile.File.FullName).ShouldBe("game.rpi");
    }

    [Fact]
    public void LoadArchive_MixedCaseExtension_ResolvesTheArchive()
    {
        using TempWorkspace workspace = new TempWorkspace();
        (string archivePath, string indexPath) = BuildPair(workspace, "Game.Rpa", "Game.Rpi");
        Archive archive = Archive.Load(archivePath);

        archive.Format.ShouldBeSameAs(ArchiveFormat.Rpa1);
        archive.Index.Keys.ShouldContain("a.txt");
    }
}
