using RpaParser.Decompilation;
using Shouldly;

namespace RpaParser.Tests.Decompilation;

/// <summary>
/// Drives the interpreter search against a controlled environment, so the Windows paths,
/// pyenv layouts and failure handling can all be verified regardless of the host machine.
/// </summary>
public class PythonLocatorSearchTests
{
    private sealed class FakeProbe : PythonLocator.IEnvironmentProbe
    {
        public bool IsWindows { get; init; }
        public string UserProfile { get; init; } = "/home/tester";
        public Dictionary<string, string> Variables { get; init; } = new();
        public HashSet<string> Files { get; init; } = new();
        public HashSet<string> Directories { get; init; } = new();
        public HashSet<string> UnreadableDirectories { get; init; } = new();

        public string? GetEnvironmentVariable(string name) => Variables.GetValueOrDefault(name);

        public bool FileExists(string path) => Files.Contains(path);

        public bool DirectoryExists(string path) => Directories.Contains(path);

        public string[] GetDirectories(string path)
        {
            if (UnreadableDirectories.Contains(path))
            {
                return [];
            }

            return Directories
                .Where(d => d.StartsWith(path + "/") && !d[(path.Length + 1)..].Contains('/'))
                .OrderBy(d => d)
                .ToArray();
        }
    }

    [Fact]
    public void Detect_Python3OnPath_PrefersItOverPython2()
    {
        FakeProbe probe = new()
        {
            Variables = { ["PATH"] = "/usr/bin" },
            Files = { "/usr/bin/python3", "/usr/bin/python2.7", "/usr/bin/python" }
        };

        var result = PythonLocator.Detect(probe);

        result.ShouldBe("/usr/bin/python3");
    }

    [Fact]
    public void Detect_OnlyPython2Available_FallsBackToIt()
    {
        FakeProbe probe = new()
        {
            Variables = { ["PATH"] = "/usr/bin" },
            Files = { "/usr/bin/python2.7" }
        };

        var result = PythonLocator.Detect(probe);

        result.ShouldBe("/usr/bin/python2.7");
    }

    [Fact]
    public void Detect_NoInterpreterAnywhere_ReturnsEmpty()
    {
        FakeProbe probe = new() { Variables = { ["PATH"] = "/usr/bin" } };

        var result = PythonLocator.Detect(probe);

        result.ShouldBe(string.Empty);
    }

    [Fact]
    public void Detect_OnWindows_LooksForExeNames()
    {
        // Paths are composed with Path.Combine so the expectation matches however the host
        // separates them; only the IsWindows flag selects the .exe names.
        var pythonDir = Path.Combine("pythons", "3.12");
        FakeProbe probe = new()
        {
            IsWindows = true,
            Variables = { ["PATH"] = pythonDir },
            Files = { Path.Combine(pythonDir, "python3.exe") }
        };

        var result = PythonLocator.Detect(probe);

        result.ShouldBe(Path.Combine(pythonDir, "python3.exe"));
    }

    [Fact]
    public void Detect_OnWindowsWithoutPath_SearchesLocalProgramsPython()
    {
        var programs = Path.Combine(@"C:\Users\tester", "AppData", "Local", "Programs", "Python");
        FakeProbe probe = new()
        {
            IsWindows = true,
            UserProfile = @"C:\Users\tester",
            Files = { Path.Combine(programs, "python.exe") }
        };

        var result = PythonLocator.Detect(probe);

        result.ShouldBe(Path.Combine(programs, "python.exe"));
    }

    [Fact]
    public void Detect_NoPathVariable_StillSearchesSystemLocations()
    {
        FakeProbe probe = new() { Files = { "/opt/homebrew/bin/python3" } };

        var result = PythonLocator.Detect(probe);

        result.ShouldBe("/opt/homebrew/bin/python3");
    }

    [Fact]
    public void Detect_PyenvShimsPresent_FindsInterpreterWhenPathHasNone()
    {
        FakeProbe probe = new()
        {
            Variables = { ["PATH"] = "/empty" },
            Directories = { "/home/tester/.pyenv", "/home/tester/.pyenv/shims" },
            Files = { "/home/tester/.pyenv/shims/python3" }
        };

        var result = PythonLocator.Detect(probe);

        result.ShouldBe("/home/tester/.pyenv/shims/python3");
    }

    [Fact]
    public void EnumerateDirectories_PyenvRootOverridden_UsesThatRoot()
    {
        FakeProbe probe = new()
        {
            Variables = { ["PYENV_ROOT"] = "/custom/pyenv" },
            Directories = { "/custom/pyenv", "/custom/pyenv/shims" }
        };

        List<string> dirs = PythonLocator.EnumerateDirectories(probe);

        dirs.ShouldContain("/custom/pyenv/shims");
    }

    [Fact]
    public void PyenvDirectories_RootMissing_ReturnsNothing()
    {
        FakeProbe probe = new();

        IEnumerable<string> dirs = PythonLocator.PyenvDirectories(probe);

        dirs.ShouldBeEmpty();
    }

    [Fact]
    public void PyenvDirectories_NoShimsDirectory_SkipsShimsButStillReadsVersions()
    {
        FakeProbe probe = new()
        {
            Directories =
            {
                "/home/tester/.pyenv",
                "/home/tester/.pyenv/versions",
                "/home/tester/.pyenv/versions/3.12.1",
                "/home/tester/.pyenv/versions/3.12.1/bin"
            }
        };

        List<string> dirs = PythonLocator.PyenvDirectories(probe).ToList();

        dirs.ShouldBe(["/home/tester/.pyenv/versions/3.12.1/bin"]);
    }

    [Fact]
    public void PyenvDirectories_NoVersionsDirectory_ReturnsOnlyShims()
    {
        FakeProbe probe = new()
        {
            Directories = { "/home/tester/.pyenv", "/home/tester/.pyenv/shims" }
        };

        List<string> dirs = PythonLocator.PyenvDirectories(probe).ToList();

        dirs.ShouldBe(["/home/tester/.pyenv/shims"]);
    }

    [Fact]
    public void PyenvDirectories_MultipleVersions_OrdersNewestFirst()
    {
        FakeProbe probe = new()
        {
            Directories =
            {
                "/home/tester/.pyenv",
                "/home/tester/.pyenv/versions",
                "/home/tester/.pyenv/versions/3.9.6",
                "/home/tester/.pyenv/versions/3.9.6/bin",
                "/home/tester/.pyenv/versions/3.12.1",
                "/home/tester/.pyenv/versions/3.12.1/bin",
                "/home/tester/.pyenv/versions/2.7.18",
                "/home/tester/.pyenv/versions/2.7.18/bin"
            }
        };

        List<string> dirs = PythonLocator.PyenvDirectories(probe).ToList();

        dirs.ShouldBe([
            "/home/tester/.pyenv/versions/3.12.1/bin",
            "/home/tester/.pyenv/versions/3.9.6/bin",
            "/home/tester/.pyenv/versions/2.7.18/bin"
        ]);
    }

    [Fact]
    public void PyenvDirectories_VersionWithoutBinDirectory_IsIgnored()
    {
        FakeProbe probe = new()
        {
            Directories =
            {
                "/home/tester/.pyenv",
                "/home/tester/.pyenv/versions",
                "/home/tester/.pyenv/versions/3.12.1"
            }
        };

        List<string> dirs = PythonLocator.PyenvDirectories(probe).ToList();

        dirs.ShouldBeEmpty();
    }

    [Fact]
    public void PyenvDirectories_UnparseableVersionName_IsListedAfterVersionedOnes()
    {
        FakeProbe probe = new()
        {
            Directories =
            {
                "/home/tester/.pyenv",
                "/home/tester/.pyenv/versions",
                "/home/tester/.pyenv/versions/3.12.1",
                "/home/tester/.pyenv/versions/3.12.1/bin",
                "/home/tester/.pyenv/versions/mystery",
                "/home/tester/.pyenv/versions/mystery/bin"
            }
        };

        List<string> dirs = PythonLocator.PyenvDirectories(probe).ToList();

        dirs.ShouldBe([
            "/home/tester/.pyenv/versions/3.12.1/bin",
            "/home/tester/.pyenv/versions/mystery/bin"
        ]);
    }

    [Fact]
    public void PyenvDirectories_OnWindows_TreatsVersionDirectoryAsTheBinDirectory()
    {
        FakeProbe probe = new()
        {
            IsWindows = true,
            Directories =
            {
                "/home/tester/.pyenv",
                "/home/tester/.pyenv/versions",
                "/home/tester/.pyenv/versions/3.12.1"
            }
        };

        List<string> dirs = PythonLocator.PyenvDirectories(probe).ToList();

        // On Windows the interpreter sits directly in the version directory, not in bin/.
        dirs.ShouldBe(["/home/tester/.pyenv/versions/3.12.1"]);
    }

    [Fact]
    public void PyenvDirectories_VersionsDirectoryUnreadable_YieldsNothing()
    {
        FakeProbe probe = new()
        {
            Directories = { "/home/tester/.pyenv", "/home/tester/.pyenv/versions" },
            UnreadableDirectories = { "/home/tester/.pyenv/versions" }
        };

        List<string> dirs = PythonLocator.PyenvDirectories(probe).ToList();

        dirs.ShouldBeEmpty();
    }

    [Fact]
    public void EnumerateDirectories_DuplicateAndBlankPathEntries_AreCollapsed()
    {
        FakeProbe probe = new()
        {
            Variables = { ["PATH"] = "/usr/bin" + Path.PathSeparator + "  " + Path.PathSeparator + "/usr/bin" }
        };

        List<string> dirs = PythonLocator.EnumerateDirectories(probe);

        dirs.Count(d => d == "/usr/bin").ShouldBe(1);
        dirs.ShouldNotContain(string.Empty);
    }

    [Fact]
    public void EnumerateDirectories_OnUnix_IncludesCommonSystemLocations()
    {
        FakeProbe probe = new();

        List<string> dirs = PythonLocator.EnumerateDirectories(probe);

        dirs.ShouldContain("/opt/homebrew/bin");
        dirs.ShouldContain("/usr/local/bin");
        dirs.ShouldContain("/usr/bin");
        dirs.ShouldContain("/opt/local/bin");
    }
}
