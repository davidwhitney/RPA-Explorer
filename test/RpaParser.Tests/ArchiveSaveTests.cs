using System;
using System.Linq;
using System.Collections.Generic;
using System.IO;
using System.Text;
using RpaParser;
using RpaParser.Formats;
using Shouldly;

namespace RpaParser.Tests;

public class ArchiveSaveTests
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

    private static Dictionary<string, byte[]> SampleEntries() => new()
    {
        ["a.txt"] = Encoding.UTF8.GetBytes("first"),
        ["dir/b.txt"] = Encoding.UTF8.GetBytes("second")
    };

    [Fact]
    public void SaveArchive_PathWithoutExtension_AppendsRpaExtension()
    {
        using var workspace = new TempWorkspace();
        var requested = workspace.Path_("no-extension");

        var saved = workspace.CreateArchive(ArchiveFormat.Rpa3, SampleEntries(), "no-extension");

        saved.ShouldBe(requested + ".rpa");
        File.Exists(saved).ShouldBeTrue();
    }

    [Fact]
    public void SaveArchive_PathEndingInRpi_WritesRpaInstead()
    {
        using var workspace = new TempWorkspace();

        var saved = workspace.CreateArchive(ArchiveFormat.Rpa3, SampleEntries(), "given.rpi");

        saved.ShouldEndWith(".rpa");
        File.Exists(saved).ShouldBeTrue();
    }

    [Fact]
    public void SaveArchive_Version1_WritesBothArchiveAndIndexFiles()
    {
        using var workspace = new TempWorkspace();

        var saved = workspace.CreateArchive(ArchiveFormat.Rpa1, SampleEntries(), "v1.rpa");

        File.Exists(saved).ShouldBeTrue();
        File.Exists(Path.ChangeExtension(saved, ".rpi")).ShouldBeTrue();
    }

    [Theory]
    [MemberData(nameof(AllFormats))]
    public void SaveArchive_EachVersion_ProducesArchiveThatReloadsIdentically(ArchiveFormat format)
    {
        using var workspace = new TempWorkspace();
        var entries = SampleEntries();

        var reloaded = workspace.LoadArchive(format, entries, $"roundtrip-{format.Version}.rpa");

        reloaded.Index.Count.ShouldBe(entries.Count);
        foreach (var entry in entries)
        {
            reloaded.Read(entry.Key).ShouldBe(entry.Value);
        }
    }

    [Fact]
    public void SaveArchive_ReSavingLoadedArchive_PreservesContents()
    {
        using var workspace = new TempWorkspace();
        var first = workspace.LoadArchive(ArchiveFormat.Rpa3, SampleEntries());

        var resaved = first.Save(workspace.Path_("resaved.rpa"));
        var second = new Archive(resaved);

        second.Index.Count.ShouldBe(2);
        Encoding.UTF8.GetString(second.Read("dir/b.txt")).ShouldBe("second");
    }

    [Fact]
    public void SaveArchive_NoFormatChosen_Throws()
    {
        using var workspace = new TempWorkspace();
        var archive = new Archive();  // no format chosen
        archive.Index.Add("a.txt", new ArchiveEntry
        {
            InArchive = false,
            FullPath = workspace.WriteFile("a.txt", "x").Replace('\\', '/'),
            TreePath = "a.txt",
            Length = 1
        });

        Should.Throw<Exception>(() => archive.Save(workspace.Path_("bad.rpa")));
    }

    [Fact]
    public void SaveArchive_SourceFileMissing_ThrowsAndLeavesNoPartialArchive()
    {
        using var workspace = new TempWorkspace();
        var archive = new Archive(ArchiveFormat.Rpa3);
        archive.Index.Add("ghost.txt", new ArchiveEntry
        {
            InArchive = false,
            FullPath = workspace.Path_("ghost.txt").Replace('\\', '/'),
            TreePath = "ghost.txt",
            Length = 10
        });
        var target = workspace.Path_("partial.rpa");

        Should.Throw<Exception>(() => archive.Save(target));

        File.Exists(target).ShouldBeFalse();
    }

    [Fact]
    public void SaveArchive_WithPadding_ProducesLargerArchiveThanWithout()
    {
        using var workspace = new TempWorkspace();

        var plain = workspace.CreateArchive(ArchiveFormat.Rpa3, SampleEntries(), "plain.rpa");
        var padded = workspace.CreateArchive(
            ArchiveFormat.Rpa3, SampleEntries(), "padded.rpa", padding: 64);

        new FileInfo(padded).Length.ShouldBeGreaterThan(new FileInfo(plain).Length);
    }

    [Fact]
    public void CopyIndex_ModifyingCopy_LeavesOriginalUnchanged()
    {
        using var workspace = new TempWorkspace();
        var archive = workspace.LoadArchive(ArchiveFormat.Rpa3, SampleEntries());

        ArchiveIndex copy = archive.Index.Copy();
        copy.Remove("dir/b.txt");
        copy["a.txt"] = copy["a.txt"] with { TreePath = "changed.txt" };

        archive.Index["a.txt"].TreePath.ShouldBe("a.txt");
        archive.Index.ShouldContainKey("dir/b.txt");
    }

    [Fact]
    public void CopyIndex_SharedEntry_CannotBeMutatedThroughTheCopy()
    {
        using var workspace = new TempWorkspace();
        var archive = workspace.LoadArchive(ArchiveFormat.Rpa3, SampleEntries());

        ArchiveIndex copy = archive.Index.Copy();

        // Entries are immutable, so sharing them between the index and its copy is safe:
        // changing one means producing a new entry, which the original never sees.
        copy["a.txt"].ShouldBeSameAs(archive.Index["a.txt"]);
        (copy["a.txt"] with { Length = 99 }).ShouldNotBeSameAs(archive.Index["a.txt"]);
        archive.Index["a.txt"].Length.ShouldNotBe(99);
    }

    [Fact]
    public void CopyIndex_CopiedEntry_CarriesAllFieldsAndSegments()
    {
        using var workspace = new TempWorkspace();
        var archive = workspace.LoadArchive(ArchiveFormat.Rpa3, SampleEntries());

        ArchiveIndex copy = archive.Index.Copy();

        var original = archive.Index["a.txt"];
        var copied = copy["a.txt"];
        copied.TreePath.ShouldBe(original.TreePath);
        copied.FullPath.ShouldBe(original.FullPath);
        copied.ParentPath.ShouldBe(original.ParentPath);
        copied.InArchive.ShouldBe(original.InArchive);
        copied.Length.ShouldBe(original.Length);
        copied.Segments.Count.ShouldBe(original.Segments.Count);
        copied.Segments[0].Offset.ShouldBe(original.Segments[0].Offset);
        copied.Segments[0].Length.ShouldBe(original.Segments[0].Length);
    }

    [Fact]
    public void CopyIndex_EmptyIndex_ReturnsEmptyCopy()
    {
        var index = new ArchiveIndex();

        ArchiveIndex copy = index.Copy();

        copy.ShouldBeEmpty();
    }
}
