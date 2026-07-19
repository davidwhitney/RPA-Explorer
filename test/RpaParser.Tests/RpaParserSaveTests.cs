using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using RpaParser;
using Shouldly;

namespace RpaParser.Tests;

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
        using var workspace = new TempWorkspace();
        var requested = workspace.Path_("no-extension");

        var saved = workspace.CreateArchive(Parser.Version.Rpa3, SampleEntries(), "no-extension");

        saved.ShouldBe(requested + ".rpa");
        File.Exists(saved).ShouldBeTrue();
    }

    [Fact]
    public void SaveArchive_PathEndingInRpi_WritesRpaInstead()
    {
        using var workspace = new TempWorkspace();

        var saved = workspace.CreateArchive(Parser.Version.Rpa3, SampleEntries(), "given.rpi");

        saved.ShouldEndWith(".rpa");
        File.Exists(saved).ShouldBeTrue();
    }

    [Fact]
    public void SaveArchive_Version1_WritesBothArchiveAndIndexFiles()
    {
        using var workspace = new TempWorkspace();

        var saved = workspace.CreateArchive(Parser.Version.Rpa1, SampleEntries(), "v1.rpa");

        File.Exists(saved).ShouldBeTrue();
        File.Exists(Path.ChangeExtension(saved, ".rpi")).ShouldBeTrue();
    }

    [Theory]
    [InlineData(Parser.Version.Rpa1)]
    [InlineData(Parser.Version.Rpa2)]
    [InlineData(Parser.Version.Rpa3)]
    [InlineData(Parser.Version.Rpa32)]
    public void SaveArchive_EachVersion_ProducesArchiveThatReloadsIdentically(double version)
    {
        using var workspace = new TempWorkspace();
        var entries = SampleEntries();

        var reloaded = workspace.LoadArchive(version, entries, $"roundtrip-{version}.rpa");

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
        var first = workspace.LoadArchive(Parser.Version.Rpa3, SampleEntries());

        var resaved = first.SaveArchive(workspace.Path_("resaved.rpa"));
        var second = new Parser();
        second.LoadArchive(resaved);

        second.Index.Count.ShouldBe(2);
        Encoding.UTF8.GetString(second.ExtractData("dir/b.txt")).ShouldBe("second");
    }

    [Fact]
    public void SaveArchive_UnsupportedVersion_Throws()
    {
        using var workspace = new TempWorkspace();
        var parser = new Parser { ArchiveVersion = Parser.Version.Unknown };
        parser.Index.Add("a.txt", new Parser.ArchiveIndex
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
        using var workspace = new TempWorkspace();
        var parser = new Parser { ArchiveVersion = Parser.Version.Rpa3 };
        parser.Index.Add("ghost.txt", new Parser.ArchiveIndex
        {
            InArchive = false,
            FullPath = workspace.Path_("ghost.txt").Replace('\\', '/'),
            TreePath = "ghost.txt",
            Length = 10
        });
        var target = workspace.Path_("partial.rpa");

        Should.Throw<Exception>(() => parser.SaveArchive(target));

        File.Exists(target).ShouldBeFalse();
    }

    [Fact]
    public void SaveArchive_WithPadding_ProducesLargerArchiveThanWithout()
    {
        using var workspace = new TempWorkspace();

        var plain = workspace.CreateArchive(Parser.Version.Rpa3, SampleEntries(), "plain.rpa");
        var padded = workspace.CreateArchive(
            Parser.Version.Rpa3, SampleEntries(), "padded.rpa", padding: 64);

        new FileInfo(padded).Length.ShouldBeGreaterThan(new FileInfo(plain).Length);
    }

    [Fact]
    public void DeepCopyIndex_ModifyingCopy_LeavesOriginalUnchanged()
    {
        using var workspace = new TempWorkspace();
        var parser = workspace.LoadArchive(Parser.Version.Rpa3, SampleEntries());

        SortedDictionary<string, Parser.ArchiveIndex> copy = parser.DeepCopyIndex(parser.Index);
        copy["a.txt"].TreePath = "changed.txt";
        copy.Remove("dir/b.txt");

        parser.Index["a.txt"].TreePath.ShouldBe("a.txt");
        parser.Index.ShouldContainKey("dir/b.txt");
    }

    [Fact]
    public void DeepCopyIndex_CopiedEntry_CarriesAllFieldsAndSegments()
    {
        using var workspace = new TempWorkspace();
        var parser = workspace.LoadArchive(Parser.Version.Rpa3, SampleEntries());

        SortedDictionary<string, Parser.ArchiveIndex> copy = parser.DeepCopyIndex(parser.Index);

        var original = parser.Index["a.txt"];
        var copied = copy["a.txt"];
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
        var parser = new Parser();

        SortedDictionary<string, Parser.ArchiveIndex> copy =
            parser.DeepCopyIndex(new SortedDictionary<string, Parser.ArchiveIndex>());

        copy.ShouldBeEmpty();
    }
}
