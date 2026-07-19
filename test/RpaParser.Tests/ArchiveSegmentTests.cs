using System.Text;
using Shouldly;

namespace RpaParser.Tests;

public class ArchiveSegmentTests
{
    [Fact]
    public void FromIndexData_OffsetAndLength_AreConvertedFromTheUnpickledValues()
    {
        object[] indexData = [4096L, 128L, ""];

        var segment = ArchiveSegment.FromIndexData(indexData);

        segment.Offset.ShouldBe(4096);
        segment.Length.ShouldBe(128);
    }

    [Fact]
    public void FromIndexData_ValuesArrivingAsInt_AreWidenedToLong()
    {
        // Pickle hands back the narrowest type that fits, so small archives yield ints.
        object[] indexData = [34, 12, ""];

        var segment = ArchiveSegment.FromIndexData(indexData);

        segment.Offset.ShouldBe(34);
        segment.Length.ShouldBe(12);
    }

    [Fact]
    public void FromIndexData_ObfuscationKeyGiven_ReturnsTheDecodedValues()
    {
        const long key = 0xDEADBEEF;
        object[] indexData = [4096L ^ key, 128L ^ key, ""];

        var segment = ArchiveSegment.FromIndexData(indexData, key);

        segment.Offset.ShouldBe(4096);
        segment.Length.ShouldBe(128);
    }

    [Fact]
    public void FromIndexData_NoObfuscationKey_LeavesValuesUntouched()
    {
        object[] indexData = [4096L, 128L];

        var segment = ArchiveSegment.FromIndexData(indexData);

        segment.Offset.ShouldBe(4096);
        segment.Length.ShouldBe(128);
    }

    [Fact]
    public void FromIndexData_PrefixStoredAsBytes_IsTakenAsIs()
    {
        byte[] prefix = [1, 2, 3];
        object[] indexData = [0L, 3L, prefix];

        var segment = ArchiveSegment.FromIndexData(indexData);

        segment.Prefix.ShouldBe(prefix);
    }

    [Fact]
    public void FromIndexData_PrefixStoredAsString_IsEncodedAsUtf8()
    {
        object[] indexData = [0L, 3L, "PRE"];

        var segment = ArchiveSegment.FromIndexData(indexData);

        segment.Prefix.ShouldBe(Encoding.UTF8.GetBytes("PRE"));
    }

    [Fact]
    public void FromIndexData_NoPrefixElement_YieldsAnEmptyPrefix()
    {
        object[] indexData = [0L, 3L];

        var segment = ArchiveSegment.FromIndexData(indexData);

        segment.Prefix.ShouldBeEmpty();
    }

    [Fact]
    public void Equality_SegmentsWithTheSameValues_AreEqual()
    {
        byte[] prefix = [1, 2];

        var first = new ArchiveSegment(10, 20, prefix);
        var second = new ArchiveSegment(10, 20, prefix);

        second.ShouldBe(first);
    }

    [Fact]
    public void With_ChangingOffset_LeavesTheOriginalAlone()
    {
        var original = new ArchiveSegment(10, 20, []);

        var moved = original with { Offset = 99 };

        moved.Offset.ShouldBe(99);
        original.Offset.ShouldBe(10);
    }
}
