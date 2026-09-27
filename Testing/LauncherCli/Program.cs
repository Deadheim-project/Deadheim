// Usa o Deadheim Launcher sem a janela: o mesmo caminho do botao Jogar (manifest das
// configuracoes do launcher -> regra de atualizacao -> ModInstallerService -> perfil), so
// que sem abrir o Valheim. Instala no perfil de verdade (%APPDATA%\DeadheimLauncher).
//
//   launcher-cli [perfil]          instala/atualiza o que o manifest pede
//   launcher-cli [perfil] --check  so mostra o que faria
using System.IO;
using System.Net.Http;
using DeadheimLauncher.Models;
using DeadheimLauncher.Services;
using DeadheimLauncher.ViewModels;

string profileName = args.FirstOrDefault(a => !a.StartsWith("--")) ?? "Default";
bool checkOnly = args.Contains("--check");

var settings = new SettingsService().Load();
Console.WriteLine($"manifest: {settings.ManifestUrl}");
Console.WriteLine($"servidor: {settings.ServerHost}:{settings.ServerPort}");

using var http = new HttpClient();
var manifest = await new ManifestService(http).GetManifestAsync(settings.ManifestUrl);
Console.WriteLine($"pack: {manifest.PackVersion}  mods: {manifest.AllMods.Count()}");

var profiles = new ProfileService();
var profile = profiles.LoadOrCreate(profileName);

// Mod que saiu do manifest sai do disco, como no Jogar.
if (!checkOnly)
{
    var removed = profiles.RemoverModsForaDoManifest(profile, manifest.AllMods.Select(m => m.Id));
    foreach (var id in removed) Console.WriteLine($"removido (fora do manifest): {id}");
}

var installer = new ModInstallerService(http, new GitHubReleaseService(http), new ThunderstoreService(http), new HexiumService(http));
int failed = 0;
foreach (var mod in manifest.AllMods)
{
    bool enabled = mod.Required || profile.EnabledModIds.Contains(mod.Id);
    if (!enabled) continue;

    profile.InstalledVersions.TryGetValue(mod.Id, out var installed);
    bool onDisk = mod.Target != InstallTarget.Plugins
        ? ModInstallerService.PacoteEstruturalEstaInstalado(mod, profile.Name)
        : Directory.Exists(Path.Combine(AppPaths.ProfilePluginsDir(profile.Name), mod.Id));
    if (!MainViewModel.PrecisaAtualizar(installed, mod.Version, onDisk))
    {
        Console.WriteLine($"ok        {mod.Id} {installed}");
        continue;
    }

    if (checkOnly)
    {
        Console.WriteLine($"instalaria {mod.Id} {installed ?? "-"} -> {mod.Version} ({mod.Source})");
        continue;
    }

    try
    {
        var progress = new Progress<ModInstallProgress>(p => Console.WriteLine($"  {mod.Id}: {p.Status}"));
        string version = await installer.InstallAsync(mod, profile.Name, progress);
        profile.InstalledVersions[mod.Id] = version;
        if (!profile.EnabledModIds.Contains(mod.Id)) profile.EnabledModIds.Add(mod.Id);
        profiles.Save(profile);
        Console.WriteLine($"instalado {mod.Id} {version}");
    }
    catch (Exception ex)
    {
        failed++;
        Console.WriteLine($"FALHOU    {mod.Id}: {ex.Message}");
    }
}

Console.WriteLine(failed == 0 ? "PRONTO" : $"FALHAS: {failed}");
return failed == 0 ? 0 : 1;
