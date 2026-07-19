using RpaParser;

namespace RpaParser.Tests;

/// <summary>
/// Previews are built by a factory that owns the decompiler settings, so tests get a
/// short way of asking for one against an archive.
/// </summary>
internal static class PreviewExtensions
{
    public static PreviewResult Preview(this Archive archive, string fileName, DecompilerOptions options = null) =>
        new PreviewFactory(options ?? new DecompilerOptions()).Create(archive, fileName);

    public static PreviewResult PreviewRaw(this Archive archive, string fileName, DecompilerOptions options = null) =>
        new PreviewFactory(options ?? new DecompilerOptions()).CreateRaw(archive, fileName);
}
