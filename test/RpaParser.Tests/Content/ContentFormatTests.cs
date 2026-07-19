using System.Linq;
using RpaParser;
using RpaParser.Content;
using RpaParser.Decompilation;
using Shouldly;

namespace RpaParser.Tests.Content;

public class ContentFormatTests
{
    // The formats under test here never reach for the decompiler.
    private static readonly Decompiler NoDecompiler = new(new DecompilerOptions());

    [Theory]
    [InlineData("art.png")]
    [InlineData("art.jpg")]
    [InlineData("art.webp")]
    [InlineData("art.gif")]
    public void Detect_ImageExtension_ReturnsImageContent(string fileName)
    {
        ContentFormat.Detect(fileName).ShouldBeOfType<ImageContent>();
    }

    [Theory]
    [InlineData("script.rpy")]
    [InlineData("notes.txt")]
    [InlineData("data.json")]
    public void Detect_TextExtension_ReturnsTextContent(string fileName)
    {
        ContentFormat.Detect(fileName).ShouldBeOfType<TextContent>();
    }

    [Theory]
    [InlineData("script.rpyc")]
    [InlineData("script.rpymc")]
    [InlineData("script.rpyc~")]
    public void Detect_CompiledScriptExtension_ReturnsCompiledScriptContent(string fileName)
    {
        ContentFormat.Detect(fileName).ShouldBeOfType<CompiledScriptContent>();
    }

    [Theory]
    [InlineData("track.mp3")]
    [InlineData("track.ogg")]
    public void Detect_AudioExtension_ReturnsAudioContent(string fileName)
    {
        ContentFormat.Detect(fileName).ShouldBeOfType<AudioContent>();
    }

    [Theory]
    [InlineData("clip.webm")]
    [InlineData("clip.mp4")]
    public void Detect_VideoExtension_ReturnsVideoContent(string fileName)
    {
        ContentFormat.Detect(fileName).ShouldBeOfType<VideoContent>();
    }

    [Theory]
    [InlineData("ART.PNG")]
    [InlineData("Script.RpY")]
    [InlineData("Clip.WebM")]
    public void Detect_UppercaseExtension_StillMatches(string fileName)
    {
        ContentFormat.Detect(fileName).ShouldNotBeOfType<UnknownContent>();
    }

    [Theory]
    [InlineData("blob.dat")]
    [InlineData("no-extension")]
    [InlineData("")]
    public void Detect_UnrecognisedName_ReturnsUnknownContent(string fileName)
    {
        ContentFormat.Detect(fileName).ShouldBeOfType<UnknownContent>();
    }

    [Fact]
    public void Detect_NullName_ReturnsUnknownContentRatherThanThrowing()
    {
        ContentFormat.Detect(null).ShouldBeOfType<UnknownContent>();
    }

    [Fact]
    public void Detect_NestedPath_UsesTheExtensionNotTheDirectory()
    {
        ContentFormat.Detect("images/scenes/room.png").ShouldBeOfType<ImageContent>();
    }

    [Fact]
    public void Extensions_AcrossAllFormats_AreClaimedByExactlyOneFormat()
    {
        var claimed = ContentFormat.All.SelectMany(f => f.Extensions).ToList();

        claimed.Distinct().Count().ShouldBe(claimed.Count);
    }

    [Fact]
    public void Extensions_EveryFormat_DeclaresLowercaseExtensionsWithALeadingDot()
    {
        foreach (var extension in ContentFormat.All.SelectMany(f => f.Extensions))
        {
            extension.ShouldStartWith(".");
            extension.ShouldBe(extension.ToLowerInvariant());
        }
    }

    [Fact]
    public void CreatePreview_ImageContent_HandsBackTheBytesUntouched()
    {
        byte[] data = [1, 2, 3, 4];

        var preview = new ImageContent().CreatePreview(data, NoDecompiler);

        preview.Format.ShouldBeOfType<ImageContent>();
        preview.Content.ShouldBe(data);
    }

    [Fact]
    public void CreatePreview_TextContent_DecodesAndNormalisesLineEndings()
    {
        byte[] data = System.Text.Encoding.UTF8.GetBytes("one\r\ntwo");

        var preview = new TextContent().CreatePreview(data, NoDecompiler);

        preview.Format.ShouldBeOfType<TextContent>();
        preview.AsText()
            .ShouldBe("one" + System.Environment.NewLine + "two");
    }

    [Fact]
    public void DisplayName_EveryFormat_IsPresentedToTheUser()
    {
        foreach (var format in ContentFormat.All)
        {
            format.DisplayName.ShouldNotBeNullOrWhiteSpace();
        }
    }
}
