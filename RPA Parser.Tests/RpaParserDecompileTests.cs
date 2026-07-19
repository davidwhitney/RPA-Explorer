using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using RPA_Parser;
using Shouldly;

namespace RPAParser.Tests;

/// <summary>
/// Covers the decompilation path by standing in a stub interpreter, so the behaviour can be
/// verified without a real Python installation or a copy of unrpyc.
/// </summary>
public class RpaParserDecompileTests
{
    /// <summary>
    /// Writes an executable stand-in for the Python interpreter. ParseRpyc invokes it as
    /// `interpreter "unrpyc.py" --try-harder "<temp>.rpyc"` and then reads `<temp>.rpy`.
    /// </summary>
    private static string WriteStubInterpreter(TempWorkspace workspace, string name, string body)
    {
        string path = workspace.Path_(name);
        File.WriteAllText(path, "#!/bin/sh\n" + body);
        File.SetUnixFileMode(path,
            UnixFileMode.UserRead | UnixFileMode.UserWrite | UnixFileMode.UserExecute |
            UnixFileMode.GroupRead | UnixFileMode.GroupExecute |
            UnixFileMode.OtherRead | UnixFileMode.OtherExecute);
        return path;
    }

    private static RpaParser ParserWithStub(TempWorkspace workspace, string interpreterBody)
    {
        return new RpaParser
        {
            PythonLocation = WriteStubInterpreter(workspace, "python-stub", interpreterBody),
            UnrpycLocation = workspace.WriteFile("unrpyc.py", "# stand-in for unrpyc")
        };
    }

    [Fact]
    public void ParseRpyc_DecompilerWritesOutput_ReturnsDecompiledSource()
    {
        if (OperatingSystem.IsWindows())
        {
            return; // the stub interpreter is a shell script
        }

        using TempWorkspace workspace = new TempWorkspace();
        RpaParser parser = ParserWithStub(workspace,
            "out=\"${3%.rpyc}.rpy\"\nprintf 'label start:\\r\\n    return\\r\\n' > \"$out\"\n");

        string decompiled = parser.ParseRpyc(new byte[] { 1, 2, 3 });

        decompiled.ShouldContain("label start:");
        decompiled.ShouldContain("return");
    }

    [Fact]
    public void ParseRpyc_DecompilerWritesCrlfOutput_NormalisesLineEndings()
    {
        if (OperatingSystem.IsWindows())
        {
            return;
        }

        using TempWorkspace workspace = new TempWorkspace();
        RpaParser parser = ParserWithStub(workspace,
            "out=\"${3%.rpyc}.rpy\"\nprintf 'one\\r\\ntwo\\r\\nthree\\r\\n' > \"$out\"\n");

        string decompiled = parser.ParseRpyc(new byte[] { 1 });

        decompiled.Split(Environment.NewLine).Length.ShouldBe(4);
        decompiled.Replace(Environment.NewLine, string.Empty).ShouldBe("onetwothree");
    }

    [Fact]
    public void ParseRpyc_DecompilerLeavesNoTemporaryFilesBehind()
    {
        if (OperatingSystem.IsWindows())
        {
            return;
        }

        using TempWorkspace workspace = new TempWorkspace();
        RpaParser parser = ParserWithStub(workspace,
            "out=\"${3%.rpyc}.rpy\"\nprintf 'x\\n' > \"$out\"\necho \"$3\" > " +
            workspace.Path_("last-input") + "\n");

        parser.ParseRpyc(new byte[] { 1 });

        string tempFileUsed = File.ReadAllText(workspace.Path_("last-input")).Trim();
        File.Exists(tempFileUsed).ShouldBeFalse();
        File.Exists(tempFileUsed.Replace(".rpyc", ".rpy")).ShouldBeFalse();
    }

    [Fact]
    public void GetPreview_CompiledScriptDecompiles_ReturnsTextPreview()
    {
        if (OperatingSystem.IsWindows())
        {
            return;
        }

        using TempWorkspace workspace = new TempWorkspace();
        RpaParser parser = workspace.LoadArchive(
            RpaParser.Version.Rpa3,
            new Dictionary<string, byte[]> { ["code.rpyc"] = Encoding.UTF8.GetBytes("compiled") });
        parser.PythonLocation = WriteStubInterpreter(workspace, "python-stub",
            "out=\"${3%.rpyc}.rpy\"\nprintf 'label decompiled:\\n' > \"$out\"\n");
        parser.UnrpycLocation = workspace.WriteFile("unrpyc.py", "# stand-in");

        KeyValuePair<string, object> preview = parser.GetPreview("code.rpyc");

        preview.Key.ShouldBe(RpaParser.PreviewTypes.Text);
        preview.Value.ShouldBeOfType<string>().ShouldContain("label decompiled:");
    }

    [Fact]
    public void GetPreview_CompiledScriptDecompilesToNothing_FallsBackToUnknown()
    {
        if (OperatingSystem.IsWindows())
        {
            return;
        }

        using TempWorkspace workspace = new TempWorkspace();
        byte[] content = Encoding.UTF8.GetBytes("compiled");
        RpaParser parser = workspace.LoadArchive(
            RpaParser.Version.Rpa3,
            new Dictionary<string, byte[]> { ["code.rpyc"] = content });
        // Produces an empty .rpy, so there is nothing to show as text.
        parser.PythonLocation = WriteStubInterpreter(workspace, "python-stub",
            "out=\"${3%.rpyc}.rpy\"\n: > \"$out\"\n");
        parser.UnrpycLocation = workspace.WriteFile("unrpyc.py", "# stand-in");

        KeyValuePair<string, object> preview = parser.GetPreview("code.rpyc");

        preview.Key.ShouldBe(RpaParser.PreviewTypes.Unknown);
        preview.Value.ShouldBeOfType<byte[]>().ShouldBe(content);
    }

    [Fact]
    public void GetPreview_CompiledScriptRawRequested_ReturnsRawBytes()
    {
        if (OperatingSystem.IsWindows())
        {
            return;
        }

        using TempWorkspace workspace = new TempWorkspace();
        byte[] content = Encoding.UTF8.GetBytes("compiled");
        RpaParser parser = workspace.LoadArchive(
            RpaParser.Version.Rpa3,
            new Dictionary<string, byte[]> { ["code.rpyc"] = content });
        parser.PythonLocation = WriteStubInterpreter(workspace, "python-stub",
            "out=\"${3%.rpyc}.rpy\"\nprintf 'label x:\\n' > \"$out\"\n");
        parser.UnrpycLocation = workspace.WriteFile("unrpyc.py", "# stand-in");

        KeyValuePair<string, object> preview = parser.GetPreview("code.rpyc", true);

        preview.Value.ShouldBeOfType<byte[]>().ShouldBe(content);
    }
}
