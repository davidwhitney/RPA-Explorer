using System;
using System.Collections.Generic;
using System.Text;
using RpaParser;
using Shouldly;

namespace RpaParser.Tests;

public class RpaParserPreviewTests
{
    private static Parser ArchiveContaining(TempWorkspace workspace, string entryName, byte[] content) =>
        workspace.LoadArchive(
            Parser.Version.Rpa3,
            new Dictionary<string, byte[]> { [entryName] = content });

    [Theory]
    [InlineData("picture.png")]
    [InlineData("picture.jpg")]
    [InlineData("picture.webp")]
    [InlineData("picture.GIF")]
    public void GetPreview_ImageExtension_ReturnsImageTypeWithRawBytes(string entryName)
    {
        using var workspace = new TempWorkspace();
        byte[] content = { 0x89, 0x50, 0x4E, 0x47 };
        var parser = ArchiveContaining(workspace, entryName, content);

        KeyValuePair<string, object> preview = parser.GetPreview(entryName);

        preview.Key.ShouldBe(Parser.PreviewTypes.Image);
        preview.Value.ShouldBeOfType<byte[]>().ShouldBe(content);
    }

    [Theory]
    [InlineData("notes.txt")]
    [InlineData("script.rpy")]
    [InlineData("data.json")]
    [InlineData("page.HTML")]
    public void GetPreview_TextExtension_ReturnsDecodedText(string entryName)
    {
        using var workspace = new TempWorkspace();
        var parser = ArchiveContaining(workspace, entryName, Encoding.UTF8.GetBytes("hello world"));

        KeyValuePair<string, object> preview = parser.GetPreview(entryName);

        preview.Key.ShouldBe(Parser.PreviewTypes.Text);
        preview.Value.ShouldBeOfType<string>().ShouldBe("hello world");
    }

    [Theory]
    [InlineData("track.mp3")]
    [InlineData("track.ogg")]
    [InlineData("track.FLAC")]
    public void GetPreview_AudioExtension_ReturnsAudioTypeWithRawBytes(string entryName)
    {
        using var workspace = new TempWorkspace();
        byte[] content = { 1, 2, 3 };
        var parser = ArchiveContaining(workspace, entryName, content);

        KeyValuePair<string, object> preview = parser.GetPreview(entryName);

        preview.Key.ShouldBe(Parser.PreviewTypes.Audio);
        preview.Value.ShouldBeOfType<byte[]>().ShouldBe(content);
    }

    [Theory]
    [InlineData("clip.webm")]
    [InlineData("clip.mp4")]
    [InlineData("clip.AVI")]
    public void GetPreview_VideoExtension_ReturnsVideoTypeWithRawBytes(string entryName)
    {
        using var workspace = new TempWorkspace();
        byte[] content = { 4, 5, 6 };
        var parser = ArchiveContaining(workspace, entryName, content);

        KeyValuePair<string, object> preview = parser.GetPreview(entryName);

        preview.Key.ShouldBe(Parser.PreviewTypes.Video);
        preview.Value.ShouldBeOfType<byte[]>().ShouldBe(content);
    }

    [Fact]
    public void GetPreview_UnrecognisedExtension_ReturnsUnknownTypeWithRawBytes()
    {
        using var workspace = new TempWorkspace();
        byte[] content = { 0xDE, 0xAD };
        var parser = ArchiveContaining(workspace, "blob.dat", content);

        KeyValuePair<string, object> preview = parser.GetPreview("blob.dat");

        preview.Key.ShouldBe(Parser.PreviewTypes.Unknown);
        preview.Value.ShouldBeOfType<byte[]>().ShouldBe(content);
    }

    [Fact]
    public void GetPreview_FileNotInIndex_ReturnsUnknownWithNullValue()
    {
        using var workspace = new TempWorkspace();
        var parser = ArchiveContaining(workspace, "a.txt", Encoding.UTF8.GetBytes("a"));

        KeyValuePair<string, object> preview = parser.GetPreview("absent.txt");

        preview.Key.ShouldBe(Parser.PreviewTypes.Unknown);
        preview.Value.ShouldBeNull();
    }

    [Fact]
    public void GetPreview_RawRequestedForTextFile_ReturnsBytesInsteadOfString()
    {
        using var workspace = new TempWorkspace();
        var content = Encoding.UTF8.GetBytes("raw please");
        var parser = ArchiveContaining(workspace, "a.txt", content);

        KeyValuePair<string, object> preview = parser.GetPreview("a.txt", true);

        preview.Key.ShouldBe(Parser.PreviewTypes.Text);
        preview.Value.ShouldBeOfType<byte[]>().ShouldBe(content);
    }

    [Fact]
    public void GetPreviewRaw_TextFile_ReturnsTypeAndBytes()
    {
        using var workspace = new TempWorkspace();
        var content = Encoding.UTF8.GetBytes("raw accessor");
        var parser = ArchiveContaining(workspace, "a.txt", content);

        KeyValuePair<string, byte[]> preview = parser.GetPreviewRaw("a.txt");

        preview.Key.ShouldBe(Parser.PreviewTypes.Text);
        preview.Value.ShouldBe(content);
    }

    [Theory]
    [InlineData("windows\r\nline\r\nendings")]
    [InlineData("unix\nline\nendings")]
    [InlineData("mac\rline\rendings")]
    [InlineData("no line endings at all")]
    public void GetPreview_TextWithAnyLineEndingStyle_NormalisesToEnvironmentNewline(string raw)
    {
        using var workspace = new TempWorkspace();
        var parser = ArchiveContaining(workspace, "a.txt", Encoding.UTF8.GetBytes(raw));

        var text = (string) parser.GetPreview("a.txt").Value;

        // Every separator that survives must be the platform's own.
        text.Replace(Environment.NewLine, string.Empty).ShouldNotContain("\r");
        text.Split(Environment.NewLine).Length.ShouldBe(raw.Contains("no line") ? 1 : 3);
    }

    [Fact]
    public void GetPreview_CompiledScriptWithoutPythonConfigured_ThrowsWithGuidance()
    {
        using var workspace = new TempWorkspace();
        var parser = ArchiveContaining(workspace, "code.rpyc", new byte[] { 1, 2, 3 });
        parser.PythonLocation = string.Empty;

        var ex = Should.Throw<Exception>(() => parser.GetPreview("code.rpyc"));

        ex.Message.ShouldContain(parser.RpycInfoBanner);
        ex.Message.ShouldContain("Python environment is not defined");
    }

    [Fact]
    public void GetPreview_CompiledScriptWithMissingPython_ThrowsWithGuidance()
    {
        using var workspace = new TempWorkspace();
        var parser = ArchiveContaining(workspace, "code.rpyc", new byte[] { 1, 2, 3 });
        parser.PythonLocation = workspace.Path_("no-such-python");

        var ex = Should.Throw<Exception>(() => parser.GetPreview("code.rpyc"));

        ex.Message.ShouldContain("cannot be found");
    }

    [Fact]
    public void GetPreview_CompiledScriptWithoutUnrpycConfigured_ThrowsWithGuidance()
    {
        using var workspace = new TempWorkspace();
        var parser = ArchiveContaining(workspace, "code.rpyc", new byte[] { 1, 2, 3 });
        parser.PythonLocation = workspace.WriteFile("python", "#!/bin/sh\n");
        parser.UnrpycLocation = string.Empty;

        var ex = Should.Throw<Exception>(() => parser.GetPreview("code.rpyc"));

        ex.Message.ShouldContain("unrpyc script is not defined");
    }

    [Fact]
    public void GetPreview_CompiledScriptWithMissingUnrpyc_ThrowsWithGuidance()
    {
        using var workspace = new TempWorkspace();
        var parser = ArchiveContaining(workspace, "code.rpyc", new byte[] { 1, 2, 3 });
        parser.PythonLocation = workspace.WriteFile("python", "#!/bin/sh\n");
        parser.UnrpycLocation = workspace.Path_("no-such-unrpyc.py");

        var ex = Should.Throw<Exception>(() => parser.GetPreview("code.rpyc"));

        ex.Message.ShouldContain("cannot be found");
    }

    [Fact]
    public void ParseRpyc_DecompilerFails_ThrowsReportingTheFailure()
    {
        using var workspace = new TempWorkspace();
        var parser = new Parser
        {
            // An interpreter that exists but produces no output file.
            PythonLocation = "/usr/bin/true",
            UnrpycLocation = workspace.WriteFile("unrpyc.py", "print('nothing')")
        };

        var ex = Should.Throw<Exception>(() => parser.ParseRpyc(new byte[] { 1, 2, 3 }));

        ex.Message.ShouldContain("Decompilation failed");
    }
}
