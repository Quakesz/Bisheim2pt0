using System.Diagnostics;
using System.IO;
using System.IO.Compression;
using System.Net.Http;
using System.Security.Cryptography;
using System.Text.Json;
using Bisheim2pt0.Models;
using Microsoft.Win32;

namespace Bisheim2pt0.Services;

public sealed record LauncherState(bool NeedsInstall, string DisplayVersion, string ServerAddress, string Status);
public sealed record InstallProgress(int Percent, string Message);

public sealed class LauncherService
{
    private static readonly HttpClient Http = new() { Timeout = TimeSpan.FromMinutes(5) };
    private static readonly JsonSerializerOptions JsonOptions = new() { PropertyNameCaseInsensitive = true };

    public async Task<LauncherState> GetStateAsync()
    {
        EnsureValheimInstalled();
        var manifest = await GetManifestAsync();
        var installed = await ReadInstalledManifestAsync();
        var needsInstall = !string.Equals(installed?.Version, manifest.Version, StringComparison.OrdinalIgnoreCase);
        var versions = installed is null ? $"Available: {manifest.Version}" : $"{installed.Version} → {manifest.Version}";
        return new(needsInstall, versions, manifest.ServerAddress ?? "Not configured",
            needsInstall ? "Update ready" : "Ready to play");
    }

    public async Task InstallAsync(bool force, IProgress<InstallProgress> progress)
    {
        EnsureGameStopped();
        var manifest = await GetManifestAsync();
        Directory.CreateDirectory(LauncherSettings.ProfileRoot);

        var installed = await ReadInstalledManifestAsync();
        if (!force && string.Equals(installed?.Version, manifest.Version, StringComparison.OrdinalIgnoreCase)) return;

        var tempRoot = Path.Combine(Path.GetTempPath(), "Bisheim2pt0", Guid.NewGuid().ToString("N"));
        var stagingRoot = Path.Combine(tempRoot, "staging");
        Directory.CreateDirectory(stagingRoot);
        try
        {
            for (var i = 0; i < manifest.Packages.Count; i++)
            {
                var package = manifest.Packages[i];
                var percent = manifest.Packages.Count == 0 ? 50 : i * 90 / manifest.Packages.Count;
                progress.Report(new(percent, $"Downloading {package.Name}…"));
                var archive = Path.Combine(tempRoot, $"{i}.zip");
                await DownloadAsync(package.Url, archive);
                VerifySha256(archive, package.Sha256);
                progress.Report(new(percent + 5, $"Installing {package.Name}…"));
                ExtractSafely(archive, stagingRoot, package.StripPrefix);
            }

            var newFiles = Directory.EnumerateFiles(stagingRoot, "*", SearchOption.AllDirectories)
                .Select(file => Path.GetRelativePath(stagingRoot, file))
                .Order(StringComparer.OrdinalIgnoreCase)
                .ToList();

            RemoveObsoleteManagedFiles(installed?.ManagedFiles ?? [], newFiles);
            CopyStagedFiles(stagingRoot, newFiles);

            var installedManifest = new InstalledManifest
            {
                Version = manifest.Version,
                Packages = manifest.Packages.Select(p => new InstalledPackage
                {
                    Name = p.Name,
                    Version = p.Version
                }).ToList(),
                ManagedFiles = newFiles
            };
            await File.WriteAllTextAsync(
                LauncherSettings.InstalledManifestFile,
                JsonSerializer.Serialize(installedManifest, new JsonSerializerOptions { WriteIndented = true }));
            progress.Report(new(100, "Bisheim is ready"));
        }
        finally
        {
            if (Directory.Exists(tempRoot)) Directory.Delete(tempRoot, recursive: true);
        }
    }

    public async Task LaunchValheimAsync()
    {
        EnsureGameStopped();
        var gameExecutable = EnsureValheimInstalled();
        var manifest = await GetManifestAsync();
        var endpoint = ParseServerEndpoint(manifest.ServerAddress);
        var steamExecutable = GetSteamExecutable();

        var start = ModdedLaunch.Prepare(steamExecutable, gameExecutable, LauncherSettings.ProfileRoot, endpoint);
        Process.Start(start);
    }

    private static void EnsureGameStopped()
    {
        var processes = Process.GetProcessesByName("valheim");
        try
        {
            if (processes.Length > 0)
                throw new InvalidOperationException("Close Valheim before installing or launching Bisheim.");
        }
        finally
        {
            foreach (var process in processes) process.Dispose();
        }
    }

    private static async Task<ModpackManifest> GetManifestAsync()
    {
        if (new Uri(LauncherSettings.ManifestUrl).Host == "example.com")
            throw new InvalidOperationException("The modpack download address has not been configured yet.");
        using var response = await Http.GetAsync(LauncherSettings.ManifestUrl);
        response.EnsureSuccessStatusCode();
        await using var stream = await response.Content.ReadAsStreamAsync();
        return await JsonSerializer.DeserializeAsync<ModpackManifest>(stream, JsonOptions)
            ?? throw new InvalidDataException("The Bisheim manifest is empty or invalid.");
    }

    private static async Task DownloadAsync(string url, string destination)
    {
        using var response = await Http.GetAsync(url, HttpCompletionOption.ResponseHeadersRead);
        response.EnsureSuccessStatusCode();
        await using var input = await response.Content.ReadAsStreamAsync();
        await using var output = File.Create(destination);
        await input.CopyToAsync(output);
    }

    private static void VerifySha256(string file, string expected)
    {
        using var stream = File.OpenRead(file);
        var actual = Convert.ToHexString(SHA256.HashData(stream));
        if (!actual.Equals(expected.Replace("-", ""), StringComparison.OrdinalIgnoreCase))
            throw new InvalidDataException($"Security check failed for {Path.GetFileName(file)}.");
    }

    private static void ExtractSafely(string archive, string destination, string? stripPrefix)
    {
        var root = Path.GetFullPath(destination) + Path.DirectorySeparatorChar;
        var normalizedPrefix = string.IsNullOrWhiteSpace(stripPrefix)
            ? null
            : stripPrefix.Replace('\\', '/').Trim('/') + "/";
        using var zip = ZipFile.OpenRead(archive);
        foreach (var entry in zip.Entries)
        {
            var entryName = entry.FullName.Replace('\\', '/');
            if (normalizedPrefix is not null)
            {
                if (!entryName.StartsWith(normalizedPrefix, StringComparison.OrdinalIgnoreCase)) continue;
                entryName = entryName[normalizedPrefix.Length..];
            }
            if (string.IsNullOrWhiteSpace(entryName)) continue;

            var output = Path.GetFullPath(Path.Combine(destination, entryName));
            if (!output.StartsWith(root, StringComparison.OrdinalIgnoreCase))
                throw new InvalidDataException("A package contains an unsafe file path.");
            if (string.IsNullOrEmpty(entry.Name)) Directory.CreateDirectory(output);
            else
            {
                Directory.CreateDirectory(Path.GetDirectoryName(output)!);
                entry.ExtractToFile(output, overwrite: true);
            }
        }
    }

    private static async Task<InstalledManifest?> ReadInstalledManifestAsync()
    {
        if (!File.Exists(LauncherSettings.InstalledManifestFile)) return null;
        await using var stream = File.OpenRead(LauncherSettings.InstalledManifestFile);
        return await JsonSerializer.DeserializeAsync<InstalledManifest>(stream, JsonOptions);
    }

    private static void RemoveObsoleteManagedFiles(IEnumerable<string> oldFiles, ICollection<string> newFiles)
    {
        var retained = new HashSet<string>(newFiles, StringComparer.OrdinalIgnoreCase);
        foreach (var relative in oldFiles.Where(path => !retained.Contains(path)))
        {
            var target = ResolveProfilePath(relative);
            if (File.Exists(target)) File.Delete(target);
        }
    }

    private static void CopyStagedFiles(string stagingRoot, IEnumerable<string> files)
    {
        foreach (var relative in files)
        {
            var source = Path.Combine(stagingRoot, relative);
            var target = ResolveProfilePath(relative);
            Directory.CreateDirectory(Path.GetDirectoryName(target)!);
            File.Copy(source, target, overwrite: true);
        }
    }

    private static string ResolveProfilePath(string relative)
    {
        var root = Path.GetFullPath(LauncherSettings.ProfileRoot) + Path.DirectorySeparatorChar;
        var target = Path.GetFullPath(Path.Combine(LauncherSettings.ProfileRoot, relative));
        if (!target.StartsWith(root, StringComparison.OrdinalIgnoreCase))
            throw new InvalidDataException("The installed file list contains an unsafe path.");
        return target;
    }

    private static string EnsureValheimInstalled()
    {
        return SteamLocator.FindValheim(GetSteamPath());
    }

    private static string GetSteamExecutable()
    {
        var executable = Path.Combine(GetSteamPath(), "steam.exe");
        if (!File.Exists(executable))
            throw new InvalidOperationException("Steam could not be found. Install Steam first.");
        return executable;
    }

    private static string GetSteamPath()
    {
        return Registry.CurrentUser.OpenSubKey(@"Software\Valve\Steam")?.GetValue("SteamPath") as string
            ?? throw new InvalidOperationException("Steam could not be found. Install Steam first.");
    }

    private static string ParseServerEndpoint(string? value)
    {
        if (string.IsNullOrWhiteSpace(value))
            throw new InvalidDataException("The Bisheim server address is missing from the manifest.");

        var parts = value.Trim().Split(':');
        if (parts.Length != 2 || string.IsNullOrWhiteSpace(parts[0]) ||
            !int.TryParse(parts[1], out var port) || port is < 1 or > 65535 ||
            parts[0].Any(c => !(char.IsLetterOrDigit(c) || c is '.' or '-')))
            throw new InvalidDataException("The Bisheim server address in the manifest is invalid.");

        return $"{parts[0]}:{port}";
    }
}

