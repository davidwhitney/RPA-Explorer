using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using RpaParser;
using RpaParser.Content;
using RpaParser.Decompilation;
using RpaParser.Formats;
using RpaParser.Previews;
using RpaParser.Tests.Previews;
using Shouldly;

namespace RpaParser.Tests.Decompilation;

/// <summary>
/// Covers the decompilation path by standing in a stub interpreter, so the behaviour can be
/// verified without a real Python installation or a copy of unrpyc.
/// </summary>
public class ArchiveDecompileTests
{
    /// <summary>
    /// Writes an executable stand-in for the Python interpreter. ParseRpyc invokes it as
    /// `interpreter "unrpyc.py" --try-harder "<temp>.rpyc"` and then reads `<temp>.rpy`.
    /// </summary>
    private static string WriteStubInterpreter(TempWorkspace workspace, string name, string body)
    {
        var path = workspace.Path_(name);
        File.WriteAllText(path, "#!/bin/sh\n" + body);
        File.SetUnixFileMode(path,
            UnixFileMode.UserRead | UnixFileMode.UserWrite | UnixFileMode.UserExecute |
            UnixFileMode.GroupRead | UnixFileMode.GroupExecute |
            UnixFileMode.OtherRead | UnixFileMode.OtherExecute);
        return path;
    }

    private static DecompilerOptions StubOptions(TempWorkspace workspace, string interpreterBody) => new()
    {
        PythonPath = WriteStubInterpreter(workspace, "python-stub", interpreterBody),
        UnrpycPath = workspace.WriteFile("unrpyc.py", "# stand-in for unrpyc")
    };

    private static Decompiler StubDecompiler(TempWorkspace workspace, string interpreterBody) =>
        new(StubOptions(workspace, interpreterBody));

    [Fact]
    public void Decompile_UnrpycWritesOutput_ReturnsDecompiledSource()
    {
        if (OperatingSystem.IsWindows())
        {
            return; // the stub interpreter is a shell script
        }

        using var workspace = new TempWorkspace();
        var decompiler = StubDecompiler(workspace,
            "out=\"${3%.rpyc}.rpy\"\nprintf 'label start:\\r\\n    return\\r\\n' > \"$out\"\n");

        var decompiled = decompiler.Decompile(new byte[] { 1, 2, 3 });

        decompiled.ShouldContain("label start:");
        decompiled.ShouldContain("return");
    }

    [Fact]
    public void Decompile_UnrpycWritesCrlfOutput_NormalisesLineEndings()
    {
        if (OperatingSystem.IsWindows())
        {
            return;
        }

        using var workspace = new TempWorkspace();
        var decompiler = StubDecompiler(workspace,
            "out=\"${3%.rpyc}.rpy\"\nprintf 'one\\r\\ntwo\\r\\nthree\\r\\n' > \"$out\"\n");

        var decompiled = decompiler.Decompile(new byte[] { 1 });

        decompiled.Split(Environment.NewLine).Length.ShouldBe(4);
        decompiled.Replace(Environment.NewLine, string.Empty).ShouldBe("onetwothree");
    }

    [Fact]
    public void Decompile_UnrpycSucceeds_LeavesNoTemporaryFilesBehind()
    {
        if (OperatingSystem.IsWindows())
        {
            return;
        }

        using var workspace = new TempWorkspace();
        var decompiler = StubDecompiler(workspace,
            "out=\"${3%.rpyc}.rpy\"\nprintf 'x\\n' > \"$out\"\necho \"$3\" > " +
            workspace.Path_("last-input") + "\n");

        decompiler.Decompile(new byte[] { 1 });

        var tempFileUsed = File.ReadAllText(workspace.Path_("last-input")).Trim();
        File.Exists(tempFileUsed).ShouldBeFalse();
        File.Exists(tempFileUsed.Replace(".rpyc", ".rpy")).ShouldBeFalse();
    }

    [Fact]
    public void Create_CompiledScriptDecompiles_ReturnsTextPreview()
    {
        if (OperatingSystem.IsWindows())
        {
            return;
        }

        using var workspace = new TempWorkspace();
        var archive = workspace.LoadArchive(
            ArchiveFormat.Rpa3,
            new Dictionary<string, byte[]> { ["code.rpyc"] = Encoding.UTF8.GetBytes("compiled") });
        var options = new DecompilerOptions { PythonPath = WriteStubInterpreter(workspace, "python-stub",
            "out=\"${3%.rpyc}.rpy\"\nprintf 'label decompiled:\\n' > \"$out\"\n"), UnrpycPath = workspace.WriteFile("unrpyc.py", "# stand-in") };

        PreviewResult preview = archive.Preview("code.rpyc", options);

        preview.Format.ShouldBeOfType<TextContent>();
        preview.AsText().ShouldContain("label decompiled:");
    }

    [Fact]
    public void Create_CompiledScriptDecompilesToNothing_FallsBackToUnknown()
    {
        if (OperatingSystem.IsWindows())
        {
            return;
        }

        using var workspace = new TempWorkspace();
        var content = Encoding.UTF8.GetBytes("compiled");
        var archive = workspace.LoadArchive(
            ArchiveFormat.Rpa3,
            new Dictionary<string, byte[]> { ["code.rpyc"] = content });
        // Produces an empty .rpy, so there is nothing to show as text.
        var options = new DecompilerOptions { PythonPath = WriteStubInterpreter(workspace, "python-stub",
            "out=\"${3%.rpyc}.rpy\"\n: > \"$out\"\n"), UnrpycPath = workspace.WriteFile("unrpyc.py", "# stand-in") };

        PreviewResult preview = archive.Preview("code.rpyc", options);

        preview.Format.ShouldBeOfType<UnknownContent>();
        preview.AsBytes().ShouldBe(content);
    }

    [Fact]
    public void CreateRaw_CompiledScript_ReturnsRawBytes()
    {
        if (OperatingSystem.IsWindows())
        {
            return;
        }

        using var workspace = new TempWorkspace();
        var content = Encoding.UTF8.GetBytes("compiled");
        var archive = workspace.LoadArchive(
            ArchiveFormat.Rpa3,
            new Dictionary<string, byte[]> { ["code.rpyc"] = content });
        var options = new DecompilerOptions { PythonPath = WriteStubInterpreter(workspace, "python-stub",
            "out=\"${3%.rpyc}.rpy\"\nprintf 'label x:\\n' > \"$out\"\n"), UnrpycPath = workspace.WriteFile("unrpyc.py", "# stand-in") };

        PreviewResult preview = archive.PreviewRaw("code.rpyc", options);

        preview.AsBytes().ShouldBe(content);
    }
}
