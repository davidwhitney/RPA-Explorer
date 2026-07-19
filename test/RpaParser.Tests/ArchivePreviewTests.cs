using System;
using System.Collections.Generic;
using System.Text;
using RpaParser;
using RpaParser.Content;
using RpaParser.Decompilation;
using RpaParser.Formats;
using RpaParser.Previews;
using Shouldly;

namespace RpaParser.Tests;

public class ArchivePreviewTests
{
    private static Archive ArchiveContaining(TempWorkspace workspace, string entryName, byte[] content) =>
        workspace.LoadArchive(
            ArchiveFormat.Rpa3,
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
        var archive = ArchiveContaining(workspace, entryName, content);

        PreviewResult preview = archive.Preview(entryName);

        preview.Format.ShouldBeOfType<ImageContent>();
        preview.AsBytes().ShouldBe(content);
    }

    [Theory]
    [InlineData("notes.txt")]
    [InlineData("script.rpy")]
    [InlineData("data.json")]
    [InlineData("page.HTML")]
    public void GetPreview_TextExtension_ReturnsDecodedText(string entryName)
    {
        using var workspace = new TempWorkspace();
        var archive = ArchiveContaining(workspace, entryName, Encoding.UTF8.GetBytes("hello world"));

        PreviewResult preview = archive.Preview(entryName);

        preview.Format.ShouldBeOfType<TextContent>();
        preview.AsText().ShouldBe("hello world");
    }

    [Theory]
    [InlineData("track.mp3")]
    [InlineData("track.ogg")]
    [InlineData("track.FLAC")]
    public void GetPreview_AudioExtension_ReturnsAudioTypeWithRawBytes(string entryName)
    {
        using var workspace = new TempWorkspace();
        byte[] content = { 1, 2, 3 };
        var archive = ArchiveContaining(workspace, entryName, content);

        PreviewResult preview = archive.Preview(entryName);

        preview.Format.ShouldBeOfType<AudioContent>();
        preview.AsBytes().ShouldBe(content);
    }

    [Theory]
    [InlineData("clip.webm")]
    [InlineData("clip.mp4")]
    [InlineData("clip.AVI")]
    public void GetPreview_VideoExtension_ReturnsVideoTypeWithRawBytes(string entryName)
    {
        using var workspace = new TempWorkspace();
        byte[] content = { 4, 5, 6 };
        var archive = ArchiveContaining(workspace, entryName, content);

        PreviewResult preview = archive.Preview(entryName);

        preview.Format.ShouldBeOfType<VideoContent>();
        preview.AsBytes().ShouldBe(content);
    }

    [Fact]
    public void GetPreview_UnrecognisedExtension_ReturnsUnknownTypeWithRawBytes()
    {
        using var workspace = new TempWorkspace();
        byte[] content = { 0xDE, 0xAD };
        var archive = ArchiveContaining(workspace, "blob.dat", content);

        PreviewResult preview = archive.Preview("blob.dat");

        preview.Format.ShouldBeOfType<UnknownContent>();
        preview.AsBytes().ShouldBe(content);
    }

    [Fact]
    public void GetPreview_FileNotInIndex_ReturnsTheMissingPreview()
    {
        using var workspace = new TempWorkspace();
        var archive = ArchiveContaining(workspace, "a.txt", Encoding.UTF8.GetBytes("a"));

        PreviewResult preview = archive.Preview("absent.txt");

        preview.ShouldBeSameAs(PreviewResult.Missing);
        preview.Format.ShouldBeOfType<UnknownContent>();
        preview.AsBytes().ShouldBeEmpty();
        preview.AsText().ShouldBeEmpty();
    }

    [Fact]
    public void GetPreview_RawRequestedForTextFile_ReturnsBytesInsteadOfString()
    {
        using var workspace = new TempWorkspace();
        var content = Encoding.UTF8.GetBytes("raw please");
        var archive = ArchiveContaining(workspace, "a.txt", content);

        PreviewResult preview = archive.PreviewRaw("a.txt");

        preview.Format.ShouldBeOfType<TextContent>();
        preview.AsBytes().ShouldBe(content);
    }

    [Fact]
    public void GetPreviewRaw_TextFile_ReturnsTypeAndBytes()
    {
        using var workspace = new TempWorkspace();
        var content = Encoding.UTF8.GetBytes("raw accessor");
        var archive = ArchiveContaining(workspace, "a.txt", content);

        PreviewResult preview = archive.PreviewRaw("a.txt");

        preview.Format.ShouldBeOfType<TextContent>();
        preview.Content.ShouldBe(content);
    }

    [Theory]
    [InlineData("windows\r\nline\r\nendings")]
    [InlineData("unix\nline\nendings")]
    [InlineData("mac\rline\rendings")]
    [InlineData("no line endings at all")]
    public void GetPreview_TextWithAnyLineEndingStyle_NormalisesToEnvironmentNewline(string raw)
    {
        using var workspace = new TempWorkspace();
        var archive = ArchiveContaining(workspace, "a.txt", Encoding.UTF8.GetBytes(raw));

        var text = archive.Preview("a.txt").AsText();

        // Every separator that survives must be the platform's own.
        text.Replace(Environment.NewLine, string.Empty).ShouldNotContain("\r");
        text.Split(Environment.NewLine).Length.ShouldBe(raw.Contains("no line") ? 1 : 3);
    }

    [Fact]
    public void GetPreview_CompiledScriptWithoutPythonConfigured_ThrowsWithGuidance()
    {
        using var workspace = new TempWorkspace();
        var archive = ArchiveContaining(workspace, "code.rpyc", new byte[] { 1, 2, 3 });
        var options = new DecompilerOptions { PythonPath = string.Empty };

        var ex = Should.Throw<Exception>(() => archive.Preview("code.rpyc", options));

        ex.Message.ShouldContain(Decompiler.InfoBanner);
        ex.Message.ShouldContain("Python environment is not defined");
    }

    [Fact]
    public void GetPreview_CompiledScriptWithMissingPython_ThrowsWithGuidance()
    {
        using var workspace = new TempWorkspace();
        var archive = ArchiveContaining(workspace, "code.rpyc", new byte[] { 1, 2, 3 });
        var options = new DecompilerOptions { PythonPath = workspace.Path_("no-such-python") };

        var ex = Should.Throw<Exception>(() => archive.Preview("code.rpyc", options));

        ex.Message.ShouldContain("cannot be found");
    }

    [Fact]
    public void GetPreview_CompiledScriptWithoutUnrpycConfigured_ThrowsWithGuidance()
    {
        using var workspace = new TempWorkspace();
        var archive = ArchiveContaining(workspace, "code.rpyc", new byte[] { 1, 2, 3 });
        var options = new DecompilerOptions { PythonPath = workspace.WriteFile("python", "#!/bin/sh\n"), UnrpycPath = string.Empty };

        var ex = Should.Throw<Exception>(() => archive.Preview("code.rpyc", options));

        ex.Message.ShouldContain("unrpyc script is not defined");
    }

    [Fact]
    public void GetPreview_CompiledScriptWithMissingUnrpyc_ThrowsWithGuidance()
    {
        using var workspace = new TempWorkspace();
        var archive = ArchiveContaining(workspace, "code.rpyc", new byte[] { 1, 2, 3 });
        var options = new DecompilerOptions { PythonPath = workspace.WriteFile("python", "#!/bin/sh\n"), UnrpycPath = workspace.Path_("no-such-unrpyc.py") };

        var ex = Should.Throw<Exception>(() => archive.Preview("code.rpyc", options));

        ex.Message.ShouldContain("cannot be found");
    }

    [Fact]
    public void ParseRpyc_DecompilerFails_ThrowsReportingTheFailure()
    {
        using var workspace = new TempWorkspace();
        var decompiler = new Decompiler(new DecompilerOptions
        {
            // An interpreter that exists but produces no output file.
            PythonPath = "/usr/bin/true",
            UnrpycPath = workspace.WriteFile("unrpyc.py", "print('nothing')")
        });

        var ex = Should.Throw<Exception>(() => decompiler.Decompile(new byte[] { 1, 2, 3 }));

        ex.Message.ShouldContain("Decompilation failed");
    }
}
