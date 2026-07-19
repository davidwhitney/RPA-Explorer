using System;
using System.Text;

namespace RpaParser;

/// <summary>
/// One run of bytes belonging to a file. RenPy may store a file as several runs, and
/// each may carry a prefix prepended to the bytes read from the archive.
/// </summary>
public sealed record ArchiveSegment(long Offset, long Length, byte[] Prefix)
{
    public static ArchiveSegment FromIndexData(object[] indexData, long obfuscationKey = 0)
    {
        var offset = Convert.ToInt64(indexData.GetValue(0)) ^ obfuscationKey;
        var length = Convert.ToInt64(indexData.GetValue(1)) ^ obfuscationKey;

        return new ArchiveSegment(offset, length, ReadPrefix(indexData));
    }

    private static byte[] ReadPrefix(object[] indexData)
    {
        if (indexData.Length < 3)
        {
            return [];
        }

        // Which of the two the prefix arrives as depends on which tool wrote the archive.
        return indexData.GetValue(2) switch
        {
            byte[] bytes => bytes,
            string text => Encoding.UTF8.GetBytes(text),
            null => [],
            var other => throw new Exception($"Unsupported index prefix of type '{other.GetType().Name}'.")
        };
    }
}