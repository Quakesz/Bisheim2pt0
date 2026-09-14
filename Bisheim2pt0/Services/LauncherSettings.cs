using System.IO;
namespace Bisheim2pt0.Services;

public static class LauncherSettings
{
    // Replace this with the public HTTPS URL of your production manifest.
    public const string ManifestUrl = "https://example.com/bisheim/manifest.json";
    public const string SteamAppId = "892970";

    public static string ProfileRoot => Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
        "Bisheim2pt0", "profile");

    public static string InstalledManifestFile => Path.Combine(ProfileRoot, ".bisheim-installed.json");
}

