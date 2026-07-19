using System.Collections.Generic;
using System.Linq;
using RpaParser;
using Shouldly;

namespace RpaParser.Tests;

public class ArchiveIndexTests
{
    private static ArchiveEntry Stored(string treePath, long offset, long length) =>
        ArchiveEntry.FromIndex(treePath, [new ArchiveSegment(offset, length, [])]);

    [Fact]
    public void Copy_RemovingFromTheCopy_LeavesTheOriginalAlone()
    {
        var index = new ArchiveIndex { ["a.txt"] = Stored("a.txt", 0, 4) };

        var copy = index.Copy();
        copy.Remove("a.txt");

        index.ShouldContainKey("a.txt");
        copy.ShouldBeEmpty();
    }

    [Fact]
    public void Copy_EmptyIndex_ReturnsAnEmptyIndex()
    {
        new ArchiveIndex().Copy().ShouldBeEmpty();
    }

    [Fact]
    public void Unsaved_MixOfStagedAndStoredFiles_ReturnsOnlyTheStagedOnes()
    {
        var index = new ArchiveIndex
        {
            ["stored.txt"] = Stored("stored.txt", 0, 4),
            ["staged.txt"] = new ArchiveEntry { TreePath = "staged.txt", InArchive = false }
        };

        index.Unsaved.Select(entry => entry.TreePath).ShouldBe(["staged.txt"]);
    }

    [Fact]
    public void Unsaved_EverythingStored_ReturnsNothing()
    {
        var index = new ArchiveIndex { ["a.txt"] = Stored("a.txt", 0, 4) };

        index.Unsaved.ShouldBeEmpty();
    }

    [Fact]
    public void Serialize_ThenRead_RoundTripsThePlacements()
    {
        using var workspace = new TempWorkspace();
        StoredFile[] placements = [new("a.txt", 34, 10), new("dir/b.txt", 44, 20)];

        var blob = ArchiveIndex.Serialize(placements, obfuscationKey: 0);
        var path = workspace.WriteFile("index.rpi", blob);
        var index = ArchiveIndex.Read(IndexFileInfo.SeparateFile(path));

        index.Count.ShouldBe(2);
        index["a.txt"].Segments[0].Offset.ShouldBe(34);
        index["a.txt"].Length.ShouldBe(10);
        index["dir/b.txt"].Segments[0].Offset.ShouldBe(44);
    }

    [Fact]
    public void Serialize_WithAnObfuscationKey_IsUndoneOnRead()
    {
        using var workspace = new TempWorkspace();
        const long key = 0xDEADBEEF;
        StoredFile[] placements = [new("a.txt", 4096, 128)];

        var blob = ArchiveIndex.Serialize(placements, key);
        var path = workspace.WriteFile("index.rpi", blob);
        var index = ArchiveIndex.Read(IndexFileInfo.InsideArchive(path, 0, key));

        index["a.txt"].Segments[0].Offset.ShouldBe(4096);
        index["a.txt"].Segments[0].Length.ShouldBe(128);
    }

    [Fact]
    public void Read_IndexAtAnOffset_StartsFromThere()
    {
        using var workspace = new TempWorkspace();
        var blob = ArchiveIndex.Serialize([new StoredFile("a.txt", 0, 4)], 0);
        byte[] padded = [.. new byte[16], .. blob];
        var path = workspace.WriteFile("archive.rpa", padded);

        var index = ArchiveIndex.Read(IndexFileInfo.InsideArchive(path, 16, 0));

        index.ShouldContainKey("a.txt");
    }

    [Fact]
    public void Serialize_EmptyIndex_StillProducesAReadableIndex()
    {
        using var workspace = new TempWorkspace();

        var blob = ArchiveIndex.Serialize([], 0);
        var path = workspace.WriteFile("index.rpi", blob);

        ArchiveIndex.Read(IndexFileInfo.SeparateFile(path)).ShouldBeEmpty();
    }
}
