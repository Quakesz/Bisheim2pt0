using System.IO.Compression;
using System.Security.Cryptography;
using Bisheim2pt0.Models;
using Bisheim2pt0.Services;

static class ThunderstoreChecks
{
    public static async Task Run(string root, Action<bool,string> check, bool live)
    {
        ThunderstoreVersion P(string id, params string[] deps) => new() { FullName=id, Version=PackageId.Parse(id).Version, Active=true, DownloadUrl="https://thunderstore.io/package/download/"+id.Replace('-','/')+"/", Dependencies=deps.ToList() };
        var packages=new Dictionary<string,ThunderstoreVersion> {
            ["Core-Runtime-2.0.0"]=P("Core-Runtime-2.0.0"),
            ["Author-Mod-1.0.0"]=P("Author-Mod-1.0.0","Core-Runtime-1.0.0")
        };
        var plan=await ThunderstoreClient.ResolveAsync(P("Team-Pack-1.0.0","Core-Runtime-2.0.0","Author-Mod-1.0.0"), id=>Task.FromResult(packages[id.FullName]));
        check(plan.Packages.Count==3 && plan.Packages[0].Version=="2.0.0", "Pinned runtime satisfies older transitive dependency without duplicate install");
        try { await ThunderstoreClient.ResolveAsync(P("Team-Pack-1.0.0","Core-Runtime-1.0.0","Author-Mod-1.0.0"), id=>Task.FromResult(id.Owner=="Author"?P(id.FullName,"Core-Runtime-2.0.0"):P(id.FullName))); throw new Exception("Expected conflict"); }
        catch(InvalidDataException) {check(true,"Reject dependency requirement newer than modpack pin");}
        var staging=Path.Combine(root,"staged"); Directory.CreateDirectory(staging);
        string Zip(string name, params string[] paths) {
            var path=Path.Combine(root,name);using var z=ZipFile.Open(path,ZipArchiveMode.Create);
            foreach(var p in paths) {using var w=new StreamWriter(z.CreateEntry(p).Open());w.Write("test");}return path;
        }
        ThunderstoreInstaller.Extract(Zip("core.zip","BepInExPack_Valheim/BepInEx/core/BepInEx.dll","BepInExPack_Valheim/BepInEx/core/BepInEx.Preloader.dll","BepInExPack_Valheim/winhttp.dll","BepInExPack_Valheim/doorstop_config.ini"),staging,PackageId.Parse("denikson-BepInExPack_Valheim-5.4.2350"));
        ThunderstoreInstaller.Extract(Zip("mod.zip","BetterArchery/plugins/BetterArchery.dll","BetterArchery/plugins/SFX/test.wav","manifest.json","icon.png"),staging,PackageId.Parse("ishid4-BetterArchery-2.0.0"));
        check(File.Exists(Path.Combine(staging,"BepInEx/plugins/ishid4-BetterArchery/SFX/test.wav")),"Route nested plugin assets alongside DLL");
        check(!File.Exists(Path.Combine(staging,"manifest.json")),"Do not install package metadata as game files");
        try {ThunderstoreInstaller.Extract(Zip("bad.zip","../escape.dll"),staging,PackageId.Parse("Bad-Mod-1.0.0"));throw new Exception("Expected traversal rejection");}
        catch(InvalidDataException){check(!File.Exists(Path.Combine(root,"escape.dll")),"Reject ZIP traversal before writing outside staging");}
        ThunderstoreInstaller.ValidateLayout(staging);
        var cfg=Path.Combine(staging,"BepInEx/config/example.cfg");Directory.CreateDirectory(Path.GetDirectoryName(cfg)!);File.WriteAllText(cfg,"default");
        var files=Directory.GetFiles(staging,"*",SearchOption.AllDirectories).Select(p=>Path.GetRelativePath(staging,p)).ToList();
        var old=new InstalledManifest{Version="1.0.0",ManagedFiles=files};
        var profile=Path.Combine(root,"transaction-profile");ThunderstoreInstaller.Commit(staging,profile,null,old);
        File.WriteAllText(Path.Combine(profile,"BepInEx/config/example.cfg"),"player customization");
        var next=new InstalledManifest{Version="1.1.0",ManagedFiles=files};
        try {ThunderstoreInstaller.Commit(staging,profile,old,next,()=>throw new IOException("Injected activation failure"));throw new Exception("Expected rollback");}
        catch(IOException){check(File.ReadAllText(Path.Combine(profile,".bisheim-installed.json")).Contains("1.0.0"),"Activation failure restores previous profile and manifest");}
        ThunderstoreInstaller.Commit(staging,profile,old,next);
        check(File.ReadAllText(Path.Combine(profile,"BepInEx/config/example.cfg"))=="player customization","Update preserves player configuration");
        check(Directory.Exists(profile+".rollback"),"Update retains prior profile backup");
        Directory.Move(profile,profile+".displaced");ThunderstoreInstaller.Recover(profile);
        check(Directory.Exists(profile),"Recover interrupted profile swap");
        if(!live)return;
        using var http=new HttpClient{Timeout=TimeSpan.FromMinutes(5)};
        var actual=await new ThunderstoreClient(http).ResolveAsync();
        var liveStage=Path.Combine(root,"live-stage");Directory.CreateDirectory(liveStage);
        foreach(var package in actual.Packages){
            var archive=Path.Combine(root,package.FullName+".zip");
            var data=await http.GetByteArrayAsync(package.DownloadUrl);await File.WriteAllBytesAsync(archive,data);
            ThunderstoreInstaller.Extract(archive,liveStage,package.Id);
            Console.WriteLine($"DOWNLOADED {package.FullName} SHA256={Convert.ToHexString(SHA256.HashData(data))}");
        }
        ThunderstoreInstaller.ValidateLayout(liveStage);
        check(actual.Packages.Count==8,"Published modpack resolves and all eight archives download and extract");
        check(Directory.GetFiles(Path.Combine(liveStage,"BepInEx/plugins"),"*.dll",SearchOption.AllDirectories).Length==6,"Published pack installs the six expected plugin DLLs");
    }
}
