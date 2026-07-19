using System;
using System.Linq;
using System.Collections.Generic;
using System.IO;
using System.Text;
using RpaParser;
using Shouldly;

namespace RpaParser.Tests;

public class RpaParserSaveTests
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
            reloaded.ExtractData(entry.Key).ShouldBe(entry.Value);
        }
    }

    [Fact]
    public void SaveArchive_ReSavingLoadedArchive_PreservesContents()
    {
        using var workspace = new TempWorkspace();
        var first = workspace.LoadArchive(ArchiveFormat.Rpa3, SampleEntries());

        var resaved = first.Save(workspace.Path_("resaved.rpa"));
        var second = Archive.Load(resaved);

        second.Index.Count.ShouldBe(2);
        Encoding.UTF8.GetString(second.ExtractData("dir/b.txt")).ShouldBe("second");
    }

    [Fact]
    public void SaveArchive_NoFormatChosen_Throws()
    {
        using var workspace = new TempWorkspace();
        var parser = new Archive();  // no format chosen
        parser.Index.Add("a.txt", new ArchiveEntry
        {
            InArchive = false,
            FullPath = workspace.WriteFile("a.txt", "x").Replace('\\', '/'),
            TreePath = "a.txt",
            Length = 1
        });

        Should.Throw<Exception>(() => parser.Save(workspace.Path_("bad.rpa")));
    }

    [Fact]
    public void SaveArchive_SourceFileMissing_ThrowsAndLeavesNoPartialArchive()
    {
        using var workspace = new TempWorkspace();
        var parser = new Archive { Format = ArchiveFormat.Rpa3 };
        parser.Index.Add("ghost.txt", new ArchiveEntry
        {
            InArchive = false,
            FullPath = workspace.Path_("ghost.txt").Replace('\\', '/'),
            TreePath = "ghost.txt",
            Length = 10
        });
        var target = workspace.Path_("partial.rpa");

        Should.Throw<Exception>(() => parser.Save(target));

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
    public void DeepCopyIndex_ModifyingCopy_LeavesOriginalUnchanged()
    {
        using var workspace = new TempWorkspace();
        var parser = workspace.LoadArchive(ArchiveFormat.Rpa3, SampleEntries());

        SortedDictionary<string, ArchiveEntry> copy = parser.CopyIndex(parser.Index);
        copy["a.txt"].TreePath = "changed.txt";
        copy.Remove("dir/b.txt");

        parser.Index["a.txt"].TreePath.ShouldBe("a.txt");
        parser.Index.ShouldContainKey("dir/b.txt");
    }

    [Fact]
    public void DeepCopyIndex_CopiedEntry_CarriesAllFieldsAndSegments()
    {
        using var workspace = new TempWorkspace();
        var parser = workspace.LoadArchive(ArchiveFormat.Rpa3, SampleEntries());

        SortedDictionary<string, ArchiveEntry> copy = parser.CopyIndex(parser.Index);

        var original = parser.Index["a.txt"];
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
    public void DeepCopyIndex_EmptyIndex_ReturnsEmptyCopy()
    {
        var parser = new Archive();

        SortedDictionary<string, ArchiveEntry> copy =
            parser.CopyIndex(new SortedDictionary<string, ArchiveEntry>());

        copy.ShouldBeEmpty();
    }
}
