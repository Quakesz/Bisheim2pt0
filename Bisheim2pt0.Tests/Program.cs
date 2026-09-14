using Bisheim2pt0.Services;

var root = Path.Combine(Path.GetTempPath(), "Bisheim-tests", Guid.NewGuid().ToString("N"));
Directory.CreateDirectory(root);
var count = 0;
void Check(bool condition, string description)
{
    if (!condition) throw new Exception(description);
    Console.WriteLine("PASS " + description);
    count++;
}
void Reject(Action action, string description)
{
    try { action(); }
    catch (InvalidOperationException) { Check(true, description); return; }
    throw new Exception("Expected rejection: " + description);
}
void Put(string path, string text)
{
    Directory.CreateDirectory(Path.GetDirectoryName(path)!);
    File.WriteAllText(path, text);
}
try
{
    var steam = Path.Combine(root, "Steam");
    var library = Path.Combine(root, "Library with spaces");
    var game = Path.Combine(library, "steamapps", "common", "Valheim", "valheim.exe");
    var escaped = library.Replace("\\", "\\\\");
    Put(Path.Combine(steam, "steamapps", "libraryfolders.vdf"), "\"libraryfolders\" { \"0\" { \"path\" \"" + escaped + "\" \"apps\" { \"892970\" \"100\" } } }");
    Put(game, "test executable");
    Check(SteamLocator.FindValheim(steam) == game, "Find game in modern custom library with spaces");
    Check(SteamLocator.ReadLibraryPaths("\"1\" \"" + escaped + "\"").Single() == library, "Read legacy escaped library paths");
    Reject(() => SteamLocator.FindValheim(Path.Combine(root, "missing")), "Missing game produces actionable error");

    var profile = Path.Combine(root, "profile with spaces");
    var preloader = Path.Combine(profile, "BepInEx", "core", "BepInEx.Preloader.dll");
    Put(preloader, "test preloader");
    Put(Path.Combine(profile, "winhttp.dll"), "test loader");
    var config = Path.Combine(profile, "doorstop_config.ini");
    Put(config, "[General]\nenabled=true\ntarget_assembly=BepInEx/core/BepInEx.Preloader.dll");
    var start = ModdedLaunch.Prepare("steam.exe", game, profile, "server.example:2456");
    Check(start.ArgumentList.Contains("--doorstop-target-assembly") && start.ArgumentList.Contains(preloader), "Modern loader gets isolated preloader as one argument");
    Check(start.ArgumentList.TakeLast(2).SequenceEqual(new[] { "+connect", "server.example:2456" }), "Steam receives direct-connect endpoint");
    var gameRoot = Path.GetDirectoryName(game)!;
    var targetConfig = Path.Combine(gameRoot, "doorstop_config.ini");
    Check(File.ReadAllText(targetConfig) == ModdedLaunch.DisabledConfig, "Bootstrap disabled for ordinary launches");
    ModdedLaunch.Prepare("steam.exe", game, profile, "server.example:2456");
    Check(true, "Repeated bootstrap setup is idempotent");
    Put(config, "[UnityDoorstop]\nenabled=true\ntargetAssembly=BepInEx/core/BepInEx.Preloader.dll");
    Check(ModdedLaunch.Prepare("steam.exe", game, profile, "server.example:2456").ArgumentList.Contains("--doorstop-target"), "Legacy loader uses legacy arguments");
    Put(targetConfig, "existing player config");
    Reject(() => ModdedLaunch.Prepare("steam.exe", game, profile, "server.example:2456"), "Reject existing player configuration");
    Check(File.ReadAllText(targetConfig) == "existing player config", "Preserve conflicting configuration");
    Put(targetConfig, ModdedLaunch.DisabledConfig);
    Put(Path.Combine(gameRoot, "winhttp.dll"), "foreign loader");
    Reject(() => ModdedLaunch.Prepare("steam.exe", game, profile, "server.example:2456"), "Reject mismatched loader");
    File.Delete(preloader);
    Reject(() => ModdedLaunch.Prepare("steam.exe", game, profile, "server.example:2456"), "Reject missing preloader");
    await ThunderstoreChecks.Run(root, Check, args.Contains("--live"));
    Console.WriteLine($"{count} checks passed. No Steam or game process was launched.");
}
finally
{
    Directory.Delete(root, recursive: true);
}

