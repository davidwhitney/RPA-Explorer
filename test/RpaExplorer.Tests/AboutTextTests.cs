using System.Text.RegularExpressions;
using Shouldly;

namespace RpaExplorer.Tests;

/// <summary>
/// The About box is built with string.Format, so a placeholder the window does not supply
/// throws - and only when someone opens the menu. These pin the two together.
/// </summary>
public class AboutTextTests
{
    /// <summary>Version, original author and repository, port author and repository, translators, contributors.</summary>
    private const int CreditsSuppliedByAboutWindow = 7;

    [Fact]
    public void AboutText_Placeholders_AreAllSuppliedByTheWindow()
    {
        var template = Strings.Get("EN", "About_text");

        var highest = Regex.Matches(template, @"\{(\d+)\}")
            .Select(match => int.Parse(match.Groups[1].Value))
            .Max();

        highest.ShouldBe(CreditsSuppliedByAboutWindow - 1);
    }

    [Fact]
    public void AboutText_EveryCredit_ReachesTheRenderedText()
    {
        var template = Strings.Get("EN", "About_text");
        string[] credits = ["VERSION", "AUTHOR", "REPO", "PORTER", "FORK", "TRANSLATORS", "CONTRIBUTORS"];

        var rendered = string.Format(template, credits);

        rendered.ShouldNotContain("MISSING TRANSLATION");
        foreach (var credit in credits)
        {
            rendered.ShouldContain(credit);
        }
    }

    [Fact]
    public void AboutText_BothRepositories_AreNamedSoNeitherProjectGoesUncredited()
    {
        var rendered = string.Format(Strings.Get("EN", "About_text"),
            "1.0.0.0", "Martin Suchy", "https://github.com/UniverseDevel/RPA-Explorer",
            "David Whitney", "https://github.com/davidwhitney/RPA-Explorer", "-", "jensbrak");

        rendered.ShouldContain("Martin Suchy");
        rendered.ShouldContain("UniverseDevel/RPA-Explorer");
        rendered.ShouldContain("David Whitney");
        rendered.ShouldContain("davidwhitney/RPA-Explorer");
    }
}
