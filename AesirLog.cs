using BepInEx.Configuration;
using HarmonyLib;
using System;
using System.Collections.Generic;
using System.Text;
using UnityEngine;

namespace Deadheim
{
    /// <summary>
    /// Log dos itens VIP (Aesir), para religar um por um e ver se buga (02/11/2026).
    ///
    /// Quase todo Aesir roda no cliente do proprio VIP (IsLocalPlayerVip), entao o log local so
    /// mostra o que aconteceu no PC dele. Com EnviarAoServidor ligado, cada linha tambem vai ao
    /// servidor pelo RPC Deadheim_AesirLog e cai no LogOutput.log de la como
    /// "[Aesir&lt;N&gt;] &lt;jogador&gt;: msg". No proprio servidor (e no host) so o log local, como
    /// "[Deadheim Aesir&lt;N&gt;] msg".
    ///
    /// Contra spam: a mesma linha (item + texto) repetida dentro de 60 s nao sai de novo; quando
    /// a janela fecha sai uma vez com "(x N a mais)". O envio ao servidor tem teto de 30 linhas
    /// por minuto por cliente, e o servidor confere de novo (tamanho e teto por peer): um cliente
    /// modificado nao enche o log.
    /// </summary>
    [HarmonyPatch]
    internal static class AesirLog
    {
        public static ConfigEntry<bool> Ativo;
        public static ConfigEntry<bool> EnviarAoServidor;

        private const string Rpc = "Deadheim_AesirLog";
        private const int LinhasPorMinutoCliente = 30;
        // Folga sobre o teto do cliente: a linha de resumo das descartadas entra na janela seguinte.
        private const int LinhasPorMinutoServidor = 40;
        private const int TamanhoMaximo = 300;
        private const float JanelaRepeticao = 60f;

        public static void Bind(ConfigFile config)
        {
            const string secao = "Aesir - Logs";
            Ativo = Plugin.Synced(config.Bind(secao, "Ativo", true,
                "Log das decisoes dos itens VIP (liberou/negou e por que). Erros dos patches Aesir sempre vao ao log local."));
            EnviarAoServidor = Plugin.Synced(config.Bind(secao, "EnviarAoServidor", true,
                "As linhas do cliente tambem vao ao LogOutput.log do servidor ([Aesir<N>] jogador: msg), ate 30 por minuto por jogador."));
            config.SettingChanged += (_, __) => _estadoPendenteEm = Time.realtimeSinceStartup + 1f;
        }

        private static bool EhServidor => ZNet.instance != null && ZNet.instance.IsServer();

        /// <summary>Decisao de um item Aesir. Nao chame por frame sem <see cref="Pode"/>.</summary>
        public static void D(int item, string msg)
        {
            if (Ativo == null || !Ativo.Value || string.IsNullOrEmpty(msg)) return;
            Registrar(item, msg, false, null);
        }

        /// <summary>Excecao num patch Aesir. A pilha completa so vai ao log local, na primeira vez.</summary>
        public static void Erro(int item, Exception e)
        {
            if (e == null) return;
            string onde = "";
            if (e.StackTrace != null)
            {
                string primeira = e.StackTrace.Split('\n')[0].Trim();
                if (primeira.Length > 0) onde = " @ " + primeira;
            }
            Registrar(item, "ERRO " + e.GetType().Name + ": " + e.Message + onde, true, e);
        }

        /// <summary>Trava de tempo para log em caminho quente: true no maximo uma vez a cada <paramref name="segundos"/>.</summary>
        public static bool Pode(ref float ultimo, float segundos)
        {
            if (Ativo == null || !Ativo.Value) return false;
            float agora = Time.realtimeSinceStartup;
            if (agora - ultimo < segundos) return false;
            ultimo = agora;
            return true;
        }

        // ------------------------------------------------------------------ agrupamento

        private class Repeticao { public float Desde; public int Vezes; public bool Erro; }

        private static readonly Dictionary<(int, string), Repeticao> _recentes = new Dictionary<(int, string), Repeticao>();
        private static readonly List<(int, string)> _expiradas = new List<(int, string)>();

        private static void Registrar(int item, string msg, bool erro, Exception e)
        {
            float agora = Time.realtimeSinceStartup;
            var chave = (item, msg);
            if (_recentes.TryGetValue(chave, out Repeticao r) && agora - r.Desde < JanelaRepeticao)
            {
                r.Vezes++;
                return;
            }
            if (_recentes.Count > 256) Descarregar(true);
            _recentes[chave] = new Repeticao { Desde = agora, Erro = erro };
            Emitir(item, msg, erro, e);
        }

        /// <summary>Fecha as janelas vencidas (ou todas) soltando a linha "(x N a mais)".</summary>
        private static void Descarregar(bool todas)
        {
            float agora = Time.realtimeSinceStartup;
            _expiradas.Clear();
            foreach (var kv in _recentes)
                if (todas || agora - kv.Value.Desde >= JanelaRepeticao) _expiradas.Add(kv.Key);
            foreach (var chave in _expiradas)
            {
                Repeticao r = _recentes[chave];
                _recentes.Remove(chave);
                if (r.Vezes > 0) Emitir(chave.Item1, chave.Item2 + " (x" + r.Vezes + " a mais em " + (int)JanelaRepeticao + " s)", r.Erro, null);
            }
        }

        private static void Emitir(int item, string msg, bool erro, Exception e)
        {
            string linha = "[Deadheim Aesir" + item + "] " + msg;
            if (erro) Debug.LogError(e != null ? linha + "\n" + e : linha);
            else Debug.Log(linha);

            if (EnviarAoServidor == null || !EnviarAoServidor.Value || EhServidor) return;
            // Erro vai ao servidor mesmo com Ativo desligado; decisao so com Ativo (ja filtrado em D).
            Enviar(item, msg);
        }

        // ------------------------------------------------------------------ cliente -> servidor

        private static float _janelaEnvio = -999f;
        private static int _enviadas;
        private static int _descartadas;

        private static void Enviar(int item, string msg)
        {
            float agora = Time.realtimeSinceStartup;
            if (agora - _janelaEnvio >= 60f) AbrirJanelaEnvio(agora);
            if (_enviadas >= LinhasPorMinutoCliente) { _descartadas++; return; }
            if (MandarRpc(item, msg)) _enviadas++;
        }

        private static void AbrirJanelaEnvio(float agora)
        {
            int descartadas = _descartadas;
            _janelaEnvio = agora;
            _enviadas = 0;
            _descartadas = 0;
            if (descartadas > 0 && MandarRpc(0, descartadas + " linha(s) nao enviada(s) no ultimo minuto (limite de " + LinhasPorMinutoCliente + "/min; ver o log do cliente)."))
                _enviadas++;
        }

        private static bool MandarRpc(int item, string msg)
        {
            ZRoutedRpc rpc = ZRoutedRpc.instance;
            if (rpc == null || ZNet.instance == null) return false;
            // Desconectado o id do servidor e 0, que no ZRoutedRpc quer dizer "todo mundo".
            long servidor = rpc.GetServerPeerID();
            if (servidor == 0L) return false;
            if (msg.Length > TamanhoMaximo) msg = msg.Substring(0, TamanhoMaximo);
            ZPackage pkg = new ZPackage();
            pkg.Write(item);
            pkg.Write(msg);
            rpc.InvokeRoutedRPC(servidor, Rpc, pkg);
            return true;
        }

        // ------------------------------------------------------------------ servidor

        private static readonly Dictionary<long, (float desde, int linhas, bool avisado)> _porPeer = new Dictionary<long, (float, int, bool)>();

        private static void RPC_AesirLog(long sender, ZPackage pkg)
        {
            if (!EhServidor || pkg == null) return;
            long real = RemetenteRpc.Real(sender, out _);
            if (real == 0L) return;

            int item;
            string msg;
            try
            {
                item = pkg.ReadInt();
                msg = pkg.ReadString();
            }
            catch { return; }
            if (item < 0 || item > 99 || string.IsNullOrEmpty(msg)) return;

            ZNetPeer peer = ZNet.instance.GetPeer(real);
            string nome = peer != null && !string.IsNullOrEmpty(peer.m_playerName) ? peer.m_playerName : real.ToString();

            float agora = Time.realtimeSinceStartup;
            if (!_porPeer.TryGetValue(real, out var conta) || agora - conta.desde >= 60f)
            {
                if (_porPeer.Count > 512) _porPeer.Clear();
                conta = (agora, 0, false);
            }
            if (conta.linhas >= LinhasPorMinutoServidor)
            {
                if (!conta.avisado) Debug.LogWarning("[Aesir] " + Limpar(nome, 40) + ": passou de " + LinhasPorMinutoServidor + " linhas no minuto; descartando ate a proxima janela.");
                _porPeer[real] = (conta.desde, conta.linhas, true);
                return;
            }
            _porPeer[real] = (conta.desde, conta.linhas + 1, conta.avisado);

            Debug.Log("[Aesir" + item + "] " + Limpar(nome, 40) + ": " + Limpar(msg, TamanhoMaximo));
        }

        /// <summary>Uma linha so, sem caractere de controle e com tamanho limitado.</summary>
        private static string Limpar(string texto, int maximo)
        {
            if (texto.Length > maximo) texto = texto.Substring(0, maximo) + "...";
            var sb = new StringBuilder(texto.Length);
            foreach (char c in texto) sb.Append(char.IsControl(c) ? ' ' : c);
            return sb.ToString();
        }

        [HarmonyPatch(typeof(Game), "Start")]
        private static class RegistrarRpc
        {
            private static void Postfix()
            {
                if (ZRoutedRpc.instance == null) return;
                ZRoutedRpc.instance.Register<ZPackage>(Rpc, RPC_AesirLog);
                _estadoPendenteEm = Time.realtimeSinceStartup + 1f;
            }
        }

        // ------------------------------------------------------------------ estado dos itens

        private static float _estadoPendenteEm = -1f;
        private static float _proximaLimpeza;

        /// <summary>Chamado pelo Plugin.Update: fecha as repeticoes vencidas e escreve o estado pendente.</summary>
        internal static void Tick()
        {
            float agora = Time.realtimeSinceStartup;
            if (agora >= _proximaLimpeza)
            {
                _proximaLimpeza = agora + 5f;
                if (_recentes.Count > 0) Descarregar(false);
                if (_descartadas > 0 && agora - _janelaEnvio >= 60f) AbrirJanelaEnvio(agora);
            }

            // No boot (Game.Start) e a cada mudanca de cfg, 1 s depois: um Reload muda varias
            // chaves de uma vez e sai uma linha so. So no servidor (ou host), dono das configs.
            if (_estadoPendenteEm > 0f && agora >= _estadoPendenteEm)
            {
                _estadoPendenteEm = -1f;
                if (EhServidor) Debug.Log("[Deadheim Aesir] " + Estado());
            }
        }

        private static string Liga(ConfigEntry<bool> c) => c == null ? "?" : c.Value ? "ON" : "OFF";

        internal static string Estado()
        {
            var sb = new StringBuilder("Aesir: ");
            bool so5 = AesirConfig.Aesir5SoVip?.Value ?? true;
            bool so11 = AesirConfig.Aesir11SoVip?.Value ?? true;
            sb.Append("5=").Append(so5 ? "VIP" : "TODOS");
            sb.Append(" plantioArea=").Append(AesirConfig.AesirPlantioArea?.Value.ToString() ?? "?");
            sb.Append(" 6=").Append(Liga(AesirConfig.Aesir6ReplantarAtivo));
            sb.Append(" 7=").Append(Liga(AesirConfig.Aesir7AbastecerAreaAtivo));
            sb.Append(" 8=").Append(Liga(AesirConfig.Aesir8SemDesgasteAtivo));
            sb.Append(" 9=").Append(Liga(AesirConfig.Aesir9TempoRestanteAtivo));
            sb.Append(" 10=").Append(Liga(AesirPullConfig.Enabled));
            sb.Append(" 11=").Append(so11 ? "VIP" : "TODOS");
            sb.Append(" 12=").Append(Liga(AesirConfig.Aesir12RepararAreaAtivo));
            sb.Append(" 13=").Append(Liga(AesirCraftConfig.Enabled));
            sb.Append(" 14=").Append(Liga(AesirConfig.Aesir14DepositarAtivo));
            sb.Append(" 15=").Append(Liga(AesirConfig.Aesir15OrdenarAtivo));
            sb.Append(" 16=").Append(Liga(AesirConfig.Aesir16EtiquetaAtivo));
            sb.Append(" 17=").Append(Liga(AesirConfig.Aesir17RestedAtivo));
            sb.Append(" 18=").Append(Liga(AesirConfig.Aesir18DoisPontos));
            sb.Append(" 19=").Append(VipSlotsConfig.Enabled == null ? "?"
                : VipSlotsConfig.Enabled.Value ? "ON(+" + VipSlotsConfig.ReservedSlots.Value + " vagas, limite " + VipSlotsConfig.PlayerLimit.Value + ")" : "OFF");
            sb.Append(" 24=").Append(Liga(Plantas.Enabled));
            sb.Append(" 26=").Append(Liga(AesirConfig.Aesir26Ativo));
            sb.Append(" 35=").Append(Liga(Transmog.Enabled));

            sb.Append("; 5 ").Append(so5 ? "so VIP" : "liberado a todos");
            sb.Append("; 11 ").Append(so11 ? "so VIP" : "liberado a todos");
            bool craftVipOnly = AesirCraftConfig.VipOnly?.Value ?? true;
            sb.Append("; AesirCraft VipOnly=").Append(craftVipOnly ? "true" : "false (todos usam se ligar)");
            if (AesirPullConfig.OnlyOwnerVip != null) sb.Append("; AesirContainers OnlyOwnerVip=").Append(AesirPullConfig.OnlyOwnerVip.Value);
            if (Transmog.RequireVip != null) sb.Append("; Transmog RequireVip=").Append(Transmog.RequireVip.Value);
            sb.Append("; logs Ativo=").Append(Liga(Ativo)).Append(" EnviarAoServidor=").Append(Liga(EnviarAoServidor));
            return sb.ToString();
        }
    }
}
