using System.Collections.Generic;
using RpaParser.Decompilation;
using RpaParser.Previews;

namespace RpaParser.Content;

/// <summary>
/// Compiled RenPy scripts, which have to go through the external decompiler. When it
/// produces nothing the file is presented as unknown rather than as empty text.
/// </summary>
public sealed class CompiledScriptContent : ContentFormat
{
    public override string DisplayName => "Compiled script";

    public override IReadOnlyList<string> Extensions { get; } =
        [".rpyc~", ".rpyc", ".rpymc~", ".rpymc"];

    public override PreviewResult CreatePreview(byte[] data, Decompiler decompiler)
    {
        var decompiled = decompiler.Decompile(data);

        return decompiled == string.Empty
            ? new PreviewResult(Unknown, data)
            : new PreviewResult(Text, decompiled);
    }
}