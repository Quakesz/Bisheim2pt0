using System.IO;
using System.Net.Http;
using System.Text.Json;
using System.Text.Json.Serialization;
using System.Text.RegularExpressions;

namespace Bisheim2pt0.Services;

public sealed record PackageId(string Owner, string Name, string Version)
{
    public string Key => $"{Owner}-{Name}";
    public string FullName => $"{Key}-{Version}";
    public static PackageId Parse(string value)
    {
        var m = Regex.Match(value, @"^([A-Za-z0-9_]+)-([A-Za-z0-9_]+)-(\d+\.\d+\.\d+)$");
        if (!m.Success) throw new InvalidDataException($"Invalid Thunderstore dependency: {value}");
        return new(m.Groups[1].Value, m.Groups[2].Value, m.Groups[3].Value);
    }
}

public sealed class ThunderstoreVersion
{
    [JsonPropertyName("full_name")] public string FullName { get; set; } = "";
    [JsonPropertyName("version_number")] public string Version { get; set; } = "";
    [JsonPropertyName("download_url")] public string DownloadUrl { get; set; } = "";
    [JsonPropertyName("is_active")] public bool Active { get; set; }
    [JsonPropertyName("dependencies")] public List<string> Dependencies { get; set; } = [];
    public PackageId Id => PackageId.Parse(FullName);
}

public sealed record ThunderstorePlan(string Version, IReadOnlyList<ThunderstoreVersion> Packages);

public sealed class ThunderstoreClient(HttpClient http)
{
    public async Task<ThunderstorePlan> ResolveAsync()
    {
        using var response = await http.GetAsync(LauncherSettings.ModpackApiUrl);
        response.EnsureSuccessStatusCode();
        using var doc = JsonDocument.Parse(await response.Content.ReadAsStringAsync());
        var root = doc.RootElement.GetProperty("latest").Deserialize<ThunderstoreVersion>()
            ?? throw new InvalidDataException("Thunderstore returned an empty modpack.");
        if (root.Id.Key != LauncherSettings.ModpackName) throw new InvalidDataException("Unexpected modpack identity.");
        return await ResolveAsync(root, FetchVersionAsync);
    }

    public static async Task<ThunderstorePlan> ResolveAsync(ThunderstoreVersion root,
        Func<PackageId, Task<ThunderstoreVersion>> fetch)
    {
        Validate(root);
        // The modpack selects exact top-level versions. Transitive references are minimums.
        var pins = root.Dependencies.Select(PackageId.Parse).ToDictionary(p => p.Key, StringComparer.OrdinalIgnoreCase);
        var selected = new Dictionary<string, ThunderstoreVersion>(StringComparer.OrdinalIgnoreCase) { [root.Id.Key] = root };
        var pending = new Queue<PackageId>(pins.Values);
        var iterations = 0;
        while (pending.TryDequeue(out var requested))
        {
            if (++iterations > 1000 || selected.Count > 150) throw new InvalidDataException("Modpack dependency graph is too large.");
            if (pins.TryGetValue(requested.Key, out var pin))
            {
                if (System.Version.Parse(pin.Version) < System.Version.Parse(requested.Version))
                    throw new InvalidDataException($"{requested.Key} needs {requested.Version}, but the modpack selects {pin.Version}.");
                requested = pin;
            }
            if (selected.TryGetValue(requested.Key, out var current) &&
                System.Version.Parse(current.Version) >= System.Version.Parse(requested.Version)) continue;
            var package = await fetch(requested);
            Validate(package);
            if (package.FullName != requested.FullName) throw new InvalidDataException("Thunderstore returned a different package version.");
            selected[requested.Key] = package;
            foreach (var dependency in package.Dependencies) pending.Enqueue(PackageId.Parse(dependency));
        }
        // Visit only the final selected graph: replaced versions may have obsolete dependencies.
        var ordered = new List<ThunderstoreVersion>();
        var visiting = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        var visited = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        void Visit(ThunderstoreVersion package)
        {
            if (visited.Contains(package.Id.Key)) return;
            if (!visiting.Add(package.Id.Key)) throw new InvalidDataException("Cyclic modpack dependencies.");
            foreach (var dep in package.Dependencies) Visit(selected[PackageId.Parse(dep).Key]);
            visiting.Remove(package.Id.Key); visited.Add(package.Id.Key); ordered.Add(package);
        }
        Visit(root);
        return new(root.Version, ordered);
    }

    private async Task<ThunderstoreVersion> FetchVersionAsync(PackageId id)
    {
        using var response = await http.GetAsync($"https://thunderstore.io/api/experimental/package/{id.Owner}/{id.Name}/{id.Version}/");
        response.EnsureSuccessStatusCode();
        return JsonSerializer.Deserialize<ThunderstoreVersion>(await response.Content.ReadAsStringAsync())
            ?? throw new InvalidDataException("Thunderstore returned an empty dependency.");
    }

    private static void Validate(ThunderstoreVersion package)
    {
        if (!package.Active || package.Id.Version != package.Version) throw new InvalidDataException("Inactive or invalid Thunderstore package.");
        ValidateDownloadUrl(package.DownloadUrl);
    }

    public static void ValidateDownloadUrl(string url)
    {
        if (!Uri.TryCreate(url, UriKind.Absolute, out var uri) || uri.Scheme != "https" ||
            uri.Host != "thunderstore.io" || !uri.AbsolutePath.StartsWith("/package/download/", StringComparison.Ordinal) ||
            !uri.IsDefaultPort || uri.UserInfo.Length != 0)
            throw new InvalidDataException("Package download must use the Thunderstore HTTPS download endpoint.");
    }
}
