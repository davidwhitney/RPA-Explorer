using System.Text;
using Shouldly;

namespace RpaParser.Tests;

public class ZlibTests
{
    [Fact]
    public void CompressBuffer_TextInput_ProducesZlibHeader()
    {
        var raw = Encoding.UTF8.GetBytes("label start:\n    return\n");

        var compressed = Zlib.CompressBuffer(raw);

        // RFC1950 zlib streams begin with 0x78 for a 32K window
        compressed.Length.ShouldBeGreaterThan(0);
        compressed[0].ShouldBe((byte) 0x78);
    }

    [Fact]
    public void UncompressBuffer_CompressedInput_RoundTripsExactly()
    {
        var raw = Encoding.UTF8.GetBytes("the quick brown fox jumps over the lazy dog");

        var result = Zlib.UncompressBuffer(Zlib.CompressBuffer(raw));

        result.ShouldBe(raw);
    }

    [Fact]
    public void UncompressBuffer_LargeBinaryInput_RoundTripsExactly()
    {
        var raw = new byte[256 * 1024];
        new Random(1234).NextBytes(raw);

        var result = Zlib.UncompressBuffer(Zlib.CompressBuffer(raw));

        result.ShouldBe(raw);
    }

    [Fact]
    public void CompressBuffer_EmptyInput_RoundTripsToEmpty()
    {
        var raw = Array.Empty<byte>();

        var result = Zlib.UncompressBuffer(Zlib.CompressBuffer(raw));

        result.ShouldBeEmpty();
    }

    [Fact]
    public void CompressBuffer_HighlyRepetitiveInput_ProducesSmallerOutput()
    {
        var raw = Enumerable.Repeat((byte) 'a', 10_000).ToArray();

        var compressed = Zlib.CompressBuffer(raw);

        compressed.Length.ShouldBeLessThan(raw.Length);
    }

    [Fact]
    public void UncompressBuffer_InputIsNotZlib_Throws()
    {
        byte[] notZlib = { 0x00, 0x01, 0x02, 0x03 };

        Should.Throw<Exception>(() => Zlib.UncompressBuffer(notZlib));
    }
}
