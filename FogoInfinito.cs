using BepInEx.Configuration;
using Deadheim.Pvp;
using Deadheim.Vanilla;
using HarmonyLib;
using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using UnityEngine;

namespace Deadheim
{
    /// <summary>
    /// Fogo infinito (27/10/2026): fogueira, tocha, braseiro e lareira (tudo que e Fireplace) dentro de uma
    /// area marcada nao gasta lenha e fica sempre cheia. As areas sao circulos no chao, "Nome,x,z,raio|...",
    /// no mesmo formato das zonas do PvP; o admin marca parado no centro com fogo_infinito.
    /// Opcional: tambem nas zonas seguras (ilha inicial, SafeZones e territorios seguros).
    /// </summary>
    public static class FogoInfinito
    {
        internal const string Secao = "Fogo Infinito";

        public static ConfigEntry<string> Zonas;
        public static ConfigEntry<bool> NasZonasSeguras;

        private static List<PvpConfig.Zone> _zonas;

        // Fireplace.m_nview e privado no jogo: por reflexao (campo privado lido direto ja deu
        // FieldAccessException em runtime).
        private static readonly AccessTools.FieldRef<Fireplace, ZNetView> NView =
            AccessTools.FieldRefAccess<Fireplace, ZNetView>("m_nview");

        // Os que este mod ligou, para desligar quando a area sai (o prefab pode ja vir infinito).
        private static readonly HashSet<int> _ligadosAqui = new HashSet<int>();

        public static void Bind(ConfigFile config)
        {
            Zonas = Plugin.Synced(config.Bind(Secao, "Zonas", "",
                "Areas onde fogueiras e tochas nao gastam lenha: Nome,x,z,raio|Nome2,x,z,raio. O admin marca parado no " +
                "centro com o comando: fogo_infinito <raio> [nome]; fogo_infinito remover <nome>."));
            NasZonasSeguras = Plugin.Synced(config.Bind(Secao, "NasZonasSeguras", false,
                "Liga: o fogo tambem e infinito nas zonas seguras (ilha inicial, SafeZones do PvP e territorios seguros)."));
            Zonas.SettingChanged += (_, __) => _zonas = null;
        }

        private static List<PvpConfig.Zone> Lista => _zonas ?? (_zonas = PvpConfig.ParseZones(Zonas.Value, "Fogo Infinito"));

        public static bool Cobre(Vector3 ponto)
        {
            if (Zonas == null) return false;
            foreach (PvpConfig.Zone zona in Lista)
                if (zona.Contains(ponto)) return true;
            return NasZonasSeguras.Value && PvpZones.IsSafeArea(ponto);
        }

        [HarmonyPatch(typeof(Fireplace), "UpdateFireplace")]
        private static class AbastecerPatch
        {
            private static void Prefix(Fireplace __instance)
            {
                try
                {
                    int id = __instance.GetInstanceID();
                    if (!Cobre(__instance.transform.position))
                    {
                        if (_ligadosAqui.Remove(id)) __instance.m_infiniteFuel = false;
                        return;
                    }
                    if (!__instance.m_infiniteFuel)
                    {
                        __instance.m_infiniteFuel = true;
                        _ligadosAqui.Add(id);
                    }

                    // Cheia: o fogo aceso fica aceso e uma tocha posta vazia acende.
                    ZNetView nview = NView(__instance);
                    if (nview == null || !nview.IsValid() || !nview.IsOwner()) return;
                    ZDO zdo = nview.GetZDO();
                    if (zdo.GetFloat(ZDOVars.s_fuel) < __instance.m_maxFuel) zdo.Set(ZDOVars.s_fuel, __instance.m_maxFuel);
                }
                catch (Exception e)
                {
                    Debug.LogWarning("[Deadheim] Fogo infinito: " + e.Message);
                }
            }
        }

        // ------------------------------------------------------------------ admin

        [HarmonyPatch(typeof(Terminal), nameof(Terminal.InitTerminal))]
        private static class ComandoPatch
        {
            private static void Postfix()
            {
                new Terminal.ConsoleCommand("fogo_infinito",
                    "Admin: fogo_infinito <raio> [nome] marca aqui uma area de fogo infinito; fogo_infinito remover <nome>",
                    (Terminal.ConsoleEvent)(args => Comando(args)));
            }
        }

        private static void Comando(Terminal.ConsoleEventArgs args)
        {
            Player player = Player.m_localPlayer;
            if (player == null) return;
            if (!Admin.LocalPlayerIsAdmin())
            {
                args.Context.AddString("So admin.");
                return;
            }

            List<string> partes = Zonas.Value.Split(new[] { '|' }, StringSplitOptions.RemoveEmptyEntries)
                .Select(p => p.Trim()).Where(p => p.Length > 0).ToList();
            string novo;
            if (args.Length >= 3 && args[1].Equals("remover", StringComparison.OrdinalIgnoreCase))
            {
                string nome = Limpar(string.Join(" ", args.Args.Skip(2)));
                int antes = partes.Count;
                partes.RemoveAll(p => string.Equals(p.Split(',')[0].Trim(), nome, StringComparison.OrdinalIgnoreCase));
                if (partes.Count == antes)
                {
                    args.Context.AddString("Nao ha area de fogo infinito \"" + nome + "\".");
                    return;
                }
                novo = string.Join("|", partes);
                args.Context.AddString("Pedido enviado: area \"" + nome + "\" removida.");
            }
            else
            {
                if (args.Length < 2 || !float.TryParse(args[1], NumberStyles.Float, CultureInfo.InvariantCulture, out float raio)
                    || raio < 1f || raio > 1000f)
                {
                    args.Context.AddString("Uso: fogo_infinito <raio de 1 a 1000> [nome]  |  fogo_infinito remover <nome>");
                    return;
                }
                string nome = Limpar(args.Length >= 3 ? string.Join(" ", args.Args.Skip(2)) : "");
                if (nome.Length == 0) nome = "Fogo" + (partes.Count + 1);
                partes.RemoveAll(p => string.Equals(p.Split(',')[0].Trim(), nome, StringComparison.OrdinalIgnoreCase));
                Vector3 aqui = player.transform.position;
                partes.Add(string.Format(CultureInfo.InvariantCulture, "{0},{1:0.#},{2:0.#},{3:0.#}", nome, aqui.x, aqui.z, raio));
                novo = string.Join("|", partes);
                args.Context.AddString(string.Format(CultureInfo.InvariantCulture,
                    "Pedido enviado: area \"{0}\" de {1:0} m em volta de ({2:0}, {3:0}).", nome, raio, aqui.x, aqui.z));
            }

            // Pelo mesmo pedido do menu de ajustes: o servidor confere a adminlist e grava a cfg dele.
            if (!Ajustes.AjustesRede.Definir(Plugin.PluginGUID, Secao, Zonas.Definition.Key, novo))
                args.Context.AddString("Sem conexao com o servidor.");
        }

        private static string Limpar(string nome) =>
            new string((nome ?? "").Where(c => c != ',' && c != '|' && !char.IsControl(c)).ToArray()).Trim();
    }
}
