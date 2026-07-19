using System.IO;
using System.IO.Compression;

namespace RPA_Parser
{
    // Drop-in replacement for the previously used Ionic.Zlib.ZlibStream helpers.
    // Uses the built-in System.IO.Compression.ZLibStream (RFC 1950, .NET 6+),
    // which reads/writes the same zlib format RenPy archives use.
    internal static class Zlib
    {
        public static byte[] UncompressBuffer(byte[] compressed)
        {
            using MemoryStream input = new MemoryStream(compressed);
            using ZLibStream zlib = new ZLibStream(input, CompressionMode.Decompress);
            using MemoryStream output = new MemoryStream();
            zlib.CopyTo(output);
            return output.ToArray();
        }

        public static byte[] CompressBuffer(byte[] raw)
        {
            using MemoryStream output = new MemoryStream();
            using (ZLibStream zlib = new ZLibStream(output, CompressionLevel.Optimal, true))
            {
                zlib.Write(raw, 0, raw.Length);
            }
            return output.ToArray();
        }
    }
}
