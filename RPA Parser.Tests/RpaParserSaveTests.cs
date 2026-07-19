using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using RPA_Parser;
using Shouldly;

namespace RPAParser.Tests;

public class RpaParserSaveTests
{
    private static Dictionary<string, byte[]> SampleEntries() => new()
    {
        ["a.txt"] = Encoding.UTF8.GetBytes("first"),
        ["dir/b.txt"] = Encoding.UTF8.GetBytes("second")
    };

    [Fact]
    public void SaveArchive_PathWithoutExtension_AppendsRpaExtension()
    {
        using TempWorkspace workspace = new TempWorkspace();
        string requested = workspace.Path_("no-extension");

        string saved = workspace.CreateArchive(RpaParser.Version.Rpa3, SampleEntries(), "no-extension");

        saved.ShouldBe(requested + ".rpa");
        File.Exists(saved).ShouldBeTrue();
    }

    [Fact]
    public void SaveArchive_PathEndingInRpi_WritesRpaInstead()
    {
        using TempWorkspace workspace = new TempWorkspace();

        string saved = workspace.CreateArchive(RpaParser.Version.Rpa3, SampleEntries(), "given.rpi");

        saved.ShouldEndWith(".rpa");
        File.Exists(saved).ShouldBeTrue();
    }

    [Fact]
    public void SaveArchive_Version1_WritesBothArchiveAndIndexFiles()
    {
        using TempWorkspace workspace = new TempWorkspace();

        string saved = workspace.CreateArchive(RpaParser.Version.Rpa1, SampleEntries(), "v1.rpa");

        File.Exists(saved).ShouldBeTrue();
        File.Exists(Path.ChangeExtension(saved, ".rpi")).ShouldBeTrue();
    }

    [Theory]
    [InlineData(RpaParser.Version.Rpa1)]
    [InlineData(RpaParser.Version.Rpa2)]
    [InlineData(RpaParser.Version.Rpa3)]
    [InlineData(RpaParser.Version.Rpa32)]
    public void SaveArchive_EachVersion_ProducesArchiveThatReloadsIdentically(double version)
    {
        using TempWorkspace workspace = new TempWorkspace();
        Dictionary<string, byte[]> entries = SampleEntries();

        RpaParser reloaded = workspace.LoadArchive(version, entries, $"roundtrip-{version}.rpa");

        reloaded.Index.Count.ShouldBe(entries.Count);
        foreach (KeyValuePair<string, byte[]> entry in entries)
        {
            reloaded.ExtractData(entry.Key).ShouldBe(entry.Value);
        }
    }

    [Fact]
    public void SaveArchive_ReSavingLoadedArchive_PreservesContents()
    {
        using TempWorkspace workspace = new TempWorkspace();
        RpaParser first = workspace.LoadArchive(RpaParser.Version.Rpa3, SampleEntries());

        string resaved = first.SaveArchive(workspace.Path_("resaved.rpa"));
        RpaParser second = new RpaParser();
        second.LoadArchive(resaved);

        second.Index.Count.ShouldBe(2);
        Encoding.UTF8.GetString(second.ExtractData("dir/b.txt")).ShouldBe("second");
    }

    [Fact]
    public void SaveArchive_UnsupportedVersion_Throws()
    {
        using TempWorkspace workspace = new TempWorkspace();
        RpaParser parser = new RpaParser { ArchiveVersion = RpaParser.Version.Unknown };
        parser.Index.Add("a.txt", new RpaParser.ArchiveIndex
        {
            InArchive = false,
            FullPath = workspace.WriteFile("a.txt", "x").Replace('\\', '/'),
            TreePath = "a.txt",
            Length = 1
        });

        Should.Throw<Exception>(() => parser.SaveArchive(workspace.Path_("bad.rpa")));
    }

    [Fact]
    public void SaveArchive_SourceFileMissing_ThrowsAndLeavesNoPartialArchive()
    {
        using TempWorkspace workspace = new TempWorkspace();
        RpaParser parser = new RpaParser { ArchiveVersion = RpaParser.Version.Rpa3 };
        parser.Index.Add("ghost.txt", new RpaParser.ArchiveIndex
        {
            InArchive = false,
            FullPath = workspace.Path_("ghost.txt").Replace('\\', '/'),
            TreePath = "ghost.txt",
            Length = 10
        });
        string target = workspace.Path_("partial.rpa");

        Should.Throw<Exception>(() => parser.SaveArchive(target));

        File.Exists(target).ShouldBeFalse();
    }

    [Fact]
    public void SaveArchive_WithPadding_ProducesLargerArchiveThanWithout()
    {
        using TempWorkspace workspace = new TempWorkspace();

        string plain = workspace.CreateArchive(RpaParser.Version.Rpa3, SampleEntries(), "plain.rpa");
        string padded = workspace.CreateArchive(
            RpaParser.Version.Rpa3, SampleEntries(), "padded.rpa", padding: 64);

        new FileInfo(padded).Length.ShouldBeGreaterThan(new FileInfo(plain).Length);
    }

    [Fact]
    public void DeepCopyIndex_ModifyingCopy_LeavesOriginalUnchanged()
    {
        using TempWorkspace workspace = new TempWorkspace();
        RpaParser parser = workspace.LoadArchive(RpaParser.Version.Rpa3, SampleEntries());

        SortedDictionary<string, RpaParser.ArchiveIndex> copy = parser.DeepCopyIndex(parser.Index);
        copy["a.txt"].TreePath = "changed.txt";
        copy.Remove("dir/b.txt");

        parser.Index["a.txt"].TreePath.ShouldBe("a.txt");
        parser.Index.ShouldContainKey("dir/b.txt");
    }

    [Fact]
    public void DeepCopyIndex_CopiedEntry_CarriesAllFieldsAndSegments()
    {
        using TempWorkspace workspace = new TempWorkspace();
        RpaParser parser = workspace.LoadArchive(RpaParser.Version.Rpa3, SampleEntries());

        SortedDictionary<string, RpaParser.ArchiveIndex> copy = parser.DeepCopyIndex(parser.Index);

        RpaParser.ArchiveIndex original = parser.Index["a.txt"];
        RpaParser.ArchiveIndex copied = copy["a.txt"];
        copied.TreePath.ShouldBe(original.TreePath);
        copied.FullPath.ShouldBe(original.FullPath);
        copied.ParentPath.ShouldBe(original.ParentPath);
        copied.InArchive.ShouldBe(original.InArchive);
        copied.Length.ShouldBe(original.Length);
        copied.Tuples.Count.ShouldBe(original.Tuples.Count);
        copied.Tuples[0].Offset.ShouldBe(original.Tuples[0].Offset);
        copied.Tuples[0].Length.ShouldBe(original.Tuples[0].Length);
    }

    [Fact]
    public void DeepCopyIndex_EmptyIndex_ReturnsEmptyCopy()
    {
        RpaParser parser = new RpaParser();

        SortedDictionary<string, RpaParser.ArchiveIndex> copy =
            parser.DeepCopyIndex(new SortedDictionary<string, RpaParser.ArchiveIndex>());

        copy.ShouldBeEmpty();
    }
}
