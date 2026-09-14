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
        ThunderstoreInstaller.Recover(LauncherSettings.ProfileRoot);

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
                using (var hashStream = File.OpenRead(archive)) package.Sha256 = Convert.ToHexString(SHA256.HashData(hashStream));
                var prior = installed?.Packages.FirstOrDefault(p => p.Name == package.Name && p.Version == package.Version);
                if (!string.IsNullOrEmpty(prior?.Sha256) && !prior.Sha256.Equals(package.Sha256, StringComparison.OrdinalIgnoreCase))
                    throw new InvalidDataException($"The previously installed archive for {package.Name} {package.Version} has changed.");
                progress.Report(new(percent + 5, $"Installing {package.Name}…"));
                ThunderstoreInstaller.Extract(archive, stagingRoot, PackageId.Parse(package.Name + "-" + package.Version));
            }

            var newFiles = Directory.EnumerateFiles(stagingRoot, "*", SearchOption.AllDirectories)
                .Select(file => Path.GetRelativePath(stagingRoot, file))
                .Order(StringComparer.OrdinalIgnoreCase)
                .ToList();

            ThunderstoreInstaller.ValidateLayout(stagingRoot);

            var installedManifest = new InstalledManifest
            {
                Version = manifest.Version,
                Packages = manifest.Packages.Select(p => new InstalledPackage
                {
                    Name = p.Name,
                    Version = p.Version,
                    Sha256 = p.Sha256
                }).ToList(),
                ManagedFiles = newFiles
            };
            EnsureGameStopped();
            ThunderstoreInstaller.Commit(stagingRoot, LauncherSettings.ProfileRoot, installed, installedManifest);
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
        var installed = await ReadInstalledManifestAsync();
        if (installed?.Version != manifest.Version) throw new InvalidOperationException("Install the current modpack version before playing.");
        ThunderstoreInstaller.ValidateLayout(LauncherSettings.ProfileRoot);
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
        var plan = await new ThunderstoreClient(Http).ResolveAsync();
        return new ModpackManifest
        {
            Version = plan.Version,
            ServerAddress = LauncherSettings.ServerAddress,
            Packages = plan.Packages.Select(p => new PackageEntry
            {
                Name = p.Id.Key, Version = p.Version, Url = p.DownloadUrl, Sha256 = ""
            }).ToList()
        };
    }
    private static async Task DownloadAsync(string url, string destination)
    {
        ThunderstoreClient.ValidateDownloadUrl(url);
        using var response = await Http.GetAsync(url, HttpCompletionOption.ResponseHeadersRead);
        response.EnsureSuccessStatusCode();
        await using var input = await response.Content.ReadAsStreamAsync();
        await using var output = File.Create(destination);
        var buffer = new byte[81920];
        long total = 0;
        using var timeout = new CancellationTokenSource(TimeSpan.FromMinutes(5));
        int read;
        while ((read = await input.ReadAsync(buffer, timeout.Token)) > 0)
        {
            if ((total += read) > 512L * 1024 * 1024) throw new InvalidDataException("Package download exceeds 512 MB.");
            await output.WriteAsync(buffer.AsMemory(0, read), timeout.Token);
        }
    }

    private static async Task<InstalledManifest?> ReadInstalledManifestAsync()
    {
        ThunderstoreInstaller.Recover(LauncherSettings.ProfileRoot);
        if (!File.Exists(LauncherSettings.InstalledManifestFile)) return null;
        await using var stream = File.OpenRead(LauncherSettings.InstalledManifestFile);
        return await JsonSerializer.DeserializeAsync<InstalledManifest>(stream, JsonOptions);
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


