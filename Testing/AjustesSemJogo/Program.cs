// Teste da opcao Deadheim do menu do ESC SEM o jogo: o AjustesRede.cs e o AjustesDados.cs de
// verdade, com o Harmony de verdade aplicando os patches, um servidor e dois clientes
// simulados trocando pacotes (Stubs.cs) e o ConfigFile do BepInEx gravando em disco.
// A janela (Unity) fica de fora: o que ela receberia e anotado pelo AjustesJanela do Stubs.cs.
// O menu do ESC e a janela sao testados no jogo, pelo passo "ajustes" do run-pvp-test.ps1.
//
//   dotnet build Testing\AjustesSemJogo -c Release
//   Testing\AjustesSemJogo\bin\Release\net48\AjustesSemJogo.exe      (Linux: mono ...)
//
// Sai com o numero de falhas (0 = tudo certo).
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Runtime.Serialization;
using BepInEx;
using BepInEx.Bootstrap;
using BepInEx.Configuration;
using Deadheim.Ajustes;
using HarmonyLib;

namespace ServerSync { public abstract class OwnConfigEntryBase { public bool SynchronizedConfig = true; } public class SyncedConfigEntry<T> : OwnConfigEntryBase { } }
class ConfigurationManagerAttributes { public bool? ReadOnly; public bool? Browsable; public bool? IsAdminOnly; }
// O ServerSync poe o dele (so ReadOnly) antes das tags do mod; o RaidSystem traz o seu com o Browsable.
namespace ServerSync { class ConfigurationManagerAttributes { public bool? ReadOnly; } }
public class FakePlugin : BaseUnityPlugin { }

static class Program
{
    const string Admin = "76561198000000001";
    const string Jogador = "76561198000000099";
    const long ServidorId = 1, AdminId = 1001, JogadorId = 1099;

    static int _ok, _falhas;
    static string _conexaoHost;     // null = chamada local (host falando com o proprio servidor)
    static long _remetente;
    static readonly string Pasta = Path.Combine(Path.GetTempPath(), "ajustes-e2e-" + Guid.NewGuid().ToString("N"));

    static void Check(string nome, bool ok, string detalhe = "")
    {
        Console.WriteLine((ok ? "ok    " : "FALHA ") + nome + (detalhe.Length > 0 ? "  -- " + detalhe : ""));
        if (ok) _ok++; else _falhas++;
    }

    static ConfigEntry<T> Sync<T>(ConfigEntry<T> e) { AddTag(e, new ServerSync.SyncedConfigEntry<T>()); return e; }
    static void AddTag(ConfigEntryBase e, object tag)
    {
        var campo = typeof(ConfigDescription).GetField("<Tags>k__BackingField", BindingFlags.NonPublic | BindingFlags.Instance);
        campo.SetValue(e.Description, (e.Description.Tags ?? new object[0]).Concat(new[] { tag }).ToArray());
    }

    static ConfigFile NovoCfg(string guid) => new ConfigFile(Path.Combine(Pasta, guid + ".cfg"), true);

    static void AddPlugin(string guid, string nome, string versao, ConfigFile cfg)
    {
        var plugin = (FakePlugin)FormatterServices.GetUninitializedObject(typeof(FakePlugin));
        // Sem gravar o m_CachedPtr: o SetValue dispara o construtor estatico do UnityEngine.Object,
        // que chama o motor da Unity (SecurityException fora do jogo). O AjustesDados so usa ?. nos plugins.
        (typeof(BaseUnityPlugin).GetField("<Config>k__BackingField", BindingFlags.NonPublic | BindingFlags.Instance)
            ?? throw new Exception("Config backing nao existe")).SetValue(plugin, cfg);
        var info = (PluginInfo)FormatterServices.GetUninitializedObject(typeof(PluginInfo));
        typeof(PluginInfo).GetProperty("Metadata").GetSetMethod(true).Invoke(info, new object[] { new BepInPlugin(guid, nome, versao) });
        typeof(PluginInfo).GetProperty("Instance").GetSetMethod(true).Invoke(info, new object[] { plugin });
        Chainloader.PluginInfos[guid] = info;
    }

    /// <summary>A rede: cliente -> servidor passa pelo ZRpc.HandlePackage (com a conexao do cliente), como no jogo.</summary>
    static void Rotear(long destino, string nome, ZPackage pkg)
    {
        var rpc = ZRoutedRpc.instance;
        if (nome == AjustesRede.ParaServidor)
        {
            if (_conexaoHost == null) { rpc.Deliver(nome, _remetente, pkg); return; }
            var conexao = new ZRpc { Socket = new FakeSocket { Host = _conexaoHost }, OnPackage = p => rpc.Deliver(nome, _remetente, p) };
            AccessTools.Method(typeof(ZRpc), "HandlePackage").Invoke(conexao, new object[] { pkg });
        }
        else rpc.Deliver(nome, ServidorId, pkg);
    }

    static void Como(string host, long remetente) { _conexaoHost = host; _remetente = remetente; AjustesJanela.Limpar(); }

    static object ConexaoAtual() => AccessTools.Field(typeof(AjustesRede), "_conexaoAtual").GetValue(null);

    static int Main()
    {
        Directory.CreateDirectory(Pasta);
        // O ConfigFile abre o BepInEx.cfg no construtor estatico: aponta para a pasta do teste.
        typeof(Paths).GetProperty("BepInExConfigPath").GetSetMethod(true).Invoke(null, new object[] { Path.Combine(Pasta, "BepInEx.cfg") });
        try { return Rodar(); }
        finally { try { Directory.Delete(Pasta, true); } catch { } }
    }

    static int Rodar()
    {
        // --------------------------------------------------------------- mundo
        var dh = NovoCfg("Detalhes.Deadheim");
        var cartografia = Sync(dh.Bind("Server config", "CartographyTableAmount", 100, "Quantidade de cada material da mesa de cartografia."));
        var raio = Sync(dh.Bind("Server config", "WardRadius", 150, new ConfigDescription("Raio", new AcceptableValueRange<int>(10, 500))));
        var killFeed = Sync(dh.Bind("PvP - Geral", "KillFeed", true, "Anuncia mortes."));
        var modo = Sync(dh.Bind("PvP - Zonas", "StartIslandMode", "Island", new ConfigDescription("Modo", new AcceptableValueList<string>("Off", "Island", "Radius"))));
        var oculta = dh.Bind("Interno", "Segredo", "x", new ConfigDescription("nao mostrar", null, new ConfigurationManagerAttributes { Browsable = false }));
        var tecla = dh.Bind("Cliente", "Tecla", "F7", "Tecla local do jogador.");
        // Jotunn: IsAdminOnly e sincronizada. ServerSync com SynchronizedConfig=false e local.
        dh.Bind("Jotunn", "SoAdmin", 1, new ConfigDescription("x", null, new ConfigurationManagerAttributes { IsAdminOnly = true }));
        var naoSincronizada = dh.Bind("Cliente", "Local pelo ServerSync", true, "Local mesmo com o ServerSync.");
        AddTag(naoSincronizada, new ServerSync.SyncedConfigEntry<bool> { SynchronizedConfig = false });
        int mudancasCartografia = 0;
        cartografia.SettingChanged += (_, __) => mudancasCartografia++;
        AddPlugin("Detalhes.Deadheim", "Detalhes.Deadheim", "7.1.0", dh);

        var raid = NovoCfg("Detalhes.RaidSystem");
        raid.Bind("9 - Client", "Menu Key", "PageUp", "Abre o menu de raide.");
        // Como o RaidSystem 2.1.1 no cliente: nao sincronizada (segredo) e Browsable=false.
        var webhook = raid.Bind("7 - Integration", "Discord Webhook URL", "", new ConfigDescription("Server-side only.", null,
            new ServerSync.ConfigurationManagerAttributes(), new ConfigurationManagerAttributes { Browsable = false }));
        AddTag(webhook, new ServerSync.SyncedConfigEntry<string> { SynchronizedConfig = false });
        Sync(raid.Bind("2 - Raid Rules", "Raid Hours (UTC)", "18-22", "Horario."));
        AddPlugin("Detalhes.RaidSystem", "Detalhes.RaidSystem", "2.1.0", raid);

        // Mod grande: 400 opcoes com descricao longa (CLLC/Jewelcrafting sao assim).
        var grande = NovoCfg("org.bepinex.plugins.creaturelevelcontrol");
        for (int i = 0; i < 400; i++)
            Sync(grande.Bind($"{i / 20:00} - Secao", $"Opcao {i:000}", i * 1.5f, string.Join(" ", Enumerable.Repeat("descricao comprida da opcao", 20))));
        AddPlugin("org.bepinex.plugins.creaturelevelcontrol", "CreatureLevelAndLootControl", "5.0.5", grande);

        var semNada = NovoCfg("Azumatt.Vazio");
        semNada.Bind("X", "Escondida", 1, new ConfigDescription("", null, new ConfigurationManagerAttributes { Browsable = false }));
        AddPlugin("Azumatt.Vazio", "Vazio", "1.0.0", semNada);

        ZNet.instance = new ZNet { Server = true, Dedicated = true, Admins = { Admin } };
        ZRoutedRpc.instance = new ZRoutedRpc { ServerId = ServidorId, Router = Rotear };

        // ------------------------------------------------------ patches e registro
        var harmony = new Harmony("teste.ajustes");
        // So as classes de patch do AjustesRede (o PatchAll do assembly tropecaria no FakePlugin, que e da Unity).
        foreach (Type tipoPatch in typeof(AjustesRede).GetNestedTypes(BindingFlags.NonPublic))
            if (tipoPatch.GetCustomAttributes(typeof(HarmonyPatch), false).Length > 0) harmony.CreateClassProcessor(tipoPatch).Patch();
        var patchHandle = Harmony.GetPatchInfo(AccessTools.Method(typeof(ZRpc), "HandlePackage"));
        Check("patch no ZRpc.HandlePackage (prefix + finalizer)", patchHandle != null && patchHandle.Prefixes.Count == 1 && patchHandle.Finalizers.Count == 1);
        AccessTools.Method(typeof(Game), "Start").Invoke(new Game(), null);
        Check("RPCs registrados no Game.Start", ZRoutedRpc.instance.Handlers.ContainsKey(AjustesRede.ParaServidor) && ZRoutedRpc.instance.Handlers.ContainsKey(AjustesRede.ParaCliente));

        // ------------------------------------------------------------- nao admin
        Como(Jogador, JogadorId);
        AjustesRede.PedirMods();
        Check("jogador comum: servidor nega a lista", AjustesJanela.Negado != null && AjustesJanela.Mods == null, AjustesJanela.Negado ?? "");
        Check("jogador comum: servidor registra a tentativa", Debug.Linhas.Any(l => l.StartsWith("WARN") && l.Contains(Jogador) && l.Contains("sem ser admin")));

        Como(Jogador, AdminId);   // escreve no pacote o id de um admin
        AjustesRede.Definir("Detalhes.Deadheim", "Server config", "CartographyTableAmount", "1");
        Check("id de admin forjado no pacote nao passa", AjustesJanela.Negado != null && cartografia.Value == 100, $"valor={cartografia.Value}");
        Check("conexao atual volta a null depois do pacote", ConexaoAtual() == null);

        Como(null, JogadorId);    // sem conexao num dedicado
        AjustesRede.PedirMods();
        Check("chamada sem conexao num dedicado e negada", AjustesJanela.Negado != null);

        // ------------------------------------------------------------------ admin
        Como(Admin, AdminId);
        AjustesRede.PedirMods();
        var mods = AjustesJanela.Mods;
        Check("admin: recebe a lista de mods", mods != null && mods.Count == 3, mods == null ? "null" : string.Join(", ", mods.Select(m => $"{m.Nome}({m.Itens})")));
        Check("ordem: Deadheim, proprios, resto", mods != null && mods.Select(m => m.Guid).SequenceEqual(new[] { "Detalhes.Deadheim", "Detalhes.RaidSystem", "org.bepinex.plugins.creaturelevelcontrol" }));
        Check("nome amigavel tira o prefixo do GUID", mods != null && mods[0].Nome == "Deadheim" && mods[1].Nome == "RaidSystem" && mods[2].Nome == "CreatureLevelAndLootControl");
        Check("contagem ignora Browsable=false", mods != null && mods[0].Itens == 7, mods == null ? "" : mods[0].Itens.ToString());
        Check("mod so com opcao oculta fica fora", mods != null && mods.All(m => m.Guid != "Azumatt.Vazio"));

        AjustesRede.PedirItens("Detalhes.Deadheim");
        var itens = AjustesJanela.Itens;
        Check("admin: recebe as opcoes do Deadheim", itens != null && AjustesJanela.ItensGuid == "Detalhes.Deadheim" && itens.Count == 7, itens == null ? "null" : string.Join(", ", itens.Select(i => i.Chave)));
        ItemAjuste Item(string chave) => itens?.FirstOrDefault(i => i.Chave == chave);
        Check("secoes em ordem alfabetica", itens != null && itens.Select(i => i.Secao).SequenceEqual(itens.Select(i => i.Secao).OrderBy(s => s, StringComparer.OrdinalIgnoreCase)));
        Check("CartographyTableAmount: texto, sincronizado, 100", Item("CartographyTableAmount") is { } c && c.Editor == TipoEditor.Texto && c.Sincronizado && c.Valor == "100" && c.Padrao == "100");
        Check("WardRadius: faixa 10 a 500", Item("WardRadius")?.Faixa == "10 a 500");
        Check("KillFeed: liga/desliga", Item("KillFeed") is { } k && k.Editor == TipoEditor.LigaDesliga && k.Valor == "true");
        Check("StartIslandMode: lista com 3 opcoes", Item("StartIslandMode") is { } m && m.Editor == TipoEditor.Lista && m.Opcoes.SequenceEqual(new[] { "Off", "Island", "Radius" }));
        Check("Tecla: opcao local marcada como 'so no servidor'", Item("Tecla") is { } t && !t.Sincronizado);
        Check("Segredo (Browsable=false) nao vai", Item("Segredo") == null);

        AjustesRede.Definir("Detalhes.Deadheim", "Server config", "CartographyTableAmount", "123");
        var r = AjustesJanela.Resultado;
        Check("admin muda CartographyTableAmount -> 123", r != null && (bool)r[0] && (string)r[4] == "123" && cartografia.Value == 123, r == null ? "sem resposta" : string.Join("|", r));
        Check("SettingChanged disparou no servidor (e o que o ServerSync escuta)", mudancasCartografia == 1);
        string arquivo = File.ReadAllText(dh.ConfigFilePath);
        Check("cfg do servidor salvo em disco", arquivo.Contains("CartographyTableAmount = 123"));
        Check("servidor registra quem mudou o que", Debug.Linhas.Any(l => l.Contains($"{Admin} mudou Detalhes.Deadheim [Server config] CartographyTableAmount: '100' -> '123'")));

        Como(Admin, AdminId);
        AjustesRede.Definir("Detalhes.Deadheim", "Server config", "CartographyTableAmount", "abc");
        r = AjustesJanela.Resultado;
        Check("valor invalido e recusado e nada muda", r != null && !(bool)r[0] && ((string)r[5]).Contains("invalido") && cartografia.Value == 123, r == null ? "" : (string)r[5]);

        Como(Admin, AdminId);
        AjustesRede.Definir("Detalhes.Deadheim", "Server config", "WardRadius", "99999");
        r = AjustesJanela.Resultado;
        Check("faixa: 99999 vira 500 e a resposta traz o valor real", r != null && (bool)r[0] && (string)r[4] == "500" && raio.Value == 500);

        Como(Admin, AdminId);
        AjustesRede.Definir("Detalhes.Deadheim", "PvP - Geral", "KillFeed", "false");
        Check("liga/desliga: KillFeed -> false", killFeed.Value == false);
        Como(Admin, AdminId);
        AjustesRede.Definir("Detalhes.Deadheim", "PvP - Zonas", "StartIslandMode", "Radius");
        Check("lista: StartIslandMode -> Radius", modo.Value == "Radius");

        Como(Admin, AdminId);
        AjustesRede.Definir("Detalhes.Deadheim", "Interno", "Segredo", "y");
        Check("opcao oculta nao aceita mudanca", AjustesJanela.Resultado is { } ro && !(bool)ro[0] && oculta.Value == "x");
        Como(Admin, AdminId);
        AjustesRede.Definir("Detalhes.Deadheim", "Nao", "Existe", "1");
        Check("opcao inexistente: erro claro", AjustesJanela.Resultado is { } rn && !(bool)rn[0] && (string)rn[5] == "Opcao nao encontrada.");
        Como(Admin, AdminId);
        AjustesRede.Definir("Mod.Que.Nao.Existe", "a", "b", "1");
        Check("mod inexistente: erro claro, sem excecao", AjustesJanela.Resultado is { } rm && !(bool)rm[0]);

        Como(Admin, AdminId);
        AjustesRede.PedirItens("org.bepinex.plugins.creaturelevelcontrol");
        Check("mod com 400 opcoes chega inteiro (comprimido)", AjustesJanela.Itens?.Count == 400 && AjustesJanela.Itens[399].Valor == "598.5");
        var descricao = AjustesJanela.Itens?[0].Descricao ?? "";
        Check("descricao longa e cortada em 500", descricao.Length == 500 && descricao.EndsWith("..."), descricao.Length.ToString());

        // --------------------------------------------------- host nao dedicado
        ZNet.instance.Dedicated = false;
        Como(null, ServidorId);
        AjustesRede.PedirMods();
        Check("host de mundo nao dedicado (chamada local) e admin", AjustesJanela.Mods != null);
        ZNet.instance.Dedicated = true;

        // ------------------------------------------------------ "Meus ajustes"
        var locais = AjustesDados.Mods(local: true);
        Check("Meus ajustes: so mods com opcao local", locais.Select(m => m.Guid).SequenceEqual(new[] { "Detalhes.Deadheim", "Detalhes.RaidSystem" }), string.Join(", ", locais.Select(m => $"{m.Guid}({m.Itens})")));
        Check("webhook do RaidSystem fora de Meus ajustes (Browsable no 2o ConfigurationManagerAttributes)",
            AjustesDados.Itens("Detalhes.RaidSystem", local: true).All(i => i.Chave != "Discord Webhook URL"));
        var itensLocais = AjustesDados.Itens("Detalhes.Deadheim", local: true);
        Check("Meus ajustes do Deadheim: so as locais (IsAdminOnly fica de fora)",
            itensLocais.Select(i => i.Chave).OrderBy(k => k).SequenceEqual(new[] { "Local pelo ServerSync", "Tecla" }), string.Join(", ", itensLocais.Select(i => i.Chave)));
        bool okLocal = AjustesDados.Aplicar("Detalhes.Deadheim", "Server config", "CartographyTableAmount", "1", local: true, out _, out _, out string erroLocal);
        Check("Meus ajustes recusa opcao sincronizada", !okLocal && erroLocal == "Essa opcao e do servidor." && cartografia.Value == 123, erroLocal);
        okLocal = AjustesDados.Aplicar("Detalhes.Deadheim", "Cliente", "Tecla", "F8", local: true, out string antes, out string depois, out _);
        Check("Meus ajustes grava opcao local", okLocal && antes == "F7" && depois == "F8" && tecla.Value == "F8" && File.ReadAllText(dh.ConfigFilePath).Contains("Tecla = F8"));

        Check("nenhum erro no log", !Debug.Linhas.Any(l => l.StartsWith("ERRO")), string.Join(" / ", Debug.Linhas.Where(l => l.StartsWith("ERRO"))));
        Console.WriteLine();
        Console.WriteLine("log do servidor:");
        foreach (string l in Debug.Linhas) Console.WriteLine("  " + l);
        Console.WriteLine();
        Console.WriteLine(_falhas == 0 ? $"TUDO OK ({_ok} checagens)" : $"FALHAS: {_falhas} de {_ok + _falhas}");
        return _falhas;
    }
}
