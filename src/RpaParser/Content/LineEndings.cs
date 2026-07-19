using System;

namespace RpaParser.Content;

/// <summary>
/// Normalises whichever line ending style dominates a file to the platform's own, so
/// previewed text renders correctly wherever it came from.
/// </summary>
public static class LineEndings
{
    public static string Normalize(string text)
    {
        const string windows = "\r\n";
        const string unix = "\n";
        const string classicMac = "\r";

        var countWindows = System.Text.RegularExpressions.Regex.Matches(text, windows).Count;
        var countUnix = System.Text.RegularExpressions.Regex.Matches(text, unix).Count;
        var countMac = System.Text.RegularExpressions.Regex.Matches(text, classicMac).Count;

        var dominant = Environment.NewLine;

        if (countWindows >= countUnix && countWindows >= countMac)
        {
            dominant = windows;
        }
        else if (countUnix >= countWindows && countUnix >= countMac)
        {
            dominant = unix;
        }
        else if (countMac >= countWindows && countMac >= countUnix)
        {
            dominant = classicMac;
        }

        return text.Replace(dominant, Environment.NewLine);
    }
}