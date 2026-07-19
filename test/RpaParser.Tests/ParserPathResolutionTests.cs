using System.Collections.Generic;
using System.IO;
using System.Text;
using RpaParser;
using Shouldly;

namespace RpaParser.Tests;

/// <summary>
/// Version 1 archives are a .rpa/.rpi pair, and the parser derives one half from the other.
/// The derived path has to keep the casing it was given: on a case sensitive filesystem
/// (any Linux user) "GAME.RPI" sits beside "GAME.RPA", not "GAME.rpa".
/// </summary>
public class ParserPathResolutionTests
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

        string archive = workspace.Path_(archiveName);
        string index = workspace.Path_(indexName);
        File.Move(created, archive);
        File.Move(createdIndex, index);
        return (archive, index);
    }

    [Fact]
    public void LoadArchive_UppercaseIndexPath_DerivesArchivePathWithMatchingCase()
    {
        using TempWorkspace workspace = new TempWorkspace();
        (string archive, string index) = BuildPair(workspace, "GAME.RPA", "GAME.RPI");
        Archive parser = Archive.Load(index);

        // Lower casing the extension here is what breaks on a case sensitive filesystem.
        Path.GetFileName(parser.ArchiveInfo.FullName).ShouldBe("GAME.RPA");
        parser.Index.Keys.ShouldContain("a.txt");
    }

    [Fact]
    public void LoadArchive_UppercaseArchivePath_DerivesIndexPathWithMatchingCase()
    {
        using TempWorkspace workspace = new TempWorkspace();
        (string archive, string index) = BuildPair(workspace, "GAME.RPA", "GAME.RPI");
        Archive parser = Archive.Load(archive);

        Path.GetFileName(parser.IndexInfo.FullName).ShouldBe("GAME.RPI");
    }

    [Fact]
    public void LoadArchive_LowercaseIndexPath_StillResolvesLowercaseArchive()
    {
        using TempWorkspace workspace = new TempWorkspace();
        (string archive, string index) = BuildPair(workspace, "game.rpa", "game.rpi");
        Archive parser = Archive.Load(index);

        Path.GetFileName(parser.ArchiveInfo.FullName).ShouldBe("game.rpa");
        Path.GetFileName(parser.IndexInfo.FullName).ShouldBe("game.rpi");
    }

    [Fact]
    public void LoadArchive_MixedCaseExtension_ResolvesTheArchive()
    {
        using TempWorkspace workspace = new TempWorkspace();
        (string archive, string index) = BuildPair(workspace, "Game.Rpa", "Game.Rpi");
        Archive parser = Archive.Load(archive);

        parser.Format.ShouldBeSameAs(ArchiveFormat.Rpa1);
        parser.Index.Keys.ShouldContain("a.txt");
    }
}
