using System.Linq;
using System.Text;
using RpaParser;
using Shouldly;

namespace RpaParser.Tests;

public class ArchiveFormatTests
{
    [Theory]
    [InlineData("RPA-2.0 0000000000000019", Parser.Version.Rpa2)]
    [InlineData("RPA-3.0 0000000000000022 deadbeef", Parser.Version.Rpa3)]
    [InlineData("RPA-3.2 0000000000000022 00000000 deadbeef", Parser.Version.Rpa32)]
    public void Detect_HeaderWithKnownMagic_ReturnsMatchingFormat(string firstLine, double expected)
    {
        ArchiveFormat format = ArchiveFormat.Detect(firstLine, indexPairExists: false);

        format.ShouldNotBeNull();
        format.Version.ShouldBe(expected);
    }

    [Fact]
    public void Detect_NoMagicButIndexPairPresent_ReturnsVersion1()
    {
        ArchiveFormat format = ArchiveFormat.Detect("anything at all", indexPairExists: true);

        format.ShouldBeOfType<Rpa1Format>();
    }

    [Fact]
    public void Detect_NoMagicAndNoIndexPair_ReturnsNull()
    {
        ArchiveFormat format = ArchiveFormat.Detect("not an archive", indexPairExists: false);

        format.ShouldBeNull();
    }

    [Fact]
    public void Detect_NullFirstLine_DoesNotThrow()
    {
        ArchiveFormat format = ArchiveFormat.Detect(null, indexPairExists: false);

        format.ShouldBeNull();
    }

    [Fact]
    public void Detect_Version32Header_IsNotMistakenForVersion3()
    {
        // The 3.2 format is offered the header first; both share the "RPA-3" prefix.
        ArchiveFormat format = ArchiveFormat.Detect("RPA-3.2 0000000000000022 00000000 deadbeef", false);

        format.ShouldBeOfType<Rpa32Format>();
    }

    [Theory]
    [InlineData(Parser.Version.Rpa1)]
    [InlineData(Parser.Version.Rpa2)]
    [InlineData(Parser.Version.Rpa3)]
    [InlineData(Parser.Version.Rpa32)]
    public void ForVersion_SupportedVersion_ReturnsFormat(double version)
    {
        ArchiveFormat.ForVersion(version).Version.ShouldBe(version);
    }

    [Theory]
    [InlineData(Parser.Version.Unknown)]
    [InlineData(4)]
    [InlineData(0)]
    public void ForVersion_UnsupportedVersion_ReturnsNull(double version)
    {
        ArchiveFormat.ForVersion(version).ShouldBeNull();
    }

    /// <summary>
    /// File data is written at HeaderLength, so a header longer than that overwrites the
    /// first file. Getting this wrong is exactly what corrupted 3.2 archives.
    /// </summary>
    [Theory]
    [InlineData(Parser.Version.Rpa2)]
    [InlineData(Parser.Version.Rpa3)]
    [InlineData(Parser.Version.Rpa32)]
    public void BuildHeader_AnyOffsetAndKey_ProducesExactlyHeaderLengthBytes(double version)
    {
        ArchiveFormat format = ArchiveFormat.ForVersion(version);

        foreach (long offset in new long[] { 0, 34, 4096, 0x7FFFFFFF })
        {
            string header = format.BuildHeader(offset, 0xDEADBEEF);

            Encoding.UTF8.GetByteCount(header).ShouldBe(format.HeaderLength);
        }
    }

    /// <summary>The key has to come back out of the header the format wrote.</summary>
    [Theory]
    [InlineData(Parser.Version.Rpa3)]
    [InlineData(Parser.Version.Rpa32)]
    public void ReadObfuscationKey_HeaderItWrote_RecoversTheKey(double version)
    {
        ArchiveFormat format = ArchiveFormat.ForVersion(version);
        const long key = 0xDEADBEEF;

        string header = format.BuildHeader(4096, key);
        long recovered = format.ReadObfuscationKey(header.TrimEnd('\n').Split(' '));

        recovered.ShouldBe(key);
    }

    [Theory]
    [InlineData(Parser.Version.Rpa1)]
    [InlineData(Parser.Version.Rpa2)]
    public void ReadObfuscationKey_FormatWithoutObfuscation_ReturnsZero(double version)
    {
        ArchiveFormat format = ArchiveFormat.ForVersion(version);

        format.ReadObfuscationKey(["RPA-2.0", "0000000000000019"]).ShouldBe(0);
        format.UsesObfuscation.ShouldBeFalse();
    }

    [Fact]
    public void HasSeparateIndexFile_OnlyVersion1KeepsItsIndexOutOfTheArchive()
    {
        ArchiveFormat.All.Where(f => f.HasSeparateIndexFile)
            .ShouldHaveSingleItem()
            .ShouldBeOfType<Rpa1Format>();
    }

    [Fact]
    public void BuildHeader_Version1_ThrowsBecauseItHasNoHeader()
    {
        Should.Throw<System.InvalidOperationException>(
            () => ArchiveFormat.ForVersion(Parser.Version.Rpa1).BuildHeader(0, 0));
    }

    [Fact]
    public void All_EveryFormat_HasADistinctVersion()
    {
        ArchiveFormat.All.Select(f => f.Version).Distinct().Count().ShouldBe(ArchiveFormat.All.Count);
    }
}
