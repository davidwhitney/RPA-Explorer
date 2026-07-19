using System.IO;
using RpaParser;
using Shouldly;

namespace RpaParser.Tests;

public class PythonLocatorTests
{
    [Fact]
    public void Detected_WhenCalled_ReturnsEitherAnExistingFileOrEmpty()
    {
        var detected = PythonLocator.Detected;

        // Detection is best effort: it either points at a real interpreter or reports nothing.
        // It must never hand back a path that does not exist.
        if (detected.Length > 0)
        {
            File.Exists(detected).ShouldBeTrue();
        }
        else
        {
            detected.ShouldBe(string.Empty);
        }
    }

    [Fact]
    public void Detected_CalledRepeatedly_ReturnsCachedResult()
    {
        var first = PythonLocator.Detected;
        var second = PythonLocator.Detected;

        second.ShouldBeSameAs(first);
    }

    [Fact]
    public void Detected_WhenAnInterpreterIsFound_NamesAPythonExecutable()
    {
        var detected = PythonLocator.Detected;

        if (detected.Length > 0)
        {
            Path.GetFileName(detected).ShouldStartWith("python");
        }
    }

    [Fact]
    public void PythonPath_NewOptions_MatchesDetectedInterpreter()
    {
        var options = new DecompilerOptions();

        options.PythonPath.ShouldBe(PythonLocator.Detected);
    }

    [Fact]
    public void UnrpycPath_NewOptions_IsEmptyUntilConfigured()
    {
        var options = new DecompilerOptions();

        options.UnrpycPath.ShouldBe(string.Empty);
    }
}
