namespace RpaParser;

/// <summary>Where a file ended up when an archive was written.</summary>
public sealed record StoredFile(string TreePath, long Offset, long Length);