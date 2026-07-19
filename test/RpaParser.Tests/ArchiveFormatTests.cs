using System;
using System.Linq;
using System.Text;
using RpaParser;
using Shouldly;

namespace RpaParser.Tests;

public class ArchiveFormatTests
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

    public static TheoryData<ArchiveFormat> FormatsWithHeaders =>
        Formats(ArchiveFormat.All.Where(f => !f.HasSeparateIndexFile).ToArray());

    public static TheoryData<ArchiveFormat> ObfuscatedFormats =>
        Formats(ArchiveFormat.All.Where(f => f.UsesObfuscation).ToArray());

    public static TheoryData<ArchiveFormat> PlainFormats =>
        Formats(ArchiveFormat.All.Where(f => !f.UsesObfuscation).ToArray());

    public static TheoryData<string, ArchiveFormat> KnownHeaders => new()
    {
        { "RPA-2.0 0000000000000019", ArchiveFormat.Rpa2 },
        { "RPA-3.0 0000000000000022 deadbeef", ArchiveFormat.Rpa3 },
        { "RPA-3.2 0000000000000022 00000000 deadbeef", ArchiveFormat.Rpa32 }
    };

    [Theory]
    [MemberData(nameof(KnownHeaders))]
    public void Detect_HeaderWithKnownMagic_ReturnsMatchingFormat(string firstLine, ArchiveFormat expected)
    {
        var format = ArchiveFormat.Detect(firstLine, indexPairExists: false);

        format.ShouldBeSameAs(expected);
    }

    [Fact]
    public void Detect_NoMagicButIndexPairPresent_ReturnsVersion1()
    {
        ArchiveFormat.Detect("anything at all", indexPairExists: true).ShouldBeOfType<Rpa1Format>();
    }

    [Fact]
    public void Detect_NoMagicAndNoIndexPair_ReturnsNull()
    {
        ArchiveFormat.Detect("not an archive", indexPairExists: false).ShouldBeNull();
    }

    [Fact]
    public void Detect_NullFirstLine_DoesNotThrow()
    {
        ArchiveFormat.Detect(null, indexPairExists: false).ShouldBeNull();
    }

    [Fact]
    public void Detect_Version32Header_IsNotMistakenForVersion3()
    {
        // Both share the "RPA-3" prefix, so 3.2 is offered the header first.
        ArchiveFormat.Detect("RPA-3.2 0000000000000022 00000000 deadbeef", false)
            .ShouldBeOfType<Rpa32Format>();
    }

    [Theory]
    [MemberData(nameof(AllFormats))]
    public void ForVersion_SupportedVersion_ReturnsThatFormat(ArchiveFormat format)
    {
        ArchiveFormat.ForVersion(format.Version).ShouldBeSameAs(format);
    }

    [Theory]
    [InlineData(-1)]
    [InlineData(0)]
    [InlineData(4)]
    public void ForVersion_UnsupportedVersion_ReturnsNull(double version)
    {
        ArchiveFormat.ForVersion(version).ShouldBeNull();
    }

    /// <summary>
    /// File data is written at HeaderLength, so a header longer than that overwrites the
    /// first file. Getting this wrong is exactly what corrupted 3.2 archives.
    /// </summary>
    [Theory]
    [MemberData(nameof(FormatsWithHeaders))]
    public void BuildHeader_AnyOffsetAndKey_ProducesExactlyHeaderLengthBytes(ArchiveFormat format)
    {
        foreach (long offset in new long[] { 0, 34, 4096, 0x7FFFFFFF })
        {
            string header = format.BuildHeader(offset, 0xDEADBEEF);

            Encoding.UTF8.GetByteCount(header).ShouldBe(format.HeaderLength);
        }
    }

    /// <summary>The key has to come back out of the header the format wrote.</summary>
    [Theory]
    [MemberData(nameof(ObfuscatedFormats))]
    public void ReadObfuscationKey_HeaderItWrote_RecoversTheKey(ArchiveFormat format)
    {
        const long key = 0xDEADBEEF;

        string header = format.BuildHeader(4096, key);
        long recovered = format.ReadObfuscationKey(header.TrimEnd('\n').Split(' '));

        recovered.ShouldBe(key);
    }

    [Theory]
    [MemberData(nameof(PlainFormats))]
    public void ReadObfuscationKey_FormatWithoutObfuscation_ReturnsZero(ArchiveFormat format)
    {
        format.ReadObfuscationKey(["RPA-2.0", "0000000000000019"]).ShouldBe(0);
    }

    [Fact]
    public void HasSeparateIndexFile_OnlyVersion1KeepsItsIndexOutOfTheArchive()
    {
        ArchiveFormat.All.Where(f => f.HasSeparateIndexFile)
            .ShouldHaveSingleItem()
            .ShouldBeOfType<Rpa1Format>();
    }

    [Fact]
    public void SupportsPadding_Version1_IsFalseBecauseItHasNoHeaderToOffsetFrom()
    {
        ArchiveFormat.Rpa1.SupportsPadding.ShouldBeFalse();
        ArchiveFormat.All.Where(f => f != ArchiveFormat.Rpa1)
            .ShouldAllBe(f => f.SupportsPadding);
    }

    [Fact]
    public void BuildHeader_Version1_ThrowsBecauseItHasNoHeader()
    {
        Should.Throw<System.InvalidOperationException>(() => ArchiveFormat.Rpa1.BuildHeader(0, 0));
    }

    [Theory]
    [MemberData(nameof(AllFormats))]
    public void DisplayName_EveryFormat_IsPresentedToTheUser(ArchiveFormat format)
    {
        format.DisplayName.ShouldNotBeNullOrWhiteSpace();
        format.ToString().ShouldBe(format.DisplayName);
    }

    [Theory]
    [MemberData(nameof(FormatsWithHeaders))]
    public void LocateIndex_EmbeddedIndex_PointsAtTheArchiveItself(ArchiveFormat format)
    {
        using var workspace = new TempWorkspace();
        var archivePath = workspace.WriteFile("game.rpa", format.BuildHeader(4096, 0xDEADBEEF));

        IndexFileInfo location = format.LocateIndex(new ArchiveFileInfo(archivePath));

        location.FilePath.ShouldBe(archivePath);
        location.Offset.ShouldBe(4096);
        location.IsSeparateFile.ShouldBeFalse();
    }

    [Theory]
    [MemberData(nameof(ObfuscatedFormats))]
    public void LocateIndex_ObfuscatedFormat_CarriesTheKeyFromTheHeader(ArchiveFormat format)
    {
        using var workspace = new TempWorkspace();
        var archivePath = workspace.WriteFile("game.rpa", format.BuildHeader(4096, 0xDEADBEEF));

        IndexFileInfo location = format.LocateIndex(new ArchiveFileInfo(archivePath));

        location.ObfuscationKey.ShouldBe(0xDEADBEEF);
    }

    [Fact]
    public void LocateIndex_Version1_PointsAtTheSiblingIndexFile()
    {
        using var workspace = new TempWorkspace();
        var archivePath = workspace.WriteFile("game.rpa", "data");
        var indexPath = workspace.WriteFile("game.rpi", "index");
        // Both halves present, so these files are a version 1 archive.

        IndexFileInfo location = ArchiveFormat.Rpa1.LocateIndex(new ArchiveFileInfo(archivePath));

        location.FilePath.ShouldBe(indexPath);
        location.IsSeparateFile.ShouldBeTrue();
        // The index is the whole file, so there is nothing to seek past and no key.
        location.Offset.ShouldBe(0);
        location.ObfuscationKey.ShouldBe(0);
    }

    [Fact]
    public void LocateIndex_Version1WhenTheNameNamesNoPair_Throws()
    {
        using var workspace = new TempWorkspace();
        var archivePath = workspace.WriteFile("archive.bin", ArchiveFormat.Rpa3.BuildHeader(0, 0));

        Exception ex = Should.Throw<Exception>(
            () => ArchiveFormat.Rpa1.LocateIndex(new ArchiveFileInfo(archivePath)));

        ex.Message.ShouldContain("No index file provided");
    }

    [Fact]
    public void LocateIndex_Version1WithMissingIndexFile_Throws()
    {
        using var workspace = new TempWorkspace();
        // A version 3 archive, so it resolves, but with no sibling for version 1 to find.
        var archivePath = workspace.WriteFile("game.rpa", ArchiveFormat.Rpa3.BuildHeader(0, 0));

        Exception ex = Should.Throw<Exception>(
            () => ArchiveFormat.Rpa1.LocateIndex(new ArchiveFileInfo(archivePath)));

        ex.Message.ShouldContain("Index file does not exist");
    }

    [Fact]
    public void All_EveryFormat_HasADistinctVersion()
    {
        ArchiveFormat.All.Select(f => f.Version).Distinct().Count().ShouldBe(ArchiveFormat.All.Count);
    }
}
