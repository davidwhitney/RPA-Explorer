using System;
using System.Linq;
using System.Text;
using RpaParser;
using Shouldly;

namespace RpaParser.Tests;

public class ZlibTests
{
    [Fact]
    public void CompressBuffer_TextInput_ProducesZlibHeader()
    {
        byte[] raw = Encoding.UTF8.GetBytes("label start:\n    return\n");

        byte[] compressed = Zlib.CompressBuffer(raw);

        // RFC1950 zlib streams begin with 0x78 for a 32K window
        compressed.Length.ShouldBeGreaterThan(0);
        compressed[0].ShouldBe((byte) 0x78);
    }

    [Fact]
    public void UncompressBuffer_CompressedInput_RoundTripsExactly()
    {
        byte[] raw = Encoding.UTF8.GetBytes("the quick brown fox jumps over the lazy dog");

        byte[] result = Zlib.UncompressBuffer(Zlib.CompressBuffer(raw));

        result.ShouldBe(raw);
    }

    [Fact]
    public void UncompressBuffer_LargeBinaryInput_RoundTripsExactly()
    {
        byte[] raw = new byte[256 * 1024];
        new Random(1234).NextBytes(raw);

        byte[] result = Zlib.UncompressBuffer(Zlib.CompressBuffer(raw));

        result.ShouldBe(raw);
    }

    [Fact]
    public void CompressBuffer_EmptyInput_RoundTripsToEmpty()
    {
        byte[] raw = Array.Empty<byte>();

        byte[] result = Zlib.UncompressBuffer(Zlib.CompressBuffer(raw));

        result.ShouldBeEmpty();
    }

    [Fact]
    public void CompressBuffer_HighlyRepetitiveInput_ProducesSmallerOutput()
    {
        byte[] raw = Enumerable.Repeat((byte) 'a', 10_000).ToArray();

        byte[] compressed = Zlib.CompressBuffer(raw);

        compressed.Length.ShouldBeLessThan(raw.Length);
    }

    [Fact]
    public void UncompressBuffer_InputIsNotZlib_Throws()
    {
        byte[] notZlib = { 0x00, 0x01, 0x02, 0x03 };

        Should.Throw<Exception>(() => Zlib.UncompressBuffer(notZlib));
    }
}
