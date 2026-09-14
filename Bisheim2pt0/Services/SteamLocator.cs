using System.IO;
using System.Text.RegularExpressions;
using Microsoft.Win32;

namespace Bisheim2pt0.Services;

public static class SteamLocator
{
    public static string GetSteamPath()
    {
        using var key = Registry.CurrentUser.OpenSubKey(@"Software\Valve\Steam");
        return key?.GetValue("SteamPath") as string
            ?? throw new InvalidOperationException("Steam could not be found. Install Steam first.");
    }

    public static IEnumerable<string> ReadLibraryPaths(string text)
    {
        // Handles modern nested library entries and the older numbered path format.
        foreach (Match match in Regex.Matches(text, "\"(?:path|[0-9]+)\"\\s*\"(?<path>(?:\\\\.|[^\"\\\\])*)\"", RegexOptions.IgnoreCase))
        {
            var path = match.Groups["path"].Value.Replace(@"\\", @"\");
            if (Path.IsPathFullyQualified(path)) yield return path;
        }
    }

    public static string FindValheim(string steamRoot)
    {
        var libraries = new List<string> { steamRoot };
        var vdf = Path.Combine(steamRoot, "steamapps", "libraryfolders.vdf");
        if (File.Exists(vdf)) libraries.AddRange(ReadLibraryPaths(File.ReadAllText(vdf)));
        foreach (var library in libraries.Distinct(StringComparer.OrdinalIgnoreCase))
        {
            var executable = Path.Combine(library, "steamapps", "common", "Valheim", "valheim.exe");
            if (File.Exists(executable)) return executable;
        }
        throw new InvalidOperationException("Valheim was not found in your Steam libraries. Install it through Steam first.");
    }
}
