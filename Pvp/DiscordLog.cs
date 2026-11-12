using BepInEx.Configuration;
using HarmonyLib;
using System;
using System.Collections.Generic;
using System.IO;
using System.Net;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading;
using UnityEngine;

namespace Deadheim.Pvp
{
    /// <summary>
    /// Servidor: manda para o Discord (webhook) o log de PvP, mortes, gritos (shout) e conquistas de
    /// castelo. A URL fica so no cfg do servidor (nao e Synced: o cliente nunca a recebe). Sem URL, nada sai.
    /// Envio em fila numa thread propria (1 mensagem a cada 0,6 s) para nao travar o jogo nem estourar o
    /// limite do Discord.
    /// </summary>
    internal static class DiscordLog
    {
        private static ConfigEntry<string> _webhook;
        private static ConfigEntry<string> _conquestWebhook;
        private static ConfigEntry<bool> _pvp;
        private static ConfigEntry<bool> _deaths;
        private static ConfigEntry<bool> _shouts;
        private static ConfigEntry<bool> _conquests;

        private static readonly Queue<KeyValuePair<string, string>> _queue = new Queue<KeyValuePair<string, string>>();
        private static Thread _worker;
        private static readonly Regex Tags = new Regex("<[^>]+>", RegexOptions.Compiled);

        public static void Bind(ConfigFile config)
        {
            const string s = "PvP - Discord";
            // config.Bind direto (sem Plugin.Synced): a URL e segredo do servidor.
            _webhook = config.Bind(s, "WebhookUrl", "", "Webhook do canal de logs (PvP, mortes, gritos). Vazio = desligado. So vale no servidor.");
            _conquestWebhook = config.Bind(s, "ConquestWebhookUrl", "", "Webhook do canal de conquistas de castelo. Vazio = usa o WebhookUrl.");
            _pvp = config.Bind(s, "LogPvp", true, "Abates PvP, bounty, PK e logout em combate.");
            _deaths = config.Bind(s, "LogDeaths", true, "Mortes de jogadores por PvE (monstro, queda, etc.).");
            _shouts = config.Bind(s, "LogShouts", true, "Gritos (shout) do chat.");
            _conquests = config.Bind(s, "LogConquests", true, "Conquistas de castelo do RaidSystem (mensagem especial).");
        }

        private static bool Server => ZNet.instance != null && ZNet.instance.IsServer();

        public static void Pvp(string text)
        {
            if (_pvp != null && _pvp.Value) Enqueue(_webhook?.Value, Clean(text), 0xE67E22, "⚔️ PvP");
        }

        public static void Death(string text)
        {
            if (_deaths != null && _deaths.Value) Enqueue(_webhook?.Value, Clean(text), 0x95A5A6, "💀 Morte");
        }

        public static void Shout(string player, string text)
        {
            if (_shouts != null && _shouts.Value)
                Enqueue(_webhook?.Value, "**" + Escape(player) + "** gritou: " + Escape(text), 0xF1C40F, "📢 Grito");
        }

        public static void Conquest(string text)
        {
            if (_conquests == null || !_conquests.Value) return;
            string url = !string.IsNullOrWhiteSpace(_conquestWebhook?.Value) ? _conquestWebhook.Value : _webhook?.Value;
            Enqueue(url, "🏰 **CASTELO CONQUISTADO!** 🏰\n" + Escape(Clean(text)) + "\n\n*A guerra continua... quem sera o proximo a cair?*",
                0xFFD700, "👑 CONQUISTA");
        }

        private static string Clean(string text) => Tags.Replace(text ?? string.Empty, string.Empty);
        private static string Escape(string text) => (text ?? string.Empty).Replace("@", "@​");

        private static string Json(string s)
        {
            StringBuilder b = new StringBuilder(s.Length + 8);
            foreach (char c in s)
            {
                switch (c)
                {
                    case '"': b.Append("\\\""); break;
                    case '\\': b.Append("\\\\"); break;
                    case '\n': b.Append("\\n"); break;
                    case '\r': break;
                    case '\t': b.Append(' '); break;
                    default:
                        if (c < ' ') break;
                        b.Append(c);
                        break;
                }
            }
            return b.ToString();
        }

        private static void Enqueue(string url, string text, int color, string title)
        {
            if (!Server || string.IsNullOrWhiteSpace(url) || string.IsNullOrWhiteSpace(text)) return;
            if (text.Length > 1800) text = text.Substring(0, 1800);
            string body = "{\"username\":\"Deadheim\",\"allowed_mentions\":{\"parse\":[]},\"embeds\":[{\"title\":\"" + Json(title) +
                          "\",\"description\":\"" + Json(text) + "\",\"color\":" + color + ",\"timestamp\":\"" +
                          DateTime.UtcNow.ToString("o") + "\"}]}";
            lock (_queue)
            {
                if (_queue.Count > 200) return;
                _queue.Enqueue(new KeyValuePair<string, string>(url.Trim(), body));
                if (_worker == null)
                {
                    _worker = new Thread(Run) { IsBackground = true, Name = "DeadheimDiscord" };
                    _worker.Start();
                }
            }
        }

        private static void Run()
        {
            while (true)
            {
                KeyValuePair<string, string> item;
                lock (_queue)
                {
                    if (_queue.Count == 0) { _worker = null; return; }
                    item = _queue.Dequeue();
                }
                try
                {
                    using (WebClient client = new WebClient())
                    {
                        client.Encoding = Encoding.UTF8;
                        client.Headers[HttpRequestHeader.ContentType] = "application/json";
                        client.UploadString(item.Key, "POST", item.Value);
                    }
                }
                catch (WebException ex)
                {
                    HttpWebResponse resp = ex.Response as HttpWebResponse;
                    if (resp != null && (int)resp.StatusCode == 429)
                    {
                        Thread.Sleep(2000);
                        lock (_queue) _queue.Enqueue(item);
                    }
                    else Debug.LogWarning("[Deadheim Discord] Envio falhou: " + ex.Message);
                }
                catch (Exception ex) { Debug.LogWarning("[Deadheim Discord] Envio falhou: " + ex.Message); }
                Thread.Sleep(600);
            }
        }

        // ---------------------------------------------------------------- gritos

        /// <summary>
        /// O servidor dedicado nao tem Chat: o grito chega como RPC roteado "ChatMessage"
        /// (Vector3 pos, int tipo, UserInfo{3 strings}, string texto). Le por fora, sem registrar handler.
        /// </summary>
        [HarmonyPatch(typeof(ZRoutedRpc), "HandleRoutedRPC")]
        private static class ShoutPatch
        {
            private static readonly int Hash = "ChatMessage".GetStableHashCode();

            private static void Prefix(ZRoutedRpc.RoutedRPCData data)
            {
                try
                {
                    if (!Server || data == null || data.m_methodHash != Hash || _shouts == null || !_shouts.Value) return;
                    ZPackage pkg = new ZPackage(data.m_parameters.GetArray());
                    pkg.ReadVector3();
                    int type = pkg.ReadInt();
                    if (type != 2) return; // Talker.Type.Shout
                    List<string> strings = new List<string>();
                    while (pkg.GetPos() < pkg.Size()) strings.Add(pkg.ReadString());
                    if (strings.Count < 2) return;
                    string text = strings[strings.Count - 1];
                    string name = strings[0];
                    ZNetPeer peer = ZNet.instance.GetPeer(data.m_senderPeerID);
                    if (peer != null && !string.IsNullOrEmpty(peer.m_playerName)) name = peer.m_playerName;
                    Shout(name, text);
                }
                catch (Exception ex) { Debug.LogWarning("[Deadheim Discord] Grito nao lido: " + ex.Message); }
            }
        }

        // ------------------------------------------------------------ conquistas

        /// <summary>
        /// O RaidSystem (dll a parte) avisa a conquista com o dWebHook.SendMessage, de webhook fixo no codigo.
        /// Se houver webhook configurado aqui, a mensagem sai especial por ele e a antiga e suprimida.
        /// </summary>
        public static void PatchRaidSystem(Harmony harmony)
        {
            try
            {
                Type type = AccessTools.TypeByName("dWebHook");
                var method = type == null ? null : AccessTools.Method(type, "SendMessage", new[] { typeof(string) });
                if (method == null) return;
                harmony.Patch(method, prefix: new HarmonyMethod(typeof(DiscordLog), nameof(ConquestPrefix)));
            }
            catch (Exception ex) { Debug.LogWarning("[Deadheim Discord] Nao liguei a conquista do RaidSystem: " + ex.Message); }
        }

        private static bool ConquestPrefix(string msgSend)
        {
            if (!Server || _conquests == null || !_conquests.Value) return true;
            if (string.IsNullOrWhiteSpace(_conquestWebhook?.Value) && string.IsNullOrWhiteSpace(_webhook?.Value)) return true;
            Conquest((msgSend ?? string.Empty).Trim('*', ' '));
            return false;
        }
    }
}
