using System.IO;
namespace Bisheim2pt0.Services;

public static class LauncherSettings
{
    // Published Thunderstore modpack and Bisheim server.
    public const string ModpackName = "Bisheim2pt0-Bisheim2pt0";
    public const string ModpackApiUrl = "https://thunderstore.io/api/experimental/package/Bisheim2pt0/Bisheim2pt0/";
    public const string ServerAddress = "srv781780.hstgr.cloud:2456";
    public const string SteamAppId = "892970";

    public static string ProfileRoot => Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
        "Bisheim2pt0", "profile");

    public static string InstalledManifestFile => Path.Combine(ProfileRoot, ".bisheim-installed.json");
}


