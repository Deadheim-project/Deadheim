using Deadheim.Vanilla;
using HarmonyLib;
using System;
using System.Linq;
using UnityEngine;

namespace Deadheim.Wards
{
    /// <summary>
    /// Admin edita a lista de permitidos de uma ward que nao e dele, so no cliente.
    /// O vanilla so deixa o proprio jogador se adicionar, e so com a ward desligada; aqui o admin
    /// assume o ZDO da ward (ClaimOwnership) e grava a lista direto.
    /// Console (F5): wardperm add|rem &lt;nome do jogador online&gt;, mirando na ward (ou dentro da area dela).
    /// </summary>
    [HarmonyPatch]
    internal static class WardAdmin
    {
        private static readonly System.Reflection.MethodInfo MetodoAdd = AccessTools.Method(typeof(PrivateArea), "AddPermitted");
        private static readonly System.Reflection.MethodInfo MetodoRemove = AccessTools.Method(typeof(PrivateArea), "RemovePermitted");
        private static readonly System.Reflection.MethodInfo MetodoCriador = AccessTools.Method(typeof(PrivateArea), "GetCreatorName");

        [HarmonyPatch(typeof(Terminal), nameof(Terminal.InitTerminal))]
        private static class ComandosPatch
        {
            private static void Postfix()
            {
                new Terminal.ConsoleCommand("wardperm", "add|rem <jogador online> - (admin) edita permitidos da ward mirada/em que voce esta",
                    args =>
                    {
                        try
                        {
                            if (args.Length < 3) { args.Context.AddString("wardperm add|rem <jogador online>"); return; }
                            Executar(args.Context.AddString, args[1].ToLowerInvariant(), string.Join(" ", args.Args, 2, args.Length - 2));
                        }
                        catch (Exception e) { args.Context.AddString("wardperm falhou: " + e.Message); }
                    });
            }
        }

        private static PrivateArea AlvoWard()
        {
            Player p = Player.m_localPlayer;
            if (p == null) return null;
            GameObject mira = p.GetHoverObject();
            PrivateArea area = mira != null ? mira.GetComponentInParent<PrivateArea>() : null;
            if (area != null) return area;
            return PrivateArea.m_allAreas
                .Where(a => a != null && Vector3.Distance(a.transform.position, p.transform.position) <= a.m_radius)
                .OrderBy(a => Vector3.Distance(a.transform.position, p.transform.position))
                .FirstOrDefault();
        }

        private static void Executar(Action<string> say, string modo, string nome)
        {
            if (!Admin.LocalPlayerIsAdmin()) { say("So admin."); return; }
            if (modo != "add" && modo != "rem") { say("Use add ou rem."); return; }

            PrivateArea ward = AlvoWard();
            if (ward == null) { say(L.T("ward.mire")); return; }

            ZNet.PlayerInfo? alvo = null;
            foreach (ZNet.PlayerInfo info in ZNet.instance.GetPlayerList())
                if (info.m_name.IndexOf(nome, StringComparison.OrdinalIgnoreCase) >= 0) { alvo = info; break; }
            if (alvo == null) { say(L.T("ward.nao_online", nome)); return; }

            ZDO zdo = ZDOMan.instance.GetZDO(alvo.Value.m_characterID);
            long id = zdo != null ? zdo.GetLong(ZDOVars.s_playerID, 0L) : 0L;
            if (id == 0L) { say("Nao achei o ID de " + alvo.Value.m_name + " (precisa estar carregado perto de voce)."); return; }

            ZNetView nv = ward.m_nview;
            if (nv == null || !nv.IsValid()) { say("Ward invalida."); return; }
            if (!nv.IsOwner()) nv.ClaimOwnership();

            // Metodos privados do PrivateArea: por reflexao (acesso direto pode dar MethodAccessException no jogo).
            if (modo == "add") MetodoAdd?.Invoke(ward, new object[] { id, alvo.Value.m_name });
            else MetodoRemove?.Invoke(ward, new object[] { id });
            string dono = MetodoCriador?.Invoke(ward, null) as string ?? "?";

            say($"{(modo == "add" ? "Adicionado" : "Removido")}: {alvo.Value.m_name} na ward de {dono}.");
        }
    }
}
