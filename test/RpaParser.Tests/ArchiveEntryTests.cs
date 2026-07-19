using Shouldly;

namespace RpaParser.Tests;

public class ArchiveEntryTests
{
    [Fact]
    public void FromFilename_FileInTheRoot_TakesItsNameAsTheArchivePath()
    {
        using var workspace = new TempWorkspace();
        var file = workspace.WriteFile("staging/script.rpy", "label start:");

        var entry = ArchiveEntry.FromFilename(file, workspace.Path_("staging"));

        entry.TreePath.ShouldBe("script.rpy");
        entry.InArchive.ShouldBeFalse();
        entry.Length.ShouldBe(new FileInfo(file).Length);
    }

    [Fact]
    public void FromFilename_NestedFile_KeepsThePathRelativeToTheRoot()
    {
        using var workspace = new TempWorkspace();
        var file = workspace.WriteFile("staging/images/scenes/room.png", "x");

        var entry = ArchiveEntry.FromFilename(file, workspace.Path_("staging"));

        entry.TreePath.ShouldBe("images/scenes/room.png");
        entry.ParentPath.ShouldBe(Path.Combine("images", "scenes"));
    }

    [Fact]
    public void FromFilename_ArchivePaths_AlwaysUseForwardSlashes()
    {
        using var workspace = new TempWorkspace();
        var file = workspace.WriteFile("staging/audio/theme.ogg", "x");

        var entry = ArchiveEntry.FromFilename(file, workspace.Path_("staging"));

        entry.TreePath.ShouldNotContain("\\");
        entry.FullPath.ShouldNotContain("\\");
    }

    [Fact]
    public void FromFilename_StagedFile_HasNoSegmentsUntilItIsWritten()
    {
        using var workspace = new TempWorkspace();
        var file = workspace.WriteFile("staging/a.txt", "x");

        var entry = ArchiveEntry.FromFilename(file, workspace.Path_("staging"));

        entry.Segments.ShouldBeEmpty();
        entry.FullPath.ShouldBe(file.Replace('\\', '/'));
    }

    [Fact]
    public void FromIndex_Segments_AreCarriedAndTheirLengthsTotalled()
    {
        ArchiveSegment[] segments = [new(0, 10, []), new(20, 5, [])];

        var entry = ArchiveEntry.FromIndex("dir/a.txt", segments);

        entry.InArchive.ShouldBeTrue();
        entry.Segments.Count.ShouldBe(2);
        entry.Length.ShouldBe(15);
    }

    [Fact]
    public void FromIndex_TreePath_YieldsTheParentDirectory()
    {
        var entry = ArchiveEntry.FromIndex("images/scenes/room.png", [new ArchiveSegment(0, 1, [])]);

        entry.TreePath.ShouldBe("images/scenes/room.png");
        entry.ParentPath.ShouldBe(Path.Combine("images", "scenes"));
    }

    [Fact]
    public void FromIndex_NoSegments_HasZeroLength()
    {
        var entry = ArchiveEntry.FromIndex("empty.txt", []);

        entry.Length.ShouldBe(0);
    }

    [Fact]
    public void With_ChangingAField_LeavesTheOriginalAlone()
    {
        var original = ArchiveEntry.FromIndex("a.txt", [new ArchiveSegment(0, 4, [])]);

        var renamed = original with { TreePath = "b.txt" };

        renamed.TreePath.ShouldBe("b.txt");
        original.TreePath.ShouldBe("a.txt");
        renamed.Length.ShouldBe(original.Length);
    }
}
