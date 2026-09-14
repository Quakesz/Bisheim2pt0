using System.Diagnostics;
using System.IO;
using System.Security.Cryptography;
using System.Text.RegularExpressions;

namespace Bisheim2pt0.Services;

public static class ModdedLaunch
{
    // Both Doorstop generations use enabled, but different section names.
    public const string DisabledConfig = "[General]\nenabled=false\n[UnityDoorstop]\nenabled=false\n";

    public static ProcessStartInfo Prepare(string steamExecutable, string gameExecutable, string profile, string endpoint)
    {
        var preloader = Path.Combine(profile, "BepInEx", "core", "BepInEx.Preloader.dll");
        var sourceDll = Path.Combine(profile, "winhttp.dll");
        var sourceConfig = Path.Combine(profile, "doorstop_config.ini");
        foreach (var file in new[] { preloader, sourceDll, sourceConfig })
            if (!File.Exists(file)) throw new InvalidOperationException("The client pack is missing BepInEx bootstrap files. Repair the installation.");

        var config = File.ReadAllText(sourceConfig);
        var modern = Regex.IsMatch(config, @"(?im)^\s*target_assembly\s*=");
        var legacy = Regex.IsMatch(config, @"(?im)^\s*targetAssembly\s*=");
        if (modern == legacy) throw new InvalidDataException("The client pack has an unsupported Doorstop configuration.");

        var gameRoot = Path.GetDirectoryName(gameExecutable)!;
        var targetDll = Path.Combine(gameRoot, "winhttp.dll");
        var targetConfig = Path.Combine(gameRoot, "doorstop_config.ini");
        // Check every conflict before writing anything. Never replace a foreign loader/config.
        if ((File.Exists(targetDll) && !SameFile(sourceDll, targetDll)) ||
            (File.Exists(targetConfig) && File.ReadAllText(targetConfig) != DisabledConfig) ||
            File.Exists(Path.Combine(gameRoot, "version.dll")))
            throw new InvalidOperationException("An existing mod loader was found in the Valheim folder. Back up and remove its loader files before using Bisheim.");

        // Write the disabled config first, so an interrupted setup cannot enable ordinary Steam launches.
        if (!File.Exists(targetConfig)) File.WriteAllText(targetConfig, DisabledConfig);
        if (!File.Exists(targetDll)) File.Copy(sourceDll, targetDll, overwrite: false);

        var start = new ProcessStartInfo(steamExecutable) { UseShellExecute = false, WorkingDirectory = gameRoot };
        foreach (var arg in new[] { "-applaunch", LauncherSettings.SteamAppId,
            modern ? "--doorstop-enabled" : "--doorstop-enable", "true",
            modern ? "--doorstop-target-assembly" : "--doorstop-target", preloader, "+connect", endpoint })
            start.ArgumentList.Add(arg);
        return start;
    }

    private static bool SameFile(string left, string right)
    {
        using var a = File.OpenRead(left);
        using var b = File.OpenRead(right);
        return SHA256.HashData(a).AsSpan().SequenceEqual(SHA256.HashData(b));
    }
}
