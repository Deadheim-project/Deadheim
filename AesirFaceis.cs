using BepInEx.Configuration;
using Deadheim.Shared;
using HarmonyLib;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using UnityEngine;

namespace Deadheim
{
    /// <summary>
    /// Configs dos Aesir "faceis" decididos em 14/10/2026 (vip-doacoes.md): cada um liga e desliga
    /// por conta propria. 5 e 11 (so VIP) moram aqui mas o efeito fica em MassPickUp.cs e
    /// CraftPatches.cs; 18 (retreat/segundo ponto) fica no Retreat.cs. O resto (6-9, 12, 14-17, 26)
    /// tem o patch bem aqui, perto da config. O 21 (marcadores extras) ficou pendente de definicao
    /// (14/10) e o 29 (chama colorida) depende do registro de cosmeticos (B3) mais teste no jogo:
    /// nenhum dos dois entrou nesta onda.
    /// </summary>
    internal static class AesirConfig
    {
        public static ConfigEntry<bool> Aesir5SoVip;
        public enum ModoPlantioArea { Desligado, SoAesir, Todos }
        public static ConfigEntry<ModoPlantioArea> AesirPlantioArea;
        public static ConfigEntry<int> AesirPlantioAreaLado;
        public static ConfigEntry<bool> Aesir11SoVip;
        public static ConfigEntry<bool> Aesir18DoisPontos;

        public static ConfigEntry<bool> Aesir6ReplantarAtivo;
        public static ConfigEntry<string> Aesir6ReplantarPares;

        public static ConfigEntry<bool> Aesir7AbastecerAreaAtivo;
        public static ConfigEntry<float> Aesir7AbastecerAreaRaio;

        public static ConfigEntry<bool> Aesir8SemDesgasteAtivo;
        public static ConfigEntry<string> Aesir8Ferramentas;

        public static ConfigEntry<bool> Aesir9TempoRestanteAtivo;

        public static ConfigEntry<bool> Aesir12RepararAreaAtivo;
        public static ConfigEntry<float> Aesir12RepararAreaRaio;

        public static ConfigEntry<bool> Aesir14DepositarAtivo;
        public static ConfigEntry<float> Aesir14DepositarRaio;

        public static ConfigEntry<bool> Aesir15OrdenarAtivo;
        public static ConfigEntry<bool> Aesir16EtiquetaAtivo;

        public static ConfigEntry<bool> Aesir17RestedAtivo;
        public static ConfigEntry<float> Aesir17RestedBonus;

        public static ConfigEntry<bool> Aesir26Ativo;
        public static ConfigEntry<string> Aesir26Mensagem;

        public static void Bind(ConfigFile config)
        {
            const string secao = "Aesir faceis";

            Aesir5SoVip = Plugin.Synced(config.Bind(secao, "Aesir5SoVip", true,
                "Aesir 5: colheita/extracao em massa (Shift+E num raio) so para VIP. Decisao do usuario, 14/10/2026."));
            AesirPlantioArea = Plugin.Synced(config.Bind(secao, "AesirPlantioArea", ModoPlantioArea.SoAesir,
                "Plantio em area: com o ancinho, Shift ao plantar uma muda planta uma grade dela em volta (cobra semente, vigor e desgaste de cada uma). " +
                "SoAesir = so VIP; Todos = qualquer jogador; Desligado = ninguem."));
            AesirPlantioAreaLado = Plugin.Synced(config.Bind(secao, "AesirPlantioAreaLado", 5,
                new ConfigDescription("Plantio em area: mudas por lado da grade (5 = 5x5, ate 25 de uma vez).", new AcceptableValueRange<int>(2, 9))));
            Aesir11SoVip = Plugin.Synced(config.Bind(secao, "Aesir11SoVip", true,
                "Aesir 11: fabricacao em lote (ate 99) e fabricacao rapida nas bancadas, so para VIP. Decisao do usuario, 14/10/2026."));
            Aesir18DoisPontos = Plugin.Synced(config.Bind(secao, "Aesir18DoisPontos", true,
                "Aesir 18: VIP tem um segundo ponto de retreat (Shift+P na cama, 'retreat 2' para usar)."));

            Aesir6ReplantarAtivo = Plugin.Synced(config.Bind(secao, "Aesir6ReplantarAtivo", true,
                "Aesir 6: ao colher uma plantacao de uma vez so (nao as que rebrotam sozinhas), planta sementes novas no lugar se o VIP tiver no inventario."));
            Aesir6ReplantarPares = Plugin.Synced(config.Bind(secao, "Aesir6ReplantarPares",
                "Pickable_Carrot:CarrotSeeds,Pickable_Turnip:TurnipSeeds,Pickable_Onion:OnionSeeds,Pickable_Barley:Barley,Pickable_Flax:Flax",
                new ConfigDescription("Prefab colhido:prefab da semente que planta de novo. Lista separada por virgula. Conferir no jogo: " +
                    "nomes errados nao quebram nada, so deixam de replantar aquela planta.")));

            Aesir7AbastecerAreaAtivo = Plugin.Synced(config.Bind(secao, "Aesir7AbastecerAreaAtivo", true,
                "Aesir 7: VIP com Shift ao abastecer uma fundicao/forja/forno tambem abastece as do mesmo tipo num raio."));
            Aesir7AbastecerAreaRaio = Plugin.Synced(config.Bind(secao, "Aesir7AbastecerAreaRaio", 10f,
                new ConfigDescription("Raio do abastecimento em area, em metros.", new AcceptableValueRange<float>(1f, 50f))));

            Aesir8SemDesgasteAtivo = Plugin.Synced(config.Bind(secao, "Aesir8SemDesgasteAtivo", true,
                "Aesir 8: ferramentas de fazenda do VIP (Aesir8Ferramentas) nao perdem durabilidade."));
            Aesir8Ferramentas = Plugin.Synced(config.Bind(secao, "Aesir8Ferramentas", "Hoe,Cultivator",
                "Nomes (m_shared.m_name) das ferramentas que nao desgastam para o VIP. Lista separada por virgula."));

            Aesir9TempoRestanteAtivo = Plugin.Synced(config.Bind(secao, "Aesir9TempoRestanteAtivo", true,
                "Aesir 9: VIP ve, ao mirar um recurso ja colhido, quanto falta para ele voltar."));

            Aesir12RepararAreaAtivo = Plugin.Synced(config.Bind(secao, "Aesir12RepararAreaAtivo", true,
                "Aesir 12: VIP com Shift ao reparar uma construcao (martelo) repara tambem as danificadas num raio."));
            Aesir12RepararAreaRaio = Plugin.Synced(config.Bind(secao, "Aesir12RepararAreaRaio", 20f,
                new ConfigDescription("Raio do reparo em area, em metros.", new AcceptableValueRange<float>(1f, 100f))));

            Aesir14DepositarAtivo = Plugin.Synced(config.Bind(secao, "Aesir14DepositarAtivo", true,
                "Aesir 14 (/depositar): VIP deposita do proprio inventario nos baus proximos que ja tem aquele item, completando a pilha."));
            Aesir14DepositarRaio = Plugin.Synced(config.Bind(secao, "Aesir14DepositarRaio", 10f,
                new ConfigDescription("Raio do /depositar, em metros.", new AcceptableValueRange<float>(1f, 50f))));

            Aesir15OrdenarAtivo = Plugin.Synced(config.Bind(secao, "Aesir15OrdenarAtivo", true,
                "Aesir 15 (/ordenar): VIP reorganiza por nome o bau que estiver com a janela aberta."));
            Aesir16EtiquetaAtivo = Plugin.Synced(config.Bind(secao, "Aesir16EtiquetaAtivo", true,
                "Aesir 16 (/etiqueta <nome>): VIP renomeia o bau que esta olhando; o nome fica gravado no bau, todo mundo ve."));

            Aesir17RestedAtivo = Plugin.Synced(config.Bind(secao, "Aesir17RestedAtivo", true,
                "Aesir 17: a duracao do efeito Descansado do VIP e maior (Aesir17RestedBonus)."));
            Aesir17RestedBonus = Plugin.Synced(config.Bind(secao, "Aesir17RestedBonus", 0.5f,
                new ConfigDescription("Quanto a duracao do Descansado aumenta para o VIP. 0.5 = +50%.", new AcceptableValueRange<float>(0f, 5f))));

            Aesir26Ativo = Plugin.Synced(config.Bind(secao, "Aesir26Ativo", true,
                "Aesir 26: mensagem especial para o VIP ao entrar no mundo (Aesir26Mensagem; {nome} = nome do personagem)."));
            Aesir26Mensagem = Plugin.Synced(config.Bind(secao, "Aesir26Mensagem", "Bem-vindo de volta, {nome}. Que os Aesir te protejam.",
                "Mensagem mostrada ao VIP quando ele entra no mundo. {nome} vira o nome do personagem."));
        }
    }

    // ------------------------------------------------------------------ Aesir 6: colher e replantar

    [HarmonyPatch]
    internal static class AesirReplantar
    {
        private static Dictionary<string, string> _pares;
        private static string _paresFonte;

        private static Dictionary<string, string> Pares()
        {
            string fonte = AesirConfig.Aesir6ReplantarPares?.Value ?? "";
            if (_pares != null && fonte == _paresFonte) return _pares;
            _paresFonte = fonte;
            _pares = new Dictionary<string, string>(StringComparer.Ordinal);
            foreach (string par in fonte.Split(','))
            {
                string[] partes = par.Split(':');
                if (partes.Length == 2 && partes[0].Trim().Length > 0 && partes[1].Trim().Length > 0)
                    _pares[partes[0].Trim()] = partes[1].Trim();
            }
            return _pares;
        }

        [HarmonyPatch(typeof(Pickable), nameof(Pickable.Interact))]
        private static class ReplantarPatch
        {
            private static Vector3 _pos;
            private static Quaternion _rot;
            private static string _prefab;
            private static bool _colhivel;

            /// <summary>Instantiate acrescenta "(Clone)" ao nome do prefab original.</summary>
            private static string PrefabName(GameObject go)
            {
                const string sufixo = "(Clone)";
                string nome = go.name;
                return nome.EndsWith(sufixo, StringComparison.Ordinal) ? nome.Substring(0, nome.Length - sufixo.Length) : nome;
            }

            private static void Prefix(Pickable __instance)
            {
                _prefab = null;
                _colhivel = false;
                try
                {
                    if (__instance == null || __instance.transform == null) return;
                    // Antes da colheita: depois dela a planta ja esta marcada como colhida (ou destruida).
                    _colhivel = __instance.CanBePicked();
                    _pos = __instance.transform.position;
                    _rot = __instance.transform.rotation;
                    _prefab = PrefabName(__instance.gameObject);
                }
                catch (Exception e) { AesirLog.Erro(6, e); }
            }

            private static void Postfix(Pickable __instance, Humanoid character)
            {
                try { Replantar(__instance, character); }
                catch (Exception e) { AesirLog.Erro(6, e); }
            }

            /// <summary>
            /// Ate 02/11/2026 isto nunca replantava: exigia o retorno do Interact (que e o
            /// m_useInteractAnimation da planta, nao "colheu") e recusava m_harvestable, que no
            /// jogo quer dizer "a foice colhe" -- justamente as plantacoes. "Rebrota sozinha" e
            /// m_respawnTimeMinutes > 0 (frutinhas, cogumelos), e essas continuam de fora.
            /// </summary>
            private static void Replantar(Pickable __instance, Humanoid character)
            {
                if (!_colhivel || !AesirConfig.Aesir6ReplantarAtivo.Value || __instance == null || __instance.m_respawnTimeMinutes > 0f) return;
                if (!(character is Player player) || player != Player.m_localPlayer) return;
                if (!VipList.VipListApi.IsLocalPlayerVip()) return;
                if (_prefab == null || !Pares().TryGetValue(_prefab, out string semente))
                {
                    AesirLog.D(6, "colheu '" + _prefab + "': sem par em Aesir6ReplantarPares, nao replanta.");
                    return;
                }
                if (ZNetScene.instance == null) return;

                // O que nasce no chao e a muda (Plant), nao o item da semente. A config aceita os
                // dois nomes: a muda direto ("sapling_carrot") ou o item ("CarrotSeeds" -> "sapling_carrot").
                GameObject muda = ZNetScene.instance.GetPrefab(semente);
                if (muda == null || muda.GetComponent<Plant>() == null)
                    muda = ZNetScene.instance.GetPrefab("sapling_" + semente.Replace("Seeds", "").ToLowerInvariant());
                if (muda == null || muda.GetComponent<Plant>() == null)
                {
                    AesirLog.D(6, "par '" + _prefab + ":" + semente + "': nenhuma muda (Plant) com esse nome nem 'sapling_...'; conferir a config.");
                    return;
                }

                // A semente que a muda custa vem da propria peca, com o nome que o inventario conhece.
                Piece peca = muda.GetComponent<Piece>();
                Piece.Requirement custo = peca != null && peca.m_resources != null && peca.m_resources.Length > 0 ? peca.m_resources[0] : null;
                string itemSemente = custo?.m_resItem?.m_itemData?.m_shared?.m_name;
                if (string.IsNullOrEmpty(itemSemente))
                {
                    AesirLog.D(6, "muda '" + muda.name + "' sem custo de semente na Piece: nao replanta.");
                    return;
                }

                Inventory inv = player.GetInventory();
                int quantidade = Mathf.Max(1, custo.m_amount);
                if (inv.CountItems(itemSemente, -1, false) < quantidade)
                {
                    AesirLog.D(6, "colheu '" + _prefab + "': sem " + quantidade + "x " + itemSemente + " no inventario, nao replantou.");
                    return;
                }

                inv.RemoveItem(itemSemente, quantidade, -1, false);
                UnityEngine.Object.Instantiate(muda, _pos, _rot);
                AesirLog.D(6, "replantou '" + muda.name + "' no lugar de '" + _prefab + "' (-" + quantidade + "x " + itemSemente + ").");
            }
        }
    }

    // ------------------------------------------------------------------ Aesir 7: abastecer em area

    [HarmonyPatch]
    internal static class AesirAbastecerArea
    {
        [ThreadStatic] private static bool _emProgresso;

        private static bool ShiftApertado() => Input.GetKey(KeyCode.LeftShift) || Input.GetKey(KeyCode.RightShift);

        private static bool Permitido(Humanoid user)
        {
            if (user != Player.m_localPlayer || !ShiftApertado()) return false;
            if (_emProgresso) return false;   // as chamadas da propria propagacao
            if (!AesirConfig.Aesir7AbastecerAreaAtivo.Value) { AesirLog.D(7, "Shift ao abastecer: negado (Aesir7AbastecerAreaAtivo=false)."); return false; }
            if (!VipList.VipListApi.IsLocalPlayerVip()) { AesirLog.D(7, "Shift ao abastecer: negado (nao e VIP)."); return false; }
            return true;
        }

        /// <summary>Chama de novo o metodo privado (OnAddFuel/OnAddOre) nas fornalhas iguais por perto.</summary>
        private static void Propagar(MethodInfo metodo, Smelter origem, Switch sw, Humanoid user)
        {
            if (_emProgresso || origem == null) return;
            ZNetView nviewOrigem = origem.GetComponent<ZNetView>();
            int prefab = nviewOrigem != null && nviewOrigem.IsValid() ? nviewOrigem.GetZDO().GetPrefab() : 0;
            if (prefab == 0) return;

            _emProgresso = true;
            try
            {
                // A fornalha usada vem primeiro e as demais por distancia: o estoque do jogador
                // enche a que ele tocou antes de sobrar para as vizinhas.
                var alvos = new List<Smelter> { origem };
                foreach (Collider col in Physics.OverlapSphere(origem.transform.position, AesirConfig.Aesir7AbastecerAreaRaio.Value))
                {
                    Smelter alvo = col.GetComponentInParent<Smelter>();
                    if (alvo == null || alvos.Contains(alvo)) continue;
                    ZNetView nviewAlvo = alvo.GetComponent<ZNetView>();
                    if (nviewAlvo == null || !nviewAlvo.IsValid() || nviewAlvo.GetZDO().GetPrefab() != prefab) continue;
                    alvos.Add(alvo);
                }
                Vector3 centro = origem.transform.position;
                alvos.Sort((a, b) => a == origem ? -1 : b == origem ? 1
                    : (a.transform.position - centro).sqrMagnitude.CompareTo((b.transform.position - centro).sqrMagnitude));

                int unidades = 0, abastecidas = 0;
                foreach (Smelter alvo in alvos)
                {
                    // Ate 50 unidades por fornalha: nao trava o jogo se alguem tiver um estoque gigante.
                    // A origem ja recebeu uma unidade do clique original.
                    int antes = unidades;
                    for (int i = alvo == origem ? 1 : 0; i < 50; i++)
                    {
                        if (!(metodo.Invoke(alvo, new object[] { sw, user, null }) is bool ok) || !ok) break;
                        unidades++;
                    }
                    if (unidades > antes) abastecidas++;
                }
                AesirLog.D(7, metodo.Name + " em area: " + Utils.GetPrefabName(origem.gameObject) + ", " + alvos.Count + " no raio de "
                    + AesirConfig.Aesir7AbastecerAreaRaio.Value + " m, " + abastecidas + " recebeu(ram) " + unidades + " unidade(s).");
            }
            catch (Exception e) { AesirLog.Erro(7, e); }
            finally { _emProgresso = false; }
        }

        [HarmonyPatch(typeof(Smelter), "OnAddFuel")]
        private static class FuelPatch
        {
            private static readonly MethodInfo Metodo = AccessTools.Method(typeof(Smelter), "OnAddFuel");
            private static void Postfix(Smelter __instance, Switch sw, Humanoid user, bool __result)
            {
                if (__result && Permitido(user)) Propagar(Metodo, __instance, sw, user);
            }
        }

        [HarmonyPatch(typeof(Smelter), "OnAddOre")]
        private static class OrePatch
        {
            private static readonly MethodInfo Metodo = AccessTools.Method(typeof(Smelter), "OnAddOre");
            private static void Postfix(Smelter __instance, Switch sw, Humanoid user, bool __result)
            {
                if (__result && Permitido(user)) Propagar(Metodo, __instance, sw, user);
            }
        }
    }

    // ------------------------------------------------------------ Aesir 8: ferramentas sem desgaste

    [HarmonyPatch]
    internal static class AesirFerramentasSemDesgaste
    {
        private static HashSet<string> _nomes;
        private static string _nomesFonte;

        private static HashSet<string> Nomes()
        {
            string fonte = AesirConfig.Aesir8Ferramentas?.Value ?? "";
            if (_nomes != null && fonte == _nomesFonte) return _nomes;
            _nomesFonte = fonte;
            _nomes = new HashSet<string>(fonte.Split(',').Select(s => s.Trim()).Where(s => s.Length > 0), StringComparer.Ordinal);
            return _nomes;
        }

        [HarmonyPatch(typeof(Humanoid), "DrainEquipedItemDurability")]
        private static class Patch
        {
            private static bool Prefix(Humanoid __instance, ItemDrop.ItemData item)
            {
                try
                {
                    if (!AesirConfig.Aesir8SemDesgasteAtivo.Value || __instance != Player.m_localPlayer || item?.m_shared == null) return true;
                    if (!VipList.VipListApi.IsLocalPlayerVip() || !Nomes().Contains(item.m_shared.m_name)) return true;
                    AesirLog.D(8, item.m_shared.m_name + " usada sem desgaste.");
                    return false; // nao desgasta
                }
                catch (Exception e) { AesirLog.Erro(8, e); return true; }
            }
        }
    }

    // --------------------------------------------------------- Aesir 9: tempo restante ao mirar

    [HarmonyPatch]
    internal static class AesirTempoRestante
    {
        [HarmonyPatch(typeof(Pickable), nameof(Pickable.GetHoverText))]
        private static class Patch
        {
            // GetHoverText roda a cada frame com o recurso na mira: log no maximo a cada 30 s.
            private static float _ultimoLog = -999f;

            private static void Postfix(Pickable __instance, ref string __result)
            {
                try { Mostrar(__instance, ref __result); }
                catch (Exception e) { AesirLog.Erro(9, e); }
            }

            private static void Mostrar(Pickable __instance, ref string __result)
            {
                if (!AesirConfig.Aesir9TempoRestanteAtivo.Value || __instance == null) return;
                if (!VipList.VipListApi.IsLocalPlayerVip()) return;
                if (!__instance.GetPicked() || __instance.m_respawnTimeMinutes <= 0f) return;

                ZNetView nview = __instance.GetComponent<ZNetView>();
                ZDO zdo = nview != null && nview.IsValid() ? nview.GetZDO() : null;
                if (zdo == null) return;
                long pickedTick = zdo.GetLong(ZDOVars.s_pickedTime, 0L);
                if (pickedTick == 0L) return;

                double passados = (ZNet.instance != null ? ZNet.instance.GetTime() : DateTime.Now).Subtract(new DateTime(pickedTick)).TotalMinutes;
                double faltam = __instance.m_respawnTimeMinutes - passados;
                if (faltam > 0d)
                {
                    __result += $"\n<color=#9be564>Volta em {faltam:0} min</color>";
                    if (AesirLog.Pode(ref _ultimoLog, 30f))
                        AesirLog.D(9, "mostrou o tempo restante de " + Utils.GetPrefabName(__instance.gameObject) + ": " + faltam.ToString("0") + " min.");
                }
            }
        }
    }

    // ---------------------------------------------------------------- Aesir 12: reparar em area

    [HarmonyPatch]
    internal static class AesirRepararArea
    {
        [HarmonyPatch(typeof(Player), "Repair")]
        private static class Patch
        {
            private static void Postfix(Player __instance)
            {
                try { Reparar(__instance); }
                catch (Exception e) { AesirLog.Erro(12, e); }
            }

            private static void Reparar(Player __instance)
            {
                if (__instance != Player.m_localPlayer) return;
                if (!Input.GetKey(KeyCode.LeftShift) && !Input.GetKey(KeyCode.RightShift)) return;
                if (!AesirConfig.Aesir12RepararAreaAtivo.Value) { AesirLog.D(12, "Shift ao reparar: negado (Aesir12RepararAreaAtivo=false)."); return; }
                if (!VipList.VipListApi.IsLocalPlayerVip()) { AesirLog.D(12, "Shift ao reparar: negado (nao e VIP)."); return; }

                Piece centro = __instance.GetHoveringPiece();
                if (centro == null) return;

                int reparadas = 0, semAcesso = 0;
                foreach (Collider col in Physics.OverlapSphere(centro.transform.position, AesirConfig.Aesir12RepararAreaRaio.Value))
                {
                    WearNTear alvo = col.GetComponentInParent<WearNTear>();
                    if (alvo == null || alvo.gameObject == centro.gameObject) continue;
                    if (!PrivateArea.CheckAccess(alvo.transform.position)) { semAcesso++; continue; }
                    if (alvo.Repair()) reparadas++;
                }
                if (reparadas > 0)
                    __instance.Message(MessageHud.MessageType.TopLeft, $"Aesir: {reparadas} construção(ões) reparada(s) por perto.");
                AesirLog.D(12, "reparo em area (raio " + AesirConfig.Aesir12RepararAreaRaio.Value + " m): " + reparadas + " reparada(s)"
                    + (semAcesso > 0 ? ", " + semAcesso + " colisor(es) em ward alheio" : "") + ".");
            }
        }
    }

    // ------------------------------------------- Aesir 14, 15, 16: acoes de bau (deposito, ordem, etiqueta)

    /// <summary>
    /// Desde 02/11/2026 (decisao do usuario: o jogador nao digita comando) as tres acoes saem por tecla
    /// com o bau na mira ou aberto, com a dica no proprio bau (AesirTeclas, AesirDicas). Os comandos
    /// /depositar, /ordenar e /etiqueta continuam valendo e passam pelo mesmo codigo.
    /// </summary>
    [HarmonyPatch]
    internal static class AesirBaus
    {
        private static readonly FieldInfo CurrentContainer = AccessTools.Field(typeof(InventoryGui), "m_currentContainer");

        /// <summary>O bau com a janela aberta agora, ou null.</summary>
        internal static Container Aberto()
            => InventoryGui.instance != null && InventoryGui.IsVisible() ? CurrentContainer?.GetValue(InventoryGui.instance) as Container : null;

        /// <summary>O bau na mira do jogador local, ou null.</summary>
        internal static Container NaMira()
        {
            GameObject alvo = Player.m_localPlayer != null ? Player.m_localPlayer.GetHoverObject() : null;
            return alvo != null ? alvo.GetComponentInParent<Container>() : null;
        }

        [HarmonyPatch(typeof(Terminal), nameof(Terminal.InitTerminal))]
        private static class ComandosPatch
        {
            private static void Postfix()
            {
                new Terminal.ConsoleCommand("depositar", "(Aesir) deposita do seu inventario nos baus proximos que ja tem o item", args =>
                {
                    try { Depositar(args.Context.AddString); } catch (Exception e) { AesirLog.Erro(14, e); }
                });
                new Terminal.ConsoleCommand("ordenar", "(Aesir) ordena por nome o bau com a janela aberta", args =>
                {
                    try { Ordenar(args.Context.AddString, Aberto()); } catch (Exception e) { AesirLog.Erro(15, e); }
                });
                new Terminal.ConsoleCommand("etiqueta", "(Aesir) etiqueta <nome> - renomeia o bau que voce esta olhando", args =>
                {
                    try
                    {
                        if (args.Length < 2) { args.Context.AddString("etiqueta <nome>"); return; }
                        Etiquetar(args.Context.AddString, NaMira(), string.Join(" ", args.Args, 1, args.Length - 1));
                    }
                    catch (Exception e) { AesirLog.Erro(16, e); }
                });
            }
        }

        internal static bool PodeUsar(Action<string> dizer, ConfigEntry<bool> chave, int item)
        {
            if (Player.m_localPlayer == null) return false;
            if (!VipList.VipListApi.IsLocalPlayerVip()) { dizer?.Invoke("So Aesir."); AesirLog.D(item, "negado: nao e VIP."); return false; }
            if (!chave.Value) { dizer?.Invoke("Desligado neste servidor."); AesirLog.D(item, "negado: " + chave.Definition.Key + "=false."); return false; }
            return true;
        }

        /// <summary>Ha um bau com a janela aberta (o /ordenar e o botao da Central dependem disso).</summary>
        internal static bool BauAberto() => Aberto() != null;

        // ------------------------------------------------------------------ 14: depositar nos baus por perto

        private static float _conferirEm = -1f;
        private static int _inventarioAntes;
        private static int _pedidos;

        /// <summary>
        /// O "empilhar tudo" do jogo (Container.StackAll) em cada bau do raio, do mais perto ao mais longe.
        ///
        /// Antes (ate 02/11/2026) o deposito fazia inv.AddItem(item.Clone()), que punha a pilha inteira no
        /// bau, e tirava 1 do inventario: duplicava itens. O Container.StackAll pede ao dono do bau, que
        /// recusa se outro jogador estiver com ele aberto ou se o bau for privado de outro, passa a posse
        /// e so entao o cliente move: so o que o bau ja tem e exatamente o que sai do inventario.
        /// A resposta chega pela rede; a conta do inventario vai ao log 2 s depois (Conferir).
        ///
        /// O 7.9.0 trazia outra correcao (ClaimOwnership e AddItem da copia, tirando do inventario o que
        /// coube). Ficou esta no merge porque quem decide e o dono do bau: ele confere uso e acesso e so
        /// entao passa a posse, entao nunca ha dois clientes gravando o mesmo bau ao mesmo tempo.
        /// Usado pelo /depositar, pela tecla (AesirTeclas) e pelo botao da Central Deadheim.
        /// </summary>
        internal static void Depositar(Action<string> dizer)
        {
            if (!PodeUsar(dizer, AesirConfig.Aesir14DepositarAtivo, 14)) return;
            Player player = Player.m_localPlayer;
            Inventory minha = player.GetInventory();

            // Cada bau uma vez so (um bau tem varios colisores), do mais perto para o mais longe.
            var baus = new List<Container>();
            foreach (Collider col in Physics.OverlapSphere(player.transform.position, AesirConfig.Aesir14DepositarRaio.Value))
            {
                Container container = col.GetComponentInParent<Container>();
                if (container != null && !baus.Contains(container)) baus.Add(container);
            }
            Vector3 centro = player.transform.position;
            baus.Sort((a, b) => (a.transform.position - centro).sqrMagnitude.CompareTo((b.transform.position - centro).sqrMagnitude));

            int pedidos = 0, ocupados = 0, semAcesso = 0, semItem = 0;
            foreach (Container container in baus)
            {
                Inventory inv = container.GetInventory();
                if (inv == null) continue;
                if (!PrivateArea.CheckAccess(container.transform.position, 0f, false)) { semAcesso++; continue; }
                if (container.IsInUse()) { ocupados++; continue; }
                // Sem nenhum item em comum o pedido nao moveria nada: nem manda.
                if (!minha.GetAllItems().Any(item => !item.m_equipped && inv.CountItems(item.m_shared.m_name, -1, false) > 0)) { semItem++; continue; }
                container.StackAll();
                pedidos++;
            }

            if (pedidos > 0) dizer?.Invoke($"Depositando em {pedidos} bau(s) por perto...");
            else dizer?.Invoke("Nada para depositar: nenhum bau por perto tem itens que voce carrega.");
            if (ocupados > 0) dizer?.Invoke($"{ocupados} bau(s) aberto(s) por outro jogador ficaram de fora.");
            AesirLog.D(14, "depositar: " + baus.Count + " bau(s) no raio de " + AesirConfig.Aesir14DepositarRaio.Value + " m, pediu a "
                + pedidos + " (" + semItem + " sem item em comum, " + semAcesso + " sem acesso, " + ocupados + " em uso).");
            if (pedidos > 0)
            {
                _inventarioAntes = Total(minha);
                _pedidos = pedidos;
                _conferirEm = Time.realtimeSinceStartup + 2f;
            }
        }

        /// <summary>Chamado pelo AesirTeclas.Tick: 2 s depois do deposito, quanto saiu do inventario.</summary>
        internal static void Conferir()
        {
            if (_conferirEm < 0f || Time.realtimeSinceStartup < _conferirEm) return;
            _conferirEm = -1f;
            Player player = Player.m_localPlayer;
            if (player == null) return;
            int saiu = _inventarioAntes - Total(player.GetInventory());
            AesirLog.D(14, "depositar: " + saiu + " item(ns) sairam do inventario para " + _pedidos + " bau(s) (cada bau mostra o que recebeu na tela).");
        }

        private static int Total(Inventory inv)
        {
            int total = 0;
            foreach (ItemDrop.ItemData item in inv.GetAllItems()) total += item.m_stack;
            return total;
        }

        // ------------------------------------------------------------------ 15: ordenar

        /// <summary>
        /// So com o bau aberto: abrir passa a posse da ZDO para quem abriu (o jogo recusa se outro estiver
        /// com ele), e so o dono grava o inventario do bau. Nao chama mais o Container.StackAll, que alem de
        /// ordenar depositava o inventario do jogador e chegava depois, pela rede, baguncando a ordem.
        /// </summary>
        /// <summary>O bau aberto agora: o /ordenar e o botao da Central Deadheim.</summary>
        internal static void Ordenar(Action<string> dizer) => Ordenar(dizer, Aberto());

        internal static void Ordenar(Action<string> dizer, Container container)
        {
            if (!PodeUsar(dizer, AesirConfig.Aesir15OrdenarAtivo, 15)) return;
            if (container == null) { dizer?.Invoke("Abra o bau para ordenar."); AesirLog.D(15, "ordenar sem bau aberto."); return; }

            Inventory inv = container.GetInventory();
            int antesItens = Total(inv);
            List<ItemDrop.ItemData> itens = inv.GetAllItemsSortedByName();
            int antes = itens.Count;
            foreach (ItemDrop.ItemData item in itens) inv.RemoveItem(item);
            // Em ordem de leitura, da esquerda para a direita e de cima para baixo. O AddItem sem
            // posicao poe material de baixo para cima (02/11/2026, visto no teste local).
            int largura = Mathf.Max(1, inv.GetWidth());
            for (int i = 0; i < itens.Count; i++)
                if (!inv.AddItem(itens[i], new Vector2i(i % largura, i / largura))) inv.AddItem(itens[i]);
            dizer?.Invoke("Bau ordenado.");
            AesirLog.D(15, "ordenar: " + Utils.GetPrefabName(container.gameObject) + " com " + antes + " pilha(s), depois " + inv.GetAllItems().Count
                + (Total(inv) != antesItens ? "; ATENCAO: itens " + antesItens + " -> " + Total(inv) : "") + ".");
        }

        // ------------------------------------------------------------------ 16: etiqueta

        /// <summary>Abre a caixa de texto do jogo (a mesma da placa) para dar nome ao bau.</summary>
        internal static void PedirEtiqueta(Action<string> dizer, Container container)
        {
            if (!PodeUsar(dizer, AesirConfig.Aesir16EtiquetaAtivo, 16)) return;
            if (container == null) { dizer?.Invoke("Olhe para um bau."); return; }
            if (TextInput.instance == null) return;
            if (InventoryGui.IsVisible()) InventoryGui.instance.Hide();
            TextInput.instance.RequestText(new Receptor(container), "Nome do bau (vazio volta ao nome original)", 32);
        }

        private sealed class Receptor : TextReceiver
        {
            private readonly Container _container;
            public Receptor(Container container) { _container = container; }

            public string GetText()
            {
                ZNetView nview = _container != null ? _container.GetComponent<ZNetView>() : null;
                return nview != null && nview.IsValid() ? nview.GetZDO().GetString("dh_etiqueta", "") : "";
            }

            public void SetText(string text)
            {
                Action<string> dizer = t => Player.m_localPlayer?.Message(MessageHud.MessageType.Center, t);
                try { Etiquetar(dizer, _container, text); }
                catch (Exception e) { AesirLog.Erro(16, e); }
            }
        }

        /// <summary>Grava a etiqueta na ZDO do bau. Nome vazio apaga a etiqueta (volta o nome do jogo).</summary>
        internal static void Etiquetar(Action<string> dizer, Container container, string texto)
        {
            if (!PodeUsar(dizer, AesirConfig.Aesir16EtiquetaAtivo, 16)) return;
            if (container == null) { dizer?.Invoke("Olhe para um bau."); AesirLog.D(16, "etiqueta sem bau na mira."); return; }
            if (!PrivateArea.CheckAccess(container.transform.position)) { dizer?.Invoke("Sem acesso a esse bau."); AesirLog.D(16, "etiqueta negada: bau em ward alheio."); return; }

            string nome = (texto ?? "").Trim();
            if (nome.Length > 32) { dizer?.Invoke("Nome de ate 32 caracteres."); AesirLog.D(16, "etiqueta negada: nome com " + nome.Length + " caracteres."); return; }

            ZNetView nview = container.GetComponent<ZNetView>();
            if (nview == null || !nview.IsValid()) { AesirLog.D(16, "etiqueta: bau sem ZNetView valida."); return; }
            // Como a placa do jogo: grava como dono. Com outro jogador dentro do bau, nao toma a posse dele.
            if (!nview.IsOwner())
            {
                if (container.IsInUse()) { dizer?.Invoke("Bau em uso por outro jogador."); AesirLog.D(16, "etiqueta negada: bau em uso por outro jogador."); return; }
                nview.ClaimOwnership();
            }
            nview.GetZDO().Set("dh_etiqueta", nome);
            AtualizarNome(container, nview);
            dizer?.Invoke(nome.Length > 0 ? $"Bau etiquetado: '{nome}'." : "Etiqueta removida.");
            AesirLog.D(16, "etiqueta: " + Utils.GetPrefabName(container.gameObject) + (nome.Length > 0 ? " agora se chama '" + nome + "'." : " voltou ao nome original."));
        }

        /// <summary>O nome do bau segue a etiqueta da ZDO; sem etiqueta, o nome do prefab.</summary>
        internal static void AtualizarNome(Container container, ZNetView nview)
        {
            if (nview == null || !nview.IsValid()) return;
            string etiqueta = nview.GetZDO().GetString("dh_etiqueta", "");
            if (etiqueta.Length > 0) { container.m_name = etiqueta; return; }
            GameObject prefab = ZNetScene.instance != null ? ZNetScene.instance.GetPrefab(Utils.GetPrefabName(container.gameObject)) : null;
            Container original = prefab != null ? prefab.GetComponent<Container>() : null;
            if (original != null) container.m_name = original.m_name;
        }

        /// <summary>O bau carrega a etiqueta na propria ZDO: todo cliente que o abrir ve o nome.</summary>
        [HarmonyPatch(typeof(Container), "Awake")]
        private static class EtiquetaAwakePatch
        {
            private static void Postfix(Container __instance)
            {
                try
                {
                    ZNetView nview = __instance.GetComponent<ZNetView>();
                    string etiqueta = nview != null && nview.IsValid() ? nview.GetZDO().GetString("dh_etiqueta", "") : "";
                    if (!string.IsNullOrEmpty(etiqueta)) __instance.m_name = etiqueta;
                }
                catch (Exception e) { AesirLog.Erro(16, e); }
            }
        }

        /// <summary>Etiqueta trocada por outro jogador depois que o bau carregou: o nome acompanha na mira.</summary>
        [HarmonyPatch(typeof(Container), nameof(Container.GetHoverText))]
        private static class EtiquetaNaMiraPatch
        {
            private static void Prefix(Container __instance)
            {
                try
                {
                    ZNetView nview = __instance.GetComponent<ZNetView>();
                    if (nview == null || !nview.IsValid()) return;
                    string etiqueta = nview.GetZDO().GetString("dh_etiqueta", "");
                    // So mexe quando ha etiqueta, ou quando o nome ainda e o de uma etiqueta apagada.
                    if (etiqueta.Length > 0) { if (__instance.m_name != etiqueta) __instance.m_name = etiqueta; }
                    else if (__instance.m_name.Length > 0 && __instance.m_name[0] != '$') AtualizarNome(__instance, nview);
                }
                catch (Exception e) { AesirLog.Erro(16, e); }
            }
        }
    }

    // ------------------------------------------------------------------- Aesir 17: Descansado +%

    [HarmonyPatch]
    internal static class AesirRestedMaior
    {
        [HarmonyPatch(typeof(SE_Rested), "UpdateTTL")]
        private static class Patch
        {
            private static void Prefix(SE_Rested __instance, out float[] __state)
            {
                __state = null;
                try
                {
                    if (!AesirConfig.Aesir17RestedAtivo.Value || __instance.m_character != Player.m_localPlayer) return;
                    if (!VipList.VipListApi.IsLocalPlayerVip()) return;

                    float k = 1f + Mathf.Max(0f, AesirConfig.Aesir17RestedBonus.Value);
                    __state = new[] { __instance.m_baseTTL, __instance.m_TTLPerComfortLevel };
                    __instance.m_baseTTL *= k;
                    __instance.m_TTLPerComfortLevel *= k;
                    AesirLog.D(17, "Descansado x" + k.ToString("0.##") + " (base " + __state[0] + " s -> " + __instance.m_baseTTL + " s).");
                }
                catch (Exception e) { AesirLog.Erro(17, e); }
            }

            private static void Postfix(SE_Rested __instance, float[] __state)
            {
                if (__state == null) return;
                __instance.m_baseTTL = __state[0];
                __instance.m_TTLPerComfortLevel = __state[1];
            }
        }
    }

    // --------------------------------------------------------------- Aesir 26: mensagem de entrada

    [HarmonyPatch]
    internal static class AesirMensagemEntrada
    {
        [HarmonyPatch(typeof(Player), nameof(Player.OnSpawned))]
        private static class Patch
        {
            private static void Postfix(Player __instance)
            {
                try
                {
                    if (__instance != Player.m_localPlayer) return;
                    bool vip = VipList.VipListApi.IsLocalPlayerVip();
                    // Uma linha por entrada com o que o cliente acha de si: a base para ler as dos outros itens.
                    AesirLog.D(26, "entrou no mundo: VIP=" + vip + ", Aesir26Ativo=" + AesirConfig.Aesir26Ativo.Value + ".");
                    if (!AesirConfig.Aesir26Ativo.Value || !vip) return;
                    string texto = (AesirConfig.Aesir26Mensagem.Value ?? "").Replace("{nome}", __instance.GetPlayerName());
                    if (texto.Length > 0) __instance.Message(MessageHud.MessageType.Center, texto);
                }
                catch (Exception e) { AesirLog.Erro(26, e); }
            }
        }
    }
}
