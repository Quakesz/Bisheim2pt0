using System.IO;
using System.IO.Compression;
using System.Security.Cryptography;
using System.Text.Json;
using Bisheim2pt0.Models;

namespace Bisheim2pt0.Services;

public static class ThunderstoreInstaller
{
    public static void Extract(string zipPath, string destination, PackageId package)
    {
        using var zip = ZipFile.OpenRead(zipPath);
        long total = 0;
        if (zip.Entries.Count > 30000) throw new InvalidDataException("Too many files in package.");
        foreach (var entry in zip.Entries)
        {
            if ((total += entry.Length) > 2L * 1024 * 1024 * 1024) throw new InvalidDataException("Unpacked package exceeds 2 GB.");
            var parts = entry.FullName.Replace('\\', '/').Split('/', StringSplitOptions.RemoveEmptyEntries);
            if (entry.FullName.StartsWith('/') || entry.FullName.StartsWith('\\') ||
                parts.Any(p => p is "." or ".." || p.Contains(':') || p.EndsWith('.') || p.EndsWith(' ')))
                throw new InvalidDataException("Unsafe package path.");
            if (string.IsNullOrEmpty(entry.Name) || parts.Length == 0) continue;
            if (((entry.ExternalAttributes >> 16) & 0xF000) == 0xA000) throw new InvalidDataException("Package links are unsupported.");
            var name = parts[^1];
            string? relative;
            if (package.Key == "denikson-BepInExPack_Valheim")
            {
                var index = Array.FindIndex(parts, p => p.Equals("BepInEx", StringComparison.OrdinalIgnoreCase));
                if (index >= 0) relative = string.Join('/', parts[index..]);
                else if (name is "winhttp.dll" or "doorstop_config.ini") relative = name;
                else continue; // This launcher supports Windows, not the Unix scripts/loaders.
            }
            else
            {
                var route = Array.FindIndex(parts, p => new[] { "plugins", "config", "patchers", "core", "monomod" }.Contains(p.ToLowerInvariant()));
                if (route >= 0)
                {
                    var folder = parts[route].ToLowerInvariant();
                    if (folder is "core" or "monomod" or "patchers")
                        throw new InvalidDataException($"{package.Key} uses unsupported runtime routing: {folder}. Review this package before installing.");
                    relative = folder == "config" ? "BepInEx/config/" + string.Join('/', parts[(route + 1)..])
                        : $"BepInEx/plugins/{package.Key}/" + string.Join('/', parts[(route + 1)..]);
                }
                else
                {
                    if (name.Equals("manifest.json", StringComparison.OrdinalIgnoreCase) || name.Equals("icon.png", StringComparison.OrdinalIgnoreCase) ||
                        name.EndsWith(".md", StringComparison.OrdinalIgnoreCase) || name.StartsWith("LICENSE", StringComparison.OrdinalIgnoreCase)) continue;
                    relative = $"BepInEx/plugins/{package.Key}/" + string.Join('/', parts);
                }
            }
            var target = SafePath(destination, relative);
            if (File.Exists(target)) throw new InvalidDataException($"Packages collide at {relative}.");
            Directory.CreateDirectory(Path.GetDirectoryName(target)!);
            entry.ExtractToFile(target);
        }
    }

    public static string SafePath(string root, string relative)
    {
        var basePath = Path.GetFullPath(root) + Path.DirectorySeparatorChar;
        if (relative.Contains(':') || Path.IsPathRooted(relative)) throw new InvalidDataException("Unsafe profile path.");
        var result = Path.GetFullPath(Path.Combine(root, relative));
        if (!result.StartsWith(basePath, StringComparison.OrdinalIgnoreCase)) throw new InvalidDataException("Unsafe profile path.");
        return result;
    }

    public static void ValidateLayout(string root)
    {
        foreach (var file in new[] { "BepInEx/core/BepInEx.dll", "BepInEx/core/BepInEx.Preloader.dll", "winhttp.dll", "doorstop_config.ini" })
            if (!File.Exists(SafePath(root, file))) throw new InvalidDataException($"The modpack is missing {file}.");
        if (!Directory.Exists(Path.Combine(root, "BepInEx", "plugins")) ||
            !Directory.EnumerateFiles(Path.Combine(root, "BepInEx", "plugins"), "*.dll", SearchOption.AllDirectories).Any())
            throw new InvalidDataException("The modpack contains no plugins.");
    }

    public static void Recover(string profile)
    {
        if (!Directory.Exists(profile) && Directory.Exists(profile + ".rollback")) Directory.Move(profile + ".rollback", profile);
    }

    public static void Commit(string staging, string profile, InstalledManifest? previous, InstalledManifest next, Action? beforeActivate = null)
    {
        Recover(profile);
        var candidate = profile + ".next-" + Guid.NewGuid().ToString("N");
        var backup = profile + ".rollback";
        Directory.CreateDirectory(candidate);
        try
        {
            if (Directory.Exists(profile))
            {
                if ((File.GetAttributes(profile) & FileAttributes.ReparsePoint) != 0) throw new IOException("Linked profiles are unsupported.");
                CopyTree(profile, candidate);
            }
            var retained = next.ManagedFiles.ToHashSet(StringComparer.OrdinalIgnoreCase);
            var oldFiles = (previous?.ManagedFiles ?? []).ToHashSet(StringComparer.OrdinalIgnoreCase);
            foreach (var old in oldFiles.Where(p => !retained.Contains(p) && !IsConfig(p)))
            {
                var path = SafePath(candidate, old);
                if (File.Exists(path)) File.Delete(path);
            }
            foreach (var relative in next.ManagedFiles)
            {
                var target = SafePath(candidate, relative);
                var source = SafePath(staging, relative);
                if (File.Exists(target))
                {
                    if (IsConfig(relative)) continue; // Player configs win; the rollback retains the full prior profile.
                    if (!oldFiles.Contains(relative)) throw new IOException($"Unmanaged file conflicts with update: {relative}");
                }
                Directory.CreateDirectory(Path.GetDirectoryName(target)!);
                File.Copy(source, target, true);
            }
            ValidateLayout(candidate);
            File.WriteAllText(Path.Combine(candidate, ".bisheim-installed.json"), JsonSerializer.Serialize(next, new JsonSerializerOptions { WriteIndented = true }));
            // Keep backups of earlier versions, including their configuration files.
            if (Directory.Exists(backup)) Directory.Move(backup, backup + "-" + Guid.NewGuid().ToString("N"));
            if (Directory.Exists(profile)) Directory.Move(profile, backup);
            try { beforeActivate?.Invoke(); Directory.Move(candidate, profile); }
            catch { Recover(profile); throw; }
        }
        finally { if (Directory.Exists(candidate)) Directory.Delete(candidate, true); }
    }

    private static bool IsConfig(string path) => path.Replace('\\', '/').StartsWith("BepInEx/config/", StringComparison.OrdinalIgnoreCase);
    private static void CopyTree(string source, string destination)
    {
        foreach (var entry in Directory.EnumerateFileSystemEntries(source))
        {
            var attributes = File.GetAttributes(entry);
            if ((attributes & FileAttributes.ReparsePoint) != 0) throw new IOException("Linked profile files are unsupported.");
            var target = Path.Combine(destination, Path.GetFileName(entry));
            if ((attributes & FileAttributes.Directory) != 0) { Directory.CreateDirectory(target); CopyTree(entry, target); }
            else File.Copy(entry, target);
        }
    }
}
