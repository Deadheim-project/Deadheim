using BepInEx;
using BepInEx.Bootstrap;
using BepInEx.Configuration;
using HarmonyLib;
using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;

namespace Deadheim.Menus
{
    /// <summary>
    /// Menus sem tecla (7.9.0): o que o jogador usa fica no hub do inventario (Tab) e o que e de
    /// admin fica na aba Admin do ESC -> Deadheim. As teclas que abriam essas janelas nos outros
    /// mods sao zeradas aqui, no cliente, porque o Deadheim nao compila aqueles mods:
    ///
    /// - NpcValheim: J (diario), F7 (Loja Deadcoins), F8 (Painel de Admin);
    /// - Montarias: U (menu) e o F8 fixo no codigo (aba de admin), barrado por patch; as dicas
    ///   que citavam a tecla passam a dizer "Central (Tab)";
    /// - AdaptiveNet: F8 (painel de rede) e F9 (marcar incidente).
    ///
    /// O PageUp/PageDown do RaidSystem foi tirado no proprio RaidSystem (mesmo repositorio).
    /// Teclas de acao continuam: H (montar, e entrar na arena), P na cama, G no barco, Shift+E.
    ///
    /// A tecla zerada tambem some do "Meus ajustes" (AjustesDados), para ninguem religa-la achando
    /// que e preferencia. Se o mod recarregar o cfg (ou alguem editar a mao), ela e zerada de novo.
    /// </summary>
    internal static class Atalhos
    {
        private readonly struct Tecla
        {
            public readonly string Guid, Secao, Chave;
            public Tecla(string guid, string secao, string chave) { Guid = guid; Secao = secao; Chave = chave; }
        }

        private static readonly Tecla[] Removidas =
        {
            new Tecla(Integracoes.NpcValheimGuid, "Quests", "JournalKey"),
            new Tecla(Integracoes.NpcValheimGuid, "VIP", "ShopKey"),
            new Tecla(Integracoes.NpcValheimGuid, "Admin", "PanelKey"),
            new Tecla(Integracoes.MontariasGuid, "Geral", "TeclaMenu"),
            new Tecla(Integracoes.AdaptiveNetGuid, "Diagnostics", "OverlayKey"),
            new Tecla(Integracoes.AdaptiveNetGuid, "Diagnostics", "IncidentMarkerKey"),
        };

        private static readonly HashSet<ConfigFile> _vigiados = new HashSet<ConfigFile>();
        private static Harmony _harmony;
        private static bool _feito;
        private static bool _zerando;

        public static void Init(Harmony harmony) => _harmony = harmony;

        /// <summary>
        /// A opcao e uma das teclas removidas? Usado pelo "Meus ajustes" para nao lista-la.
        /// </summary>
        public static bool Removida(string guid, string secao, string chave)
            => Removidas.Any(t => t.Guid == guid && t.Secao == secao && t.Chave == chave);

        /// <summary>
        /// Chamado todo frame pelo Plugin.Update; trabalha so no primeiro. No primeiro Update todos
        /// os plugins ja passaram pelo Awake (o Chainloader carrega tudo antes do primeiro frame),
        /// entao os ConfigEntry dos outros mods ja existem e os assemblies ja estao carregados.
        /// </summary>
        public static void Tick()
        {
            if (_feito) return;
            _feito = true;
            if (Application.isBatchMode) return; // servidor dedicado nao tem teclado

            foreach (Tecla tecla in Removidas) Zerar(tecla);
            BarrarAdminDasMontarias();
        }

        private static void Zerar(Tecla tecla)
        {
            if (!Chainloader.PluginInfos.TryGetValue(tecla.Guid, out PluginInfo info) || info?.Instance?.Config == null) return;
            ConfigFile config = info.Instance.Config;

            ConfigEntryBase entrada = config
                .Select(kv => kv.Value)
                .FirstOrDefault(e => e != null && e.Definition.Section == tecla.Secao && e.Definition.Key == tecla.Chave);
            if (entrada == null)
            {
                Debug.LogWarning($"[Deadheim] Atalhos: {tecla.Guid} [{tecla.Secao}] {tecla.Chave} nao existe; nada a tirar.");
                return;
            }

            ZerarEntrada(entrada);

            // Recarregar o cfg (o mod pode vigiar o arquivo) traria a tecla de volta.
            if (_vigiados.Add(config))
            {
                config.ConfigReloaded += (_, __) => ZerarTodasDe(tecla.Guid);
                config.SettingChanged += (_, e) =>
                {
                    if (!_zerando && e?.ChangedSetting != null && Removida(tecla.Guid, e.ChangedSetting.Definition.Section, e.ChangedSetting.Definition.Key))
                        ZerarEntrada(e.ChangedSetting);
                };
            }
        }

        private static void ZerarTodasDe(string guid)
        {
            foreach (Tecla tecla in Removidas)
                if (tecla.Guid == guid) Zerar(tecla);
        }

        private static void ZerarEntrada(ConfigEntryBase entrada)
        {
            object vazio = Vazio(entrada.SettingType);
            if (vazio == null || Equals(entrada.BoxedValue, vazio)) return;

            _zerando = true;
            try
            {
                entrada.BoxedValue = vazio;
                Debug.Log($"[Deadheim] Atalhos: tecla {entrada.Definition.Section}/{entrada.Definition.Key} removida (o menu esta no hub do inventario ou na aba Admin).");
            }
            catch (Exception ex)
            {
                Debug.LogWarning($"[Deadheim] Atalhos: nao consegui zerar {entrada.Definition.Key}: {ex.Message}");
            }
            finally
            {
                _zerando = false;
            }
        }

        private static object Vazio(Type tipo)
        {
            if (tipo == typeof(KeyCode)) return KeyCode.None;
            if (tipo == typeof(KeyboardShortcut)) return KeyboardShortcut.Empty;
            return null;
        }

        /// <summary>
        /// O Montarias abre a aba de admin com F8 direto no codigo (Input.GetKeyDown(KeyCode.F8) ->
        /// AdminMenu.Toggle), sem config. O AdminMenu.Toggle so e chamado dali: barrado sempre.
        /// A aba Admin do Deadheim abre a mesma tela pelo MountMenu.OpenAdmin.
        /// </summary>
        private static void BarrarAdminDasMontarias()
        {
            if (_harmony == null || !Integracoes.TemMontarias) return;
            try
            {
                Type adminMenu = AccessTools.TypeByName("ValheimMontarias.AdminMenu");
                var toggle = adminMenu != null ? AccessTools.Method(adminMenu, "Toggle") : null;
                if (toggle == null)
                {
                    Debug.LogWarning("[Deadheim] Atalhos: ValheimMontarias.AdminMenu.Toggle nao existe; o F8 do Montarias continua.");
                    return;
                }
                _harmony.Patch(toggle, prefix: new HarmonyMethod(typeof(Atalhos), nameof(SemToggleDoMontarias)));

                // As dicas do Montarias dizem "<tecla> abre o menu": com a tecla zerada sairia "None".
                Type settings = AccessTools.TypeByName("ValheimMontarias.MountSettings");
                var nomeTecla = settings != null ? AccessTools.Method(settings, "MenuKeyName") : null;
                if (nomeTecla != null)
                    _harmony.Patch(nomeTecla, postfix: new HarmonyMethod(typeof(Atalhos), nameof(NomeDaTeclaDoMontarias)));
            }
            catch (Exception ex)
            {
                Debug.LogWarning($"[Deadheim] Atalhos: nao consegui barrar o F8 do Montarias: {ex.Message}");
            }
        }

        private static bool SemToggleDoMontarias() => false;

        private static void NomeDaTeclaDoMontarias(ref string __result)
        {
            if (string.IsNullOrEmpty(__result) || __result == nameof(KeyCode.None)) __result = "Central (Tab)";
        }
    }
}
