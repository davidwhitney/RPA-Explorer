using RpaParser;
using RpaParser.Formats;

namespace RpaExplorer.Tests;

/// <summary>
/// An isolated temporary directory plus a session pointed at a settings file inside it, so
/// a test never reads or writes the settings of the machine running it.
/// </summary>
public sealed class SessionWorkspace : IDisposable
{
    public string Root { get; }

    public SessionWorkspace()
    {
        Root = Path.Combine(Path.GetTempPath(), "rpa-session-tests-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(Root);
    }

    private Settings? _settings;
    private ArchiveSession? _session;

    public Settings Settings => _settings ??= new Settings(Path_("settings.ini"));

    public ArchiveSession Session => _session ??= new ArchiveSession(Settings);

    public string Path_(params string[] parts) => Path.Combine(Root, Path.Combine(parts));

    public string WriteFile(string relativePath, string content)
    {
        var full = Path_(relativePath.Replace('/', Path.DirectorySeparatorChar));
        Directory.CreateDirectory(Path.GetDirectoryName(full)!);
        File.WriteAllText(full, content);
        return full;
    }

    /// <summary>
    /// Writes a real archive containing <paramref name="entries"/>, keyed by the path each
    /// should have inside it, and returns where it landed.
    /// </summary>
    public string CreateArchive(IDictionary<string, string> entries, string archiveName = "test.rpa")
    {
        var archive = new Archive(ArchiveFormat.Rpa3);
        var source = Path_("source-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(source);

        foreach (var entry in entries)
        {
            var onDisk = Path.Combine(source, entry.Key.Replace('/', Path.DirectorySeparatorChar));
            Directory.CreateDirectory(Path.GetDirectoryName(onDisk)!);
            File.WriteAllText(onDisk, entry.Value);

            archive.Index.Add(entry.Key, ArchiveEntry.FromFilename(onDisk, source));
        }

        return archive.Save(Path_(archiveName));
    }

    /// <summary>A session with an archive already open, which most operations require.</summary>
    public ArchiveSession OpenedSession(IDictionary<string, string>? entries = null)
    {
        Session.Open(CreateArchive(entries ?? new Dictionary<string, string>
        {
            ["readme.txt"] = "hello",
            ["images/logo.png"] = "not really a png",
            ["images/scenes/room.png"] = "also not a png"
        }));

        return Session;
    }

    public void Dispose()
    {
        try
        {
            if (Directory.Exists(Root))
            {
                Directory.Delete(Root, true);
            }
        }
        catch
        {
            // A leftover temp directory must never fail a test run.
        }
    }
}
