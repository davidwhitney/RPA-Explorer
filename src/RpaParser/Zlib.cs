using System.IO;
using System.IO.Compression;

namespace RpaParser
{
    // Drop-in replacement for the previously used Ionic.Zlib.ZlibStream helpers.
    // Uses the built-in System.IO.Compression.ZLibStream (RFC 1950, .NET 6+),
    // which reads/writes the same zlib format RenPy archives use.
    internal static class Zlib
    {
        public static byte[] UncompressBuffer(byte[] compressed)
        {
            using var input = new MemoryStream(compressed);
            using var zlib = new ZLibStream(input, CompressionMode.Decompress);
            using var output = new MemoryStream();
            zlib.CopyTo(output);
            return output.ToArray();
        }

        public static byte[] CompressBuffer(byte[] raw)
        {
            using var output = new MemoryStream();
            using (var zlib = new ZLibStream(output, CompressionLevel.Optimal, true))
            {
                zlib.Write(raw, 0, raw.Length);
            }
            return output.ToArray();
        }
    }
}
