using BepInEx.Configuration;
using Deadheim.Shared;
using Deadheim.Vanilla;
using Deadheim.Wards;
using HarmonyLib;
using System;
using System.Collections.Generic;
using UnityEngine;

namespace Deadheim
{
    /// <summary>
    /// Totem da Cidade (26/10/2026): peca posta dentro do proprio ward que leva, so de ida, ao ponto de
    /// chegada da cidade. Volta-se pela pedra de retorno, pelo /retreat ou pelo Teleportador do NpcValheim
    /// ("Voltar para casa"), que leva cada jogador so ao totem que ele mesmo construiu: nao existe rota de
    /// ninguem para a base de ninguem.
    /// - Colocar: so dentro de ward do qual o jogador e dono, com limite por jogador contado pelo servidor
    ///   (5o valor do DeadheimPortalAndTotemCount).
    /// - Usar: quem tem acesso ao ward ligado que cobre o totem (dono, guilda, adicionados), com as travas
    ///   da pedra (combate, cacado, minerio). Sem recarga.
    /// - Chegada: [Totem da Cidade] Destino = "x;y;z", que o admin grava no Teleportador ("Chegada do Totem
    ///   aqui") ou parado no lugar com totem_destino.
    /// </summary>
    public static class TotemCidade
    {
        public const string PrefabName = "DH_TotemCidade";
        // O Portal de Pedra do jogo, sem a parte de portal: o visual de arco ja diz "isto leva a outro lugar".
        private const string VanillaPrefab = "portal_stone";
        internal const string Secao = "Totem da Cidade";
        private const string Nome = "Totem da Cidade";

        public static ConfigEntry<bool> Ligado;
        public static ConfigEntry<string> Custo;
        public static ConfigEntry<string> Destino;
        public static ConfigEntry<int> Limite;
        public static ConfigEntry<int> LimiteVip;

        /// <summary>Totens do jogador local, contados pelo servidor. 999 = ainda sem resposta.</summary>
        public static int Contagem = 999;

        private static GameObject _prefab;
        private static bool _noMartelo;
        private static bool _esperandoDestino;

        // PrivateArea.m_allAreas e privado no jogo e campo privado lido direto ja deu FieldAccessException em
        // runtime: lido uma vez por reflexao (a lista e sempre a mesma).
        private static List<PrivateArea> _todasAreas;

        // ZDOMan.m_objectsBySector tambem e privado no jogo.
        private static readonly AccessTools.FieldRef<ZDOMan, List<ZDO>[]> ObjetosPorSetor =
            AccessTools.FieldRefAccess<ZDOMan, List<ZDO>[]>("m_objectsBySector");

        private static List<PrivateArea> TodasAreas
        {
            get
            {
                if (_todasAreas == null)
                    _todasAreas = AccessTools.Field(typeof(PrivateArea), "m_allAreas")?.GetValue(null) as List<PrivateArea>
                                  ?? new List<PrivateArea>();
                return _todasAreas;
            }
        }

        public static void Bind(ConfigFile config)
        {
            Ligado = Plugin.Synced(config.Bind(Secao, "Ligado", true,
                "Totem da Cidade: peca posta dentro do proprio ward que leva, so de ida, a chegada da cidade. Desligado, nao coloca nem viaja (os que existem continuam no mundo)."));
            Custo = Plugin.Synced(config.Bind(Secao, "Custo", "Stone:20,GreydwarfEye:10,Coins:50",
                "Materiais do totem. Formato: PrefabName:Quantidade,PrefabName:Quantidade"));
            Destino = Plugin.Synced(config.Bind(Secao, "Destino", "",
                "Ponto de chegada na cidade, \"x;y;z\". Vazio = o totem nao leva a lugar nenhum. O admin grava no Teleportador (Chegada do Totem aqui) ou com o comando totem_destino."));
            Limite = Plugin.Synced(config.Bind(Secao, "Limite", 1,
                new ConfigDescription("Totens por jogador. 0 = sem limite.", new AcceptableValueRange<int>(0, 50))));
            LimiteVip = Plugin.Synced(config.Bind(Secao, "LimiteVip", 2,
                new ConfigDescription("Totens por jogador Aesir. 0 = sem limite.", new AcceptableValueRange<int>(0, 50))));

            Custo.SettingChanged += (_, __) => AplicarCusto();
            Destino.SettingChanged += (_, __) => AvisarDestino();

            Prefabs.ZNetSceneReady += CriarPrefab;
            Prefabs.PiecesReady += RegistrarNoMartelo;
        }

        // ------------------------------------------------------------------ prefab

        /// <summary>
        /// Sempre criado, mesmo com Ligado desligado: os totens que ja estao no mundo precisam do prefab
        /// para carregar (sem ele o cliente spama "Missing prefab hash").
        /// </summary>
        private static void CriarPrefab()
        {
            Prefabs.ZNetSceneReady -= CriarPrefab;
            if (_prefab != null) return;

            _prefab = Prefabs.Clone(PrefabName, VanillaPrefab);
            if (_prefab == null)
            {
                Debug.LogError("[Deadheim] " + VanillaPrefab + " nao encontrado para clonar o " + PrefabName + ".");
                return;
            }

            // Sem a parte de portal: o jogo ligaria o totem a qualquer portal com o mesmo nome, e o gatilho
            // da passagem chamaria um TeleportWorld que nao existe mais.
            TotemCidadeInteracao interacao = _prefab.AddComponent<TotemCidadeInteracao>();
            foreach (TeleportWorldTrigger gatilho in _prefab.GetComponentsInChildren<TeleportWorldTrigger>(true))
                UnityEngine.Object.DestroyImmediate(gatilho);
            foreach (TeleportWorld portal in _prefab.GetComponentsInChildren<TeleportWorld>(true))
            {
                // A altura do texto sobre a peca e a do portal que ele era.
                interacao.m_hoverOffset = portal.m_hoverOffset;
                UnityEngine.Object.DestroyImmediate(portal);
            }

            Piece piece = _prefab.GetComponent<Piece>();
            if (piece != null)
            {
                piece.m_name = Nome;
                piece.m_description = "Leva voce, so de ida, a cidade. Fica dentro de um ward seu e so quem tem acesso ao ward usa. " +
                                      "Para voltar, use a pedra de retorno ou o /retreat.";
            }

            Debug.Log("[Deadheim] " + PrefabName + " criado a partir do " + VanillaPrefab + ".");
        }

        /// <summary>A cada ObjectDB novo (os itens do custo so existem a partir dai).</summary>
        private static void RegistrarNoMartelo()
        {
            if (_prefab == null) return;

            AplicarCusto();
            Piece piece = _prefab.GetComponent<Piece>();
            GameObject bancada = Prefabs.Get("piece_workbench");
            if (piece != null && bancada != null) piece.m_craftingStation = bancada.GetComponent<CraftingStation>();

            if (_noMartelo) return;
            Pieces.AddToHammer(_prefab, "Misc");
            _noMartelo = true;
            Debug.Log("[Deadheim] " + PrefabName + " adicionado ao martelo.");
        }

        private static void AplicarCusto()
        {
            Piece piece = _prefab != null ? _prefab.GetComponent<Piece>() : null;
            if (piece == null || Custo == null) return;

            List<Piece.Requirement> requisitos = new List<Piece.Requirement>();
            foreach (string entrada in (Custo.Value ?? "").Split(','))
            {
                string[] partes = entrada.Split(':');
                if (partes.Length != 2) continue;
                if (!int.TryParse(partes[1].Trim(), out int quantidade) || quantidade <= 0) continue;

                string nomeItem = partes[0].Trim();
                GameObject prefab = Prefabs.Get(nomeItem);
                ItemDrop item = prefab != null ? prefab.GetComponent<ItemDrop>() : null;
                if (item == null)
                {
                    Debug.LogWarning("[Deadheim] Totem da Cidade: item de custo nao encontrado: " + nomeItem);
                    continue;
                }
                requisitos.Add(new Piece.Requirement { m_resItem = item, m_amount = quantidade, m_recover = true });
            }

            if (requisitos.Count > 0) piece.m_resources = requisitos.ToArray();
        }

        private static bool EhTotem(GameObject go) => go != null && WardProfiles.CleanName(go) == PrefabName;

        // ------------------------------------------------------------------ viagem

        public static bool TryDestino(out Vector3 destino)
        {
            destino = Vector3.zero;
            string[] partes = (Destino.Value ?? "").Split(';');
            if (partes.Length != 3) return false;
            if (!Numeros.TryLer(partes[0], out float x) || !Numeros.TryLer(partes[1], out float y) || !Numeros.TryLer(partes[2], out float z))
                return false;
            destino = new Vector3(x, y, z);
            return true;
        }

        /// <summary>"x;y;z" com ponto decimal e uma casa, o formato que TryDestino le.</summary>
        internal static string FormatarPonto(Vector3 p) =>
            Numeros.Escrever(Mathf.Round(p.x * 10f) / 10f) + ";"
            + Numeros.Escrever(Mathf.Round(p.y * 10f) / 10f) + ";"
            + Numeros.Escrever(Mathf.Round(p.z * 10f) / 10f);

        /// <summary>
        /// Servidor: o ponto de chegada na frente de cada totem construido por este jogador, na ordem das
        /// ZDOs (estavel entre pedidos, entao "Casa 2" e sempre o mesmo totem).
        /// </summary>
        internal static List<Vector3> CasasDe(long playerId)
        {
            List<ZDO> totens = new List<ZDO>();
            List<ZDO>[] setores = ZDOMan.instance != null ? ObjetosPorSetor(ZDOMan.instance) : null;
            if (playerId == 0L || setores == null) return new List<Vector3>();

            int hash = PrefabName.GetStableHashCode();
            foreach (List<ZDO> setor in setores)
            {
                if (setor == null) continue;
                for (int i = 0; i < setor.Count; i++)
                {
                    ZDO zdo = setor[i];
                    if (zdo != null && zdo.GetPrefab() == hash && zdo.GetLong(ZDOVars.s_creator, 0L) == playerId)
                        totens.Add(zdo);
                }
            }

            totens.Sort((a, b) => a.m_uid.UserID != b.m_uid.UserID
                ? a.m_uid.UserID.CompareTo(b.m_uid.UserID)
                : a.m_uid.ID.CompareTo(b.m_uid.ID));
            // Na frente do arco, nao dentro dele.
            return totens.ConvertAll(zdo => zdo.GetPosition() + zdo.GetRotation() * Vector3.forward * 2.5f);
        }

        /// <summary>
        /// Motivo para negar o uso, ou null. Vale o ward que cobre o totem, esta ligado e da acesso ao
        /// jogador (dono, guilda ou adicionado).
        /// </summary>
        public static string RecusaDeAcesso(Vector3 pos, Player player)
        {
            if (player == null) return Nome + " indisponivel.";
            if (Admin.LocalPlayerIsAdmin()) return null;

            bool coberto = false, temAcesso = false;
            foreach (PrivateArea area in TodasAreas)
            {
                if (area == null || WardProfiles.For(area) == null || !area.IsInside(pos, 0f)) continue;
                coberto = true;
                if (!WardCore.IsPermittedIn(area, player)) continue;
                temAcesso = true;
                if (area.IsEnabled()) return null;
            }

            if (!coberto) return "Este totem esta fora de ward e nao funciona.";
            return temAcesso ? "O ward desta base esta desligado ou sem combustivel."
                             : "So quem tem acesso ao ward desta base usa o totem.";
        }

        internal static void Viajar(Vector3 posTotem, Player player)
        {
            if (player == null) return;

            string recusa = Ligado.Value ? RecusaDeAcesso(posTotem, player) : Nome + " esta desligado no servidor.";
            Vector3 destino = Vector3.zero;
            if (recusa == null && !TryDestino(out destino))
                recusa = "O admin ainda nao definiu a chegada da cidade.";
            // As mesmas travas da pedra e do /retreat: o totem nao pode ser a saida de uma luta.
            if (recusa == null)
                recusa = Pvp.PvpModule.TeleportRefusal(Pvp.PvpConfig.RetreatBlockedByPveCombat.Value);
            if (recusa == null && !player.IsTeleportable(false))
                recusa = "Carregando minerio ou metal voce nao viaja pelo totem.";

            if (recusa != null)
            {
                player.Message(MessageHud.MessageType.Center, recusa);
                return;
            }

            // Um passo para o lado em cada chegada: quem viaja junto nao aparece um dentro do outro.
            Vector2 lado = UnityEngine.Random.insideUnitCircle * 2f;
            if (!player.TeleportTo(destino + new Vector3(lado.x, 0f, lado.y), player.transform.rotation, true))
            {
                player.Message(MessageHud.MessageType.Center, "O teleporte nao saiu agora; tente de novo em instantes.");
                return;
            }

            player.Message(MessageHud.MessageType.Center, "Indo para a cidade...");
        }

        // ------------------------------------------------------------- colocar e remover

        /// <summary>
        /// So dentro de um ward do qual o jogador e dono, e dentro do limite contado pelo servidor.
        /// Admin coloca em qualquer lugar.
        /// </summary>
        [HarmonyPatch(typeof(Player), nameof(Player.PlacePiece))]
        private static class ColocarPatch
        {
            private static bool Prefix(Player __instance, Piece piece, Vector3 pos)
            {
                if (piece == null || __instance != Player.m_localPlayer || !EhTotem(piece.gameObject)) return true;

                if (!Ligado.Value)
                {
                    __instance.Message(MessageHud.MessageType.Center, Nome + " esta desligado no servidor.");
                    return false;
                }
                if (Admin.LocalPlayerIsAdmin()) return true;

                if (!DentroDoProprioWard(pos, __instance.GetPlayerID()))
                {
                    __instance.Message(MessageHud.MessageType.Center, "O " + Nome + " so pode ficar dentro de um ward seu.");
                    return false;
                }

                int limite = VipList.VipListApi.IsLocalPlayerVip() ? LimiteVip.Value : Limite.Value;
                if (limite <= 0) return true;

                // Sem a resposta do servidor nao da para saber quantos ja existem: pergunta e pede para tentar de novo.
                if (Contagem >= 999)
                {
                    WardCore.RequestWardCount();
                    __instance.Message(MessageHud.MessageType.Center, "Conferindo seus totens com o servidor. Tente de novo.");
                    return false;
                }
                if (Contagem >= limite)
                {
                    __instance.Message(MessageHud.MessageType.Center,
                        limite == 1 ? "Voce ja tem um " + Nome + ". Remova o antigo primeiro."
                                    : "Limite de Totens da Cidade atingido (" + limite + ").");
                    return false;
                }

                // Conta ja: o servidor so reconta em ate 10 s, e uma segunda colocacao nesse meio passaria.
                Contagem++;
                return true;
            }
        }

        private static bool DentroDoProprioWard(Vector3 pos, long playerId)
        {
            foreach (PrivateArea area in TodasAreas)
            {
                if (area == null || WardProfiles.For(area) == null) continue;
                Piece piece = area.GetComponent<Piece>();
                if (piece != null && piece.GetCreator() == playerId && area.IsInside(pos, 0f)) return true;
            }
            return false;
        }

        /// <summary>So o dono desmonta: com acesso ao ward, um membro da guilda desmontaria e ficaria com o material.</summary>
        [HarmonyPatch(typeof(Player), "CheckCanRemovePiece")]
        private static class RemoverPatch
        {
            private static void Postfix(Player __instance, Piece piece, ref bool __result)
            {
                if (!__result || piece == null || !EhTotem(piece.gameObject)) return;
                if (piece.GetCreator() == __instance.GetPlayerID() || Admin.LocalPlayerIsAdmin()) return;

                __result = false;
                __instance.Message(MessageHud.MessageType.Center, "So o dono desmonta o " + Nome + ".");
            }
        }

        // ------------------------------------------------------------------ admin

        [HarmonyPatch(typeof(Terminal), nameof(Terminal.InitTerminal))]
        private static class ComandoPatch
        {
            private static void Postfix()
            {
                new Terminal.ConsoleCommand("totem_destino", "Admin: grava a sua posicao como chegada do Totem da Cidade", (Terminal.ConsoleEvent)(args =>
                {
                    Player player = Player.m_localPlayer;
                    if (player == null) return;
                    if (!Admin.LocalPlayerIsAdmin())
                    {
                        args.Context.AddString("So admin.");
                        return;
                    }

                    string valor = FormatarPonto(player.transform.position);
                    // Pelo mesmo pedido do menu de ajustes: o servidor confere a adminlist e grava a cfg dele.
                    if (!Ajustes.AjustesRede.Definir(Plugin.PluginGUID, Secao, Destino.Definition.Key, valor))
                    {
                        args.Context.AddString("Sem conexao com o servidor.");
                        return;
                    }
                    _esperandoDestino = true;
                    args.Context.AddString("Pedido enviado: chegada do " + Nome + " = " + valor);
                }));
            }
        }

        private static void AvisarDestino()
        {
            if (!_esperandoDestino) return;
            _esperandoDestino = false;
            Player.m_localPlayer?.Message(MessageHud.MessageType.Center, "Chegada do " + Nome + " gravada: " + Destino.Value);
        }
    }

    /// <summary>
    /// API publica do totem para o NpcValheim, lida por reflexao como a TransmogApi: o Teleportador grava a
    /// chegada da cidade e leva o jogador de volta ao totem que ele construiu. Versao so cresce: quem le
    /// confere a minima, nunca a igualdade.
    /// </summary>
    public static class TotemCidadeApi
    {
        public const int Version = 1;

        public static bool IsEnabled() => TotemCidade.Ligado != null && TotemCidade.Ligado.Value;

        /// <summary>Cliente: totens do jogador local segundo o servidor. 999 = ainda sem resposta.</summary>
        public static int LocalCount() => TotemCidade.Contagem;

        /// <summary>Chegada atual ("x;y;z"), ou "" sem chegada.</summary>
        public static string Arrival() => TotemCidade.TryDestino(out Vector3 p) ? TotemCidade.FormatarPonto(p) : "";

        /// <summary>
        /// Servidor: grava a chegada, que a cfg sincroniza com os clientes. Null = gravado; senao, o motivo.
        /// Quem chama ja conferiu que o pedido veio de admin.
        /// </summary>
        public static string SetArrival(float x, float y, float z)
        {
            if (ZNet.instance == null || !ZNet.instance.IsServer()) return "So o servidor grava a chegada.";
            string valor = TotemCidade.FormatarPonto(new Vector3(x, y, z));
            bool ok = Ajustes.AjustesDados.Aplicar(Plugin.PluginGUID, TotemCidade.Secao, TotemCidade.Destino.Definition.Key, valor,
                local: false, out string anterior, out _, out string erro);
            if (!ok) return string.IsNullOrEmpty(erro) ? "O Deadheim recusou a chegada." : erro;
            Debug.Log($"[Deadheim] Totem da Cidade: chegada '{anterior}' -> '{valor}' (Teleportador).");
            return null;
        }

        /// <summary>Servidor: onde este jogador chega em cada totem que construiu, "x;y;z|x;y;z" na ordem das ZDOs.</summary>
        public static string HomesOf(long playerId) =>
            string.Join("|", TotemCidade.CasasDe(playerId).ConvertAll(TotemCidade.FormatarPonto));
    }

    /// <summary>E para usar o totem; o texto mostra a tecla ou por que nao da.</summary>
    public class TotemCidadeInteracao : MonoBehaviour, Interactable, Hoverable
    {
        public float m_hoverOffset;

        public float GetHoverOffset() => m_hoverOffset;

        public bool Interact(Humanoid user, bool hold, bool alt)
        {
            if (hold || !(user is Player player) || player != Player.m_localPlayer) return false;
            TotemCidade.Viajar(transform.position, player);
            return true;
        }

        public bool UseItem(Humanoid user, ItemDrop.ItemData item) => false;

        public string GetHoverName() => "Totem da Cidade";

        public string GetHoverText()
        {
            string texto = "Totem da Cidade";
            if (!TotemCidade.Ligado.Value) return texto + "\n<color=grey>Desligado no servidor</color>";

            Player player = Player.m_localPlayer;
            string recusa = TotemCidade.RecusaDeAcesso(transform.position, player);
            if (recusa != null) return texto + "\n<color=grey>" + recusa + "</color>";

            texto += "\n[<color=yellow><b>$KEY_Use</b></color>] Viajar para a cidade (so ida)";
            return Localization.instance.Localize(texto);
        }
    }
}
