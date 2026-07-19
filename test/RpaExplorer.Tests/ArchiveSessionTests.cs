using RpaParser.Content;
using RpaParser.Formats;
using Shouldly;

namespace RpaExplorer.Tests;

public class ArchiveSessionTests
{
    [Fact]
    public void IsOpen_NewSession_IsFalseUntilAnArchiveIsOpened()
    {
        using var workspace = new SessionWorkspace();

        workspace.Session.IsOpen.ShouldBeFalse();
        workspace.Session.Archive.ShouldBeNull();
        workspace.Session.HasUnsavedChanges.ShouldBeFalse();
    }

    [Fact]
    public void CreateNew_EmptyArchive_CountsAsUnsaved()
    {
        using var workspace = new SessionWorkspace();

        workspace.Session.CreateNew();

        workspace.Session.IsOpen.ShouldBeTrue();
        workspace.Session.Archive!.Index.ShouldBeEmpty();
        workspace.Session.HasUnsavedChanges.ShouldBeTrue();
    }

    [Fact]
    public void Open_ExistingArchive_LoadsItWithNoUnsavedChanges()
    {
        using var workspace = new SessionWorkspace();
        var path = workspace.CreateArchive(new Dictionary<string, string> { ["a.txt"] = "hello" });

        workspace.Session.Open(path);

        workspace.Session.IsOpen.ShouldBeTrue();
        workspace.Session.Archive!.Index.Keys.ShouldContain("a.txt");
        workspace.Session.HasUnsavedChanges.ShouldBeFalse();
    }

    [Fact]
    public void Open_UnreadableFile_LeavesThePreviousArchiveInPlace()
    {
        using var workspace = new SessionWorkspace();
        var session = workspace.OpenedSession(new Dictionary<string, string> { ["keep.txt"] = "x" });

        Should.Throw<Exception>(() => session.Open(workspace.WriteFile("junk.rpa", "not an archive")));

        session.IsOpen.ShouldBeTrue();
        session.Archive!.Index.Keys.ShouldContain("keep.txt");
    }

    [Fact]
    public void Contains_FileInTheArchive_IsTrueOnlyForFilesThatAreThere()
    {
        using var workspace = new SessionWorkspace();
        var session = workspace.OpenedSession();

        session.Contains("readme.txt").ShouldBeTrue();
        session.Contains("absent.txt").ShouldBeFalse();
    }

    [Fact]
    public void Contains_NoArchiveOpen_IsFalseRatherThanThrowing()
    {
        using var workspace = new SessionWorkspace();

        workspace.Session.Contains("readme.txt").ShouldBeFalse();
    }

    [Fact]
    public void Remove_CheckedFiles_DropsThemAndMarksTheArchiveUnsaved()
    {
        using var workspace = new SessionWorkspace();
        var session = workspace.OpenedSession();

        var removed = session.Remove(["readme.txt"]);

        removed.ShouldBeTrue();
        session.Contains("readme.txt").ShouldBeFalse();
        session.HasUnsavedChanges.ShouldBeTrue();
    }

    [Fact]
    public void Remove_NothingMatches_LeavesTheArchiveUnchanged()
    {
        using var workspace = new SessionWorkspace();
        var session = workspace.OpenedSession();

        var removed = session.Remove(["absent.txt"]);

        removed.ShouldBeFalse();
        session.HasUnsavedChanges.ShouldBeFalse();
    }

    [Fact]
    public void AddFiles_SingleFile_StagesItUnderItsOwnName()
    {
        using var workspace = new SessionWorkspace();
        var session = workspace.OpenedSession();
        var file = workspace.WriteFile("staging/extra.txt", "new");

        session.AddFiles([file]);

        session.Contains("extra.txt").ShouldBeTrue();
        session.Archive!.Index["extra.txt"].InArchive.ShouldBeFalse();
        session.HasUnsavedChanges.ShouldBeTrue();
    }

    [Fact]
    public void AddFiles_Directory_KeepsPathsRelativeToTheDirectoryDropped()
    {
        using var workspace = new SessionWorkspace();
        var session = workspace.OpenedSession();
        workspace.WriteFile("drop/audio/theme.ogg", "x");
        workspace.WriteFile("drop/audio/nested/sting.ogg", "y");

        session.AddFiles([workspace.Path_("drop", "audio")]);

        session.Contains("audio/theme.ogg").ShouldBeTrue();
        session.Contains("audio/nested/sting.ogg").ShouldBeTrue();
    }

    [Fact]
    public void AddFiles_NameAlreadyInTheArchive_ReplacesTheStoredEntry()
    {
        using var workspace = new SessionWorkspace();
        var session = workspace.OpenedSession();
        var replacement = workspace.WriteFile("staging/readme.txt", "replaced");

        session.AddFiles([replacement]);

        session.Archive!.Index["readme.txt"].InArchive.ShouldBeFalse();
        session.Archive.Index.Count(entry => entry.Key == "readme.txt").ShouldBe(1);
    }

    [Fact]
    public void AddFiles_NoArchiveOpen_Throws()
    {
        using var workspace = new SessionWorkspace();
        var file = workspace.WriteFile("a.txt", "x");

        Should.Throw<InvalidOperationException>(() => workspace.Session.AddFiles([file]));
    }

    [Fact]
    public void Save_AddedFile_IsReadableFromTheSavedArchive()
    {
        using var workspace = new SessionWorkspace();
        var session = workspace.OpenedSession();
        session.AddFiles([workspace.WriteFile("staging/extra.txt", "new content")]);

        var written = session.Save(workspace.Path_("saved.rpa"));
        session.Open(written);

        session.Contains("extra.txt").ShouldBeTrue();
        session.Archive!.Read("extra.txt").ShouldBe("new content"u8.ToArray());
        session.HasUnsavedChanges.ShouldBeFalse();
    }

    [Fact]
    public void Export_CheckedFiles_WritesThemUnderTheDestination()
    {
        using var workspace = new SessionWorkspace();
        var session = workspace.OpenedSession();
        var destination = workspace.Path_("out");
        Directory.CreateDirectory(destination);

        session.Export(["readme.txt", "images/logo.png"], destination);

        File.ReadAllText(Path.Combine(destination, "readme.txt")).ShouldBe("hello");
        File.Exists(Path.Combine(destination, "images", "logo.png")).ShouldBeTrue();
    }

    [Fact]
    public void Export_EveryFile_IsReportedOnceInOrder()
    {
        using var workspace = new SessionWorkspace();
        var session = workspace.OpenedSession();
        var destination = workspace.Path_("out");
        Directory.CreateDirectory(destination);

        Recorder progress = new();
        session.Export(["readme.txt", "images/logo.png"], destination, progress);

        progress.Reported.Select(p => p.TreePath).ShouldBe(["readme.txt", "images/logo.png"]);
        progress.Reported.Select(p => p.Done).ShouldBe([1, 2]);
        progress.Reported.ShouldAllBe(p => p.Total == 2);
    }

    [Fact]
    public void Export_CancelledAfterTheFirstFile_StopsAndLeavesThatFileWhole()
    {
        using var workspace = new SessionWorkspace();
        var session = workspace.OpenedSession();
        var destination = workspace.Path_("out");
        Directory.CreateDirectory(destination);

        using CancellationTokenSource cancellation = new();
        Recorder progress = new(_ => cancellation.Cancel());

        session.Export(["readme.txt", "images/logo.png"], destination, progress, cancellation.Token);

        File.ReadAllText(Path.Combine(destination, "readme.txt")).ShouldBe("hello");
        File.Exists(Path.Combine(destination, "images", "logo.png")).ShouldBeFalse();
    }

    [Fact]
    public void FolderSizes_NestedFolders_TotalEverythingBeneathThem()
    {
        using var workspace = new SessionWorkspace();
        var session = workspace.OpenedSession(new Dictionary<string, string>
        {
            ["a.txt"] = "12345",
            ["dir/b.txt"] = "123",
            ["dir/deeper/c.txt"] = "12"
        });

        var sizes = session.FolderSizes();

        sizes[string.Empty].ShouldBe(10);
        sizes["dir"].ShouldBe(5);
        sizes["dir/deeper"].ShouldBe(2);
    }

    [Fact]
    public void FolderSizes_NoArchiveOpen_ReportsAnEmptyArchive()
    {
        using var workspace = new SessionWorkspace();

        workspace.Session.FolderSizes()[string.Empty].ShouldBe(0);
    }

    [Fact]
    public void FolderSizes_FilesOnlyAtTheRoot_NameNoFolders()
    {
        using var workspace = new SessionWorkspace();
        var session = workspace.OpenedSession(new Dictionary<string, string> { ["a.txt"] = "abc" });

        var sizes = session.FolderSizes();

        sizes.Keys.ShouldBe([string.Empty]);
        sizes[string.Empty].ShouldBe(3);
    }

    [Fact]
    public void Preview_TextFile_ReturnsItsContent()
    {
        using var workspace = new SessionWorkspace();
        var session = workspace.OpenedSession();

        var preview = session.Preview("readme.txt");

        preview.Format.ShouldBeOfType<TextContent>();
        preview.AsText().ShouldBe("hello");
    }

    [Fact]
    public void PreviewRaw_TextFile_ReturnsBytesRatherThanText()
    {
        using var workspace = new SessionWorkspace();
        var session = workspace.OpenedSession();

        var preview = session.PreviewRaw("readme.txt");

        preview.AsBytes().ShouldBe("hello"u8.ToArray());
    }

    [Fact]
    public void Preview_FileNotInTheArchive_ReportsItAsMissing()
    {
        using var workspace = new SessionWorkspace();
        var session = workspace.OpenedSession();

        session.Preview("absent.txt").Format.ShouldBeOfType<UnknownContent>();
    }

    [Fact]
    public void UsePython_PathChosen_AppliesImmediatelyAndIsRemembered()
    {
        using var workspace = new SessionWorkspace();
        var interpreter = workspace.WriteFile("python3", string.Empty);

        // Deliberately before any archive is open: configuring a tool used to do nothing
        // until the next load.
        workspace.Session.UsePython(interpreter);

        workspace.Session.DecompilerOptions.PythonPath.ShouldBe(interpreter);
        workspace.Settings.GetPython().ShouldBe(interpreter);
    }

    [Fact]
    public void UseUnrpyc_PathChosen_AppliesImmediatelyAndIsRemembered()
    {
        using var workspace = new SessionWorkspace();
        var script = workspace.WriteFile("unrpyc.py", string.Empty);

        workspace.Session.UseUnrpyc(script);

        workspace.Session.DecompilerOptions.UnrpycPath.ShouldBe(script);
        workspace.Settings.GetUnrpyc().ShouldBe(script);
    }

    [Fact]
    public void Open_ToolsConfigured_CarriesThemOntoTheNewArchive()
    {
        using var workspace = new SessionWorkspace();
        var interpreter = workspace.WriteFile("python3", string.Empty);
        workspace.Session.UsePython(interpreter);

        workspace.Session.Open(workspace.CreateArchive(new Dictionary<string, string> { ["a.txt"] = "x" }));

        workspace.Session.DecompilerOptions.PythonPath.ShouldBe(interpreter);
    }

    /// <summary>
    /// Records progress on the calling thread. Progress&lt;T&gt; posts to a synchronisation
    /// context, so in a test it would report after the assertions have already run.
    /// </summary>
    private sealed class Recorder(Action<ExportProgress>? onReport = null) : IProgress<ExportProgress>
    {
        public List<ExportProgress> Reported { get; } = [];

        public void Report(ExportProgress value)
        {
            Reported.Add(value);
            onReport?.Invoke(value);
        }
    }
}
