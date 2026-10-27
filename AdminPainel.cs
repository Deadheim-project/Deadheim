using BepInEx.Configuration;
using Deadheim.Vanilla;
using HarmonyLib;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.RegularExpressions;
using UnityEngine;

namespace Deadheim
{
    /// <summary>
    /// Ferramentas de admin (21/10/2026):
    /// - aviso a todos quando um admin (adminlist.txt) entra no mundo;
    /// - mensagem global do admin, em faixa grande e colorida no meio da tela, com som e no chat;
    /// - ir ate um jogador, puxar um jogador, buff e cura em area.
    /// O painel (F8) e do NpcValheim, no mesmo menu dos NPCs (UI/AdminPanel.cs de la): ele so
    /// manda os pedidos para DH_Admin_ToServer e mostra as respostas de DH_Admin_Status. Aqui
    /// fica o servidor e a faixa do aviso. Tudo passa pelo servidor, que confere o remetente de verdade (RemetenteRpc) contra a
    /// adminlist antes de agir. O cliente so aceita o que vem do servidor.
    /// </summary>
    internal static class AdminPainel
    {
        private const string ToServer = "DH_Admin_ToServer";
        private const string ToClient = "DH_Admin_ToClient";
        // Resposta ao admin, na linha de status do painel do NpcValheim (quem registra e ele).
        private const string Status = "DH_Admin_Status";
        internal const int TextoTamanhoMaximo = 200;

        public static ConfigEntry<bool> AvisoLogin;
        public static ConfigEntry<string> AvisoLoginTexto;
        public static ConfigEntry<string> AvisoLoginCor;
        public static ConfigEntry<float> BuffRaioMaximo;
        public static ConfigEntry<float> BuffDuracaoPadrao;

        public static void Bind(ConfigFile config)
        {
            const string secao = "Admin";
            AvisoLogin = Plugin.Synced(config.Bind(secao, "AvisoLoginAdmin", true,
                "Avisa a todos, com a faixa grande no meio da tela, quando um admin (adminlist.txt) entra no mundo."));
            AvisoLoginTexto = Plugin.Synced(config.Bind(secao, "AvisoLoginTexto", "O admin {nome} entrou no servidor!",
                "Texto do aviso de entrada de admin. {nome} vira o nome do personagem."));
            AvisoLoginCor = Plugin.Synced(config.Bind(secao, "AvisoLoginCor", "#FFD700",
                "Cor (hex #RRGGBB) do aviso de entrada de admin."));
            BuffRaioMaximo = Plugin.Synced(config.Bind(secao, "BuffRaioMaximo", 100f,
                new ConfigDescription("Raio maximo, em metros, do buff e da cura em area do painel de admin.", new AcceptableValueRange<float>(5f, 1000f))));
            BuffDuracaoPadrao = Plugin.Synced(config.Bind(secao, "BuffDuracaoPadrao", 600f,
                new ConfigDescription("Duracao inicial (s) do buff no painel. 0 = a duracao propria do efeito.", new AcceptableValueRange<float>(0f, 7200f))));
        }

        // ------------------------------------------------------------------------------- rede

        [HarmonyPatch(typeof(Game), "Start")]
        private static class RegisterPatch
        {
            private static void Postfix()
            {
                if (ZRoutedRpc.instance == null) return;
                ZRoutedRpc.instance.Register<ZPackage>(ToServer, OnToServer);
                ZRoutedRpc.instance.Register<ZPackage>(ToClient, OnToClient);
                AdminAvisoHud.Ensure();
            }
        }

        private static void Responder(long peer, string texto)
        {
            ZRoutedRpc.instance?.InvokeRoutedRPC(peer, Status, texto);
        }

        private static ZPackage Pacote(string op)
        {
            ZPackage pkg = new ZPackage();
            pkg.Write(op);
            return pkg;
        }

        // ------------------------------------------------------------------- servidor

        private static readonly HashSet<long> _anunciados = new HashSet<long>();

        // Um pedido a cada 0,4 s por peer, admin ou nao: um cliente modificado mandando o RPC em
        // laco nao faz o servidor responder, varrer peers nem mandar pacote a cada um.
        private const float IntervaloPedidos = 0.4f;
        private static readonly Dictionary<long, float> _ultimoPedido = new Dictionary<long, float>();

        private static bool DentroDoLimite(long peer)
        {
            float agora = Time.realtimeSinceStartup;
            if (_ultimoPedido.TryGetValue(peer, out float antes) && agora - antes < IntervaloPedidos) return false;
            _ultimoPedido[peer] = agora;
            return true;
        }

        /// <summary>Posicao que um cliente mandou: finita e dentro do mundo (raio 10500 m do Valheim).</summary>
        private static bool PosicaoValida(Vector3 v) =>
            !float.IsNaN(v.x) && !float.IsNaN(v.y) && !float.IsNaN(v.z) &&
            !float.IsInfinity(v.x) && !float.IsInfinity(v.y) && !float.IsInfinity(v.z) &&
            Mathf.Abs(v.x) <= 11000f && Mathf.Abs(v.z) <= 11000f && v.y > -1000f && v.y < 6000f;

        /// <summary>
        /// Efeitos que o painel nao aplica: os que o jogo cria para outra coisa (nascer, subir
        /// vida maxima, arpao preso a algo) e quebram fora dela.
        /// </summary>
        internal static bool EfeitoProibido(StatusEffect se) =>
            se == null || se is SE_Spawn || se is SE_HealthUpgrade || se is SE_Harpooned;

        /// <summary>
        /// O CharacterID chega quando o personagem do cliente nasce no mundo (e de novo a cada
        /// renascimento): o aviso sai uma vez por conexao.
        /// </summary>
        [HarmonyPatch(typeof(ZNet), "RPC_CharacterID")]
        private static class AvisoEntradaPatch
        {
            private static void Postfix(ZNet __instance, ZRpc rpc)
            {
                if (!__instance.IsServer() || AvisoLogin == null || !AvisoLogin.Value) return;
                // ZNet.GetPeer(ZRpc) e privado no jogo: procura o peer dono da conexao a mao.
                ZNetPeer peer = __instance.GetPeers().FirstOrDefault(p => p != null && p.m_rpc == rpc);
                if (peer?.m_socket == null || peer.m_characterID.IsNone()) return;
                if (!__instance.IsAdmin(peer.m_socket.GetHostName())) return;
                if (!_anunciados.Add(peer.m_uid)) return;

                string texto = (AvisoLoginTexto.Value ?? "").Replace("{nome}", peer.m_playerName);
                if (string.IsNullOrWhiteSpace(texto)) return;
                BroadcastAviso("ADMIN ONLINE", texto, CorValida(AvisoLoginCor.Value, "#FFD700"), 8f, true, true);
                Debug.Log($"[Deadheim] Admin: {peer.m_playerName} entrou; aviso enviado.");
            }
        }

        [HarmonyPatch(typeof(ZNet), nameof(ZNet.Disconnect))]
        private static class SaidaPatch
        {
            private static void Prefix(ZNetPeer peer)
            {
                if (peer == null) return;
                _anunciados.Remove(peer.m_uid);
                _ultimoPedido.Remove(peer.m_uid);
            }
        }

        private static readonly Regex Hex = new Regex("^#[0-9a-fA-F]{6}$");

        internal static string CorValida(string cor, string padrao)
        {
            cor = (cor ?? "").Trim();
            if (cor.Length == 6) cor = "#" + cor;
            return Hex.IsMatch(cor) ? cor.ToUpperInvariant() : padrao;
        }

        private static void BroadcastAviso(string titulo, string texto, string cor, float duracao, bool piscar, bool som,
            long para = 0L)
        {
            if (ZRoutedRpc.instance == null) return;
            ZPackage pkg = Pacote("aviso");
            pkg.Write(titulo);
            pkg.Write(texto);
            pkg.Write(cor);
            pkg.Write(duracao);
            pkg.Write(piscar);
            pkg.Write(som);
            ZRoutedRpc.instance.InvokeRoutedRPC(para != 0L ? para : ZRoutedRpc.Everybody, ToClient, pkg);
        }

        /// <summary>Posicao do personagem pela ZDO (o servidor tem todas); sem ZDO, a refPos do peer.</summary>
        private static bool PosicaoDe(ZNetPeer peer, out Vector3 pos)
        {
            pos = Vector3.zero;
            if (peer == null || peer.m_characterID.IsNone()) return false;
            ZDO zdo = ZDOMan.instance?.GetZDO(peer.m_characterID);
            pos = zdo != null ? zdo.GetPosition() : peer.m_refPos;
            return true;
        }

        private static void OnToServer(long sender, ZPackage pkg)
        {
            ZNet net = ZNet.instance;
            if (net == null || !net.IsServer()) return;
            long real = RemetenteRpc.Real(sender, out _);
            if (real == 0L || !DentroDoLimite(real)) return;
            if (!Pvp.PvpServer.IsAdminPeer(real)) { Responder(real, "So admin."); return; }

            ZNetPeer admin = net.GetPeer(real);
            string adminNome = admin != null ? admin.m_playerName : (Player.m_localPlayer?.GetPlayerName() ?? "Admin");

            try
            {
                string op = pkg.ReadString();
                switch (op)
                {
                    case "msg":
                    case "previa":
                    {
                        string texto = (pkg.ReadString() ?? "").Trim();
                        string cor = CorValida(pkg.ReadString(), "#FF4040");
                        float duracao = Mathf.Clamp(pkg.ReadSingle(), 3f, 30f);
                        bool piscar = pkg.ReadBool();
                        bool som = pkg.ReadBool();
                        if (texto.Length == 0) return;
                        if (texto.Length > TextoTamanhoMaximo) texto = texto.Substring(0, TextoTamanhoMaximo);
                        if (op == "previa")
                        {
                            BroadcastAviso("PREVIA - ADMIN " + adminNome, texto, cor, duracao, piscar, som, real);
                            Responder(real, "Previa mostrada so para voce.");
                            return;
                        }
                        BroadcastAviso("ADMIN " + adminNome, texto, cor, duracao, piscar, som);
                        Responder(real, "Mensagem enviada a todos.");
                        Debug.Log($"[Deadheim] Admin: {adminNome} mandou mensagem global: {texto}");
                        return;
                    }
                    case "ir":
                    {
                        ZNetPeer alvo = net.GetPeer(pkg.ReadLong());
                        if (!PosicaoDe(alvo, out Vector3 pos)) { Responder(real, "Jogador nao encontrado ou ainda carregando."); return; }
                        ZPackage resp = Pacote("irpara");
                        resp.Write(pos);
                        resp.Write(alvo.m_playerName);
                        ZRoutedRpc.instance.InvokeRoutedRPC(real, ToClient, resp);
                        Responder(real, $"Indo ate {alvo.m_playerName}.");
                        Debug.Log($"[Deadheim] Admin: {adminNome} foi ate {alvo.m_playerName} ({pos.x:F0},{pos.z:F0}).");
                        return;
                    }
                    case "puxar":
                    {
                        ZNetPeer alvo = net.GetPeer(pkg.ReadLong());
                        Vector3 destino = pkg.ReadVector3();
                        if (alvo == null || alvo.m_characterID.IsNone()) { Responder(real, "Jogador nao encontrado ou ainda carregando."); return; }
                        if (!PosicaoValida(destino)) return;
                        // Mandado pelo servidor: o PvpPatches (AdminTeleportPatch) deixa passar mesmo cacado ou em combate.
                        ZRoutedRpc.instance.InvokeRoutedRPC(alvo.m_uid, alvo.m_characterID, "RPC_TeleportTo",
                            destino, Quaternion.identity, true);
                        Pvp.PvpNet.Message(alvo.m_uid, $"Voce foi puxado pelo admin {adminNome}.");
                        Responder(real, $"{alvo.m_playerName} esta vindo ate voce.");
                        Debug.Log($"[Deadheim] Admin: {adminNome} puxou {alvo.m_playerName}.");
                        return;
                    }
                    case "buff":
                    case "curar":
                    {
                        string efeito = op == "buff" ? (pkg.ReadString() ?? "").Trim() : "";
                        float duracao = op == "buff" ? Mathf.Clamp(pkg.ReadSingle(), 0f, 7200f) : 0f;
                        float raio = Mathf.Clamp(pkg.ReadSingle(), 1f, BuffRaioMaximo.Value);
                        Vector3 centro = pkg.ReadVector3();
                        bool incluirAdmin = pkg.ReadBool();

                        if (!PosicaoValida(centro)) return;
                        if (op == "buff" && (ObjectDB.instance == null || EfeitoProibido(ObjectDB.instance.GetStatusEffect(efeito.GetStableHashCode()))))
                        {
                            Responder(real, $"Efeito '{efeito}' nao existe ou nao pode ser aplicado.");
                            return;
                        }

                        int n = 0;
                        foreach (ZNetPeer peer in net.GetConnectedPeers())
                        {
                            if (peer.m_uid == real && !incluirAdmin) continue;
                            if (!PosicaoDe(peer, out Vector3 pos)) continue;
                            if (Vector3.Distance(pos, centro) > raio) continue;
                            ZPackage p = Pacote(op);
                            p.Write(efeito);
                            p.Write(duracao);
                            p.Write(adminNome);
                            ZRoutedRpc.instance.InvokeRoutedRPC(peer.m_uid, ToClient, p);
                            n++;
                        }
                        string oQue = op == "buff" ? $"'{efeito}'" : "cura";
                        Responder(real, $"{oQue} aplicado em {n} jogador(es) num raio de {raio:F0} m.");
                        Debug.Log($"[Deadheim] Admin: {adminNome} aplicou {oQue} em {n} jogador(es), raio {raio:F0} m.");
                        return;
                    }
                }
            }
            catch (Exception ex)
            {
                Debug.LogError("[Deadheim] Admin: pacote de " + real + " falhou: " + ex);
            }
        }

        // ------------------------------------------------------------------- cliente

        private static void OnToClient(long sender, ZPackage pkg)
        {
            if (ZRoutedRpc.instance == null || sender != ZRoutedRpc.instance.GetServerPeerID()) return;
            if (ZNet.instance != null && ZNet.instance.IsDedicated()) return;
            try
            {
                string op = pkg.ReadString();
                Player player = Player.m_localPlayer;
                switch (op)
                {
                    case "aviso":
                    {
                        string titulo = pkg.ReadString();
                        string texto = pkg.ReadString();
                        string cor = CorValida(pkg.ReadString(), "#FFFFFF");
                        float duracao = pkg.ReadSingle();
                        bool piscar = pkg.ReadBool();
                        bool som = pkg.ReadBool();
                        AdminAvisoHud.Mostrar(titulo, texto, cor, duracao, piscar, som);
                        Chat.instance?.AddString($"<color={cor}><b>[{titulo}]</b> {texto}</color>");
                        return;
                    }
                    case "irpara":
                    {
                        Vector3 pos = pkg.ReadVector3();
                        string nome = pkg.ReadString();
                        if (player == null || player.IsDead() || player.IsTeleporting() || !PosicaoValida(pos)) return;
                        player.TeleportTo(pos + new Vector3(1.5f, 0.5f, 1.5f), player.transform.rotation, true);
                        player.Message(MessageHud.MessageType.Center, "Indo ate " + nome + "...");
                        return;
                    }
                    case "buff":
                    {
                        string efeito = pkg.ReadString();
                        float duracao = pkg.ReadSingle();
                        string adminNome = pkg.ReadString();
                        if (player == null || player.IsDead() || ObjectDB.instance == null) return;
                        if (EfeitoProibido(ObjectDB.instance.GetStatusEffect(efeito.GetStableHashCode()))) return;
                        StatusEffect se = player.GetSEMan().AddStatusEffect(efeito.GetStableHashCode(), true);
                        if (se == null) return;
                        // "Duracao do efeito" num efeito sem duracao propria (os de equipamento) ficaria
                        // para sempre: 10 minutos nesse caso.
                        if (duracao > 0f) se.m_ttl = duracao;
                        else if (se.m_ttl <= 0f) se.m_ttl = 600f;
                        string nome = Localization.instance != null ? Localization.instance.Localize(se.m_name) : efeito;
                        player.Message(MessageHud.MessageType.TopLeft, $"O admin {adminNome} te deu: {nome}");
                        return;
                    }
                    case "curar":
                    {
                        pkg.ReadString();
                        pkg.ReadSingle();
                        string adminNome = pkg.ReadString();
                        if (player == null || player.IsDead()) return;
                        player.Heal(player.GetMaxHealth(), true);
                        player.AddStamina(player.GetMaxStamina());
                        player.AddEitr(player.GetMaxEitr());
                        player.Message(MessageHud.MessageType.TopLeft, $"O admin {adminNome} curou voce.");
                        return;
                    }
                }
            }
            catch (Exception ex)
            {
                Debug.LogError("[Deadheim] Admin: pacote do servidor falhou: " + ex);
            }
        }
    }

    /// <summary>
    /// Faixa grande dos avisos de admin, que todos veem: borda na cor do aviso, texto grande
    /// com contorno, piscando se pedido, e o som de bioma descoberto. IMGUI, por cima de tudo.
    /// </summary>
    public class AdminAvisoHud : MonoBehaviour
    {
        private static AdminAvisoHud _instance;

        private sealed class Aviso
        {
            public string Titulo, Texto, TextoSemTags;
            public Color Cor;
            public float Inicio, Duracao;
            public bool Piscar;
        }

        private readonly List<Aviso> _avisos = new List<Aviso>();
        private GUIStyle _estiloTitulo, _estiloTexto;
        private int _alturaDosEstilos;

        internal static void Ensure()
        {
            if (_instance != null) return;
            if (ZNet.instance != null && ZNet.instance.IsDedicated()) return;
            GameObject go = new GameObject("DeadheimAdminAvisoHud");
            DontDestroyOnLoad(go);
            _instance = go.AddComponent<AdminAvisoHud>();
        }

        internal static void Mostrar(string titulo, string texto, string cor, float duracao, bool piscar, bool som)
        {
            Ensure();
            if (_instance == null) return;
            if (!ColorUtility.TryParseHtmlString(cor, out Color c)) c = Color.white;
            List<Aviso> avisos = _instance._avisos;
            if (avisos.Count >= 3) avisos.RemoveAt(0);
            avisos.Add(new Aviso
            {
                Titulo = titulo,
                Texto = texto ?? "",
                TextoSemTags = Regex.Replace(texto ?? "", "<.*?>", ""),
                Cor = c,
                Inicio = Time.time,
                Duracao = Mathf.Max(1f, duracao),
                Piscar = piscar
            });
            if (som && MessageHud.instance != null && MessageHud.instance.m_biomeFoundStinger != null)
                Instantiate(MessageHud.instance.m_biomeFoundStinger);
        }

        private void Update()
        {
            _avisos.RemoveAll(a => Time.time - a.Inicio > a.Duracao);
        }

        private void OnGUI()
        {
            if (_avisos.Count == 0) return;
            // Acima das janelas de UI (o menu dos NPCs inclusive), para o admin ver a previa.
            GUI.depth = -1000;

            float escala = Mathf.Max(0.6f, Screen.height / 1080f);
            float largura = Mathf.Min(Screen.width - 40f, 1100f * escala);
            float altura = 120f * escala;
            float y = Screen.height * 0.18f;

            // Estilos feitos uma vez por resolucao, e nao a cada OnGUI (varias vezes por quadro).
            if (_estiloTexto == null || _alturaDosEstilos != Screen.height)
            {
                _alturaDosEstilos = Screen.height;
                Font fonte = Ui.AveriaSerifBold;
                _estiloTitulo = new GUIStyle(GUI.skin.label)
                {
                    alignment = TextAnchor.MiddleCenter, fontSize = Mathf.RoundToInt(20f * escala),
                    fontStyle = FontStyle.Bold, richText = false
                };
                _estiloTexto = new GUIStyle(GUI.skin.label)
                {
                    alignment = TextAnchor.MiddleCenter, fontSize = Mathf.RoundToInt(38f * escala),
                    fontStyle = FontStyle.Bold, wordWrap = true, richText = true
                };
                if (fonte != null) { _estiloTitulo.font = fonte; _estiloTexto.font = fonte; }
            }
            GUIStyle estiloTitulo = _estiloTitulo, estiloTexto = _estiloTexto;

            Color antes = GUI.color;
            foreach (Aviso a in _avisos)
            {
                float t = Time.time - a.Inicio;
                float alfa = Mathf.Clamp01(t / 0.3f) * Mathf.Clamp01((a.Duracao - t) / 0.8f);
                float pulso = a.Piscar ? 0.6f + 0.4f * Mathf.Abs(Mathf.Sin(t * 4f)) : 1f;
                Rect caixa = new Rect((Screen.width - largura) / 2f, y, largura, altura);

                GUI.color = new Color(0f, 0f, 0f, 0.6f * alfa);
                GUI.DrawTexture(caixa, Texture2D.whiteTexture);
                GUI.color = new Color(a.Cor.r, a.Cor.g, a.Cor.b, alfa * pulso);
                float borda = Mathf.Max(2f, 4f * escala);
                GUI.DrawTexture(new Rect(caixa.x, caixa.y, caixa.width, borda), Texture2D.whiteTexture);
                GUI.DrawTexture(new Rect(caixa.x, caixa.yMax - borda, caixa.width, borda), Texture2D.whiteTexture);

                Rect areaTitulo = new Rect(caixa.x, caixa.y + 6f * escala, caixa.width, 26f * escala);
                Rect areaTexto = new Rect(caixa.x + 16f, caixa.y + 30f * escala, caixa.width - 32f, caixa.height - 36f * escala);

                Contorno(areaTitulo, a.Titulo, estiloTitulo, alfa);
                GUI.color = new Color(1f, 1f, 1f, alfa);
                estiloTitulo.normal.textColor = new Color(1f, 0.85f, 0.5f);
                GUI.Label(areaTitulo, a.Titulo, estiloTitulo);

                Contorno(areaTexto, a.TextoSemTags, estiloTexto, alfa);
                GUI.color = new Color(1f, 1f, 1f, alfa * pulso);
                estiloTexto.normal.textColor = a.Cor;
                GUI.Label(areaTexto, a.Texto, estiloTexto);

                y += altura + 8f;
            }
            GUI.color = antes;
        }

        private static void Contorno(Rect area, string texto, GUIStyle estilo, float alfa)
        {
            Color cor = estilo.normal.textColor;
            bool rich = estilo.richText;
            estilo.normal.textColor = Color.black;
            estilo.richText = false;
            GUI.color = new Color(1f, 1f, 1f, alfa);
            for (int dx = -2; dx <= 2; dx += 2)
                for (int dy = -2; dy <= 2; dy += 2)
                    if (dx != 0 || dy != 0) GUI.Label(new Rect(area.x + dx, area.y + dy, area.width, area.height), texto, estilo);
            estilo.normal.textColor = cor;
            estilo.richText = rich;
        }
    }
}
