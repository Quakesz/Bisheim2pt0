namespace Bisheim2pt0.Models;

public sealed class ModpackManifest
{
    public string Name { get; init; } = "Bisheim 2.0";
    public string Version { get; init; } = "0.0.0";
    public string? ServerAddress { get; init; }
    public List<PackageEntry> Packages { get; init; } = [];
}

public sealed class PackageEntry
{
    public required string Name { get; init; }
    public required string Version { get; init; }
    public required string Url { get; init; }
    public required string Sha256 { get; init; }
    public string? StripPrefix { get; init; }
}

public sealed class InstalledManifest
{
    public string Version { get; init; } = "0.0.0";
    public List<InstalledPackage> Packages { get; init; } = [];
    public List<string> ManagedFiles { get; init; } = [];
}

public sealed class InstalledPackage
{
    public required string Name { get; init; }
    public required string Version { get; init; }
}
