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
                if (__instance == null || __instance.transform == null) return;
                _pos = __instance.transform.position;
                _rot = __instance.transform.rotation;
                _prefab = PrefabName(__instance.gameObject);
            }

            private static void Postfix(Pickable __instance, Humanoid character, bool __result)
            {
                if (!__result || !AesirConfig.Aesir6ReplantarAtivo.Value || __instance == null || __instance.m_harvestable) return;
                if (!(character is Player player) || player != Player.m_localPlayer) return;
                if (!VipList.VipListApi.IsLocalPlayerVip()) return;
                if (_prefab == null || !Pares().TryGetValue(_prefab, out string semente)) return;

                Inventory inv = player.GetInventory();
                if (inv.CountItems(semente, -1, false) <= 0) return;
                GameObject prefabSemente = ZNetScene.instance != null ? ZNetScene.instance.GetPrefab(semente) : null;
                if (prefabSemente == null) return;

                inv.RemoveItem(semente, 1, -1, false);
                UnityEngine.Object.Instantiate(prefabSemente, _pos, _rot);
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
            => AesirConfig.Aesir7AbastecerAreaAtivo.Value && user == Player.m_localPlayer
               && VipList.VipListApi.IsLocalPlayerVip() && ShiftApertado();

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
                foreach (Collider col in Physics.OverlapSphere(origem.transform.position, AesirConfig.Aesir7AbastecerAreaRaio.Value))
                {
                    Smelter alvo = col.GetComponentInParent<Smelter>();
                    if (alvo == null || alvo == origem) continue;
                    ZNetView nviewAlvo = alvo.GetComponent<ZNetView>();
                    if (nviewAlvo == null || !nviewAlvo.IsValid() || nviewAlvo.GetZDO().GetPrefab() != prefab) continue;
                    // Ate 50 unidades por fornalha: nao trava o jogo se alguem tiver um estoque gigante.
                    for (int i = 0; i < 50; i++)
                        if (!(metodo.Invoke(alvo, new object[] { sw, user, null }) is bool ok) || !ok) break;
                }
            }
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
                if (!AesirConfig.Aesir8SemDesgasteAtivo.Value || __instance != Player.m_localPlayer || item?.m_shared == null) return true;
                if (!VipList.VipListApi.IsLocalPlayerVip() || !Nomes().Contains(item.m_shared.m_name)) return true;
                return false; // nao desgasta
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
            private static void Postfix(Pickable __instance, ref string __result)
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
                    __result += $"\n<color=#9be564>Volta em {faltam:0} min</color>";
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
                if (!AesirConfig.Aesir12RepararAreaAtivo.Value || __instance != Player.m_localPlayer) return;
                if (!VipList.VipListApi.IsLocalPlayerVip()) return;
                if (!Input.GetKey(KeyCode.LeftShift) && !Input.GetKey(KeyCode.RightShift)) return;

                Piece centro = __instance.GetHoveringPiece();
                if (centro == null) return;

                int reparadas = 0;
                foreach (Collider col in Physics.OverlapSphere(centro.transform.position, AesirConfig.Aesir12RepararAreaRaio.Value))
                {
                    WearNTear alvo = col.GetComponentInParent<WearNTear>();
                    if (alvo == null || alvo.gameObject == centro.gameObject) continue;
                    if (!PrivateArea.CheckAccess(alvo.transform.position)) continue;
                    if (alvo.Repair()) reparadas++;
                }
                if (reparadas > 0)
                    __instance.Message(MessageHud.MessageType.TopLeft, $"Aesir: {reparadas} construção(ões) reparada(s) por perto.");
            }
        }
    }

    // --------------------------------------------- Aesir 14, 15, 16: comandos de bau (deposito, ordem, etiqueta)

    [HarmonyPatch]
    internal static class AesirBaus
    {
        private static readonly FieldInfo CurrentContainer = AccessTools.Field(typeof(InventoryGui), "m_currentContainer");

        [HarmonyPatch(typeof(Terminal), nameof(Terminal.InitTerminal))]
        private static class ComandosPatch
        {
            private static void Postfix()
            {
                new Terminal.ConsoleCommand("depositar", "(Aesir) deposita do seu inventario nos baus proximos que ja tem o item", args => Depositar(args.Context));
                new Terminal.ConsoleCommand("ordenar", "(Aesir) ordena por nome o bau com a janela aberta", args => Ordenar(args.Context));
                new Terminal.ConsoleCommand("etiqueta", "(Aesir) etiqueta <nome> - renomeia o bau que voce esta olhando", args => Etiquetar(args));
            }
        }

        private static bool PodeUsar(Terminal context, ConfigEntry<bool> chave)
        {
            if (Player.m_localPlayer == null) return false;
            if (!VipList.VipListApi.IsLocalPlayerVip()) { context?.AddString("So Aesir."); return false; }
            if (!chave.Value) { context?.AddString("Desligado neste servidor."); return false; }
            return true;
        }

        private static void Depositar(Terminal context)
        {
            if (!PodeUsar(context, AesirConfig.Aesir14DepositarAtivo)) return;
            Player player = Player.m_localPlayer;
            Inventory minha = player.GetInventory();
            int depositados = 0;

            foreach (Collider col in Physics.OverlapSphere(player.transform.position, AesirConfig.Aesir14DepositarRaio.Value))
            {
                Container container = col.GetComponentInParent<Container>();
                if (container == null || !PrivateArea.CheckAccess(container.transform.position)) continue;
                Inventory inv = container.GetInventory();
                if (inv == null) continue;

                foreach (ItemDrop.ItemData item in minha.GetAllItems().ToList())
                {
                    if (item == null || minha.CountItems(item.m_shared.m_name, -1, false) <= 0) continue;
                    if (inv.CountItems(item.m_shared.m_name, -1, false) <= 0) continue; // so completa pilha que ja existe no bau

                    int tinha = minha.CountItems(item.m_shared.m_name, -1, false);
                    if (!inv.AddItem(item.Clone())) continue;
                    minha.RemoveItem(item.m_shared.m_name, 1, -1, false);
                    if (tinha > 0) depositados++;
                }
            }
            if (depositados > 0) context?.AddString($"Depositado(s) {depositados} item(ns).");
            else context?.AddString("Nada para depositar (so completa pilha que o bau ja tem).");
        }

        private static void Ordenar(Terminal context)
        {
            if (!PodeUsar(context, AesirConfig.Aesir15OrdenarAtivo)) return;
            Container container = CurrentContainer?.GetValue(InventoryGui.instance) as Container;
            if (container == null) { context?.AddString("Abra um bau primeiro."); return; }

            container.StackAll();
            Inventory inv = container.GetInventory();
            List<ItemDrop.ItemData> itens = inv.GetAllItemsSortedByName();
            foreach (ItemDrop.ItemData item in itens) inv.RemoveItem(item);
            foreach (ItemDrop.ItemData item in itens) inv.AddItem(item);
            context?.AddString("Bau ordenado.");
        }

        private static void Etiquetar(Terminal.ConsoleEventArgs args)
        {
            if (!PodeUsar(args.Context, AesirConfig.Aesir16EtiquetaAtivo)) return;
            if (args.Length < 2) { args.Context.AddString("etiqueta <nome>"); return; }

            GameObject alvoGo = Player.m_localPlayer.GetHoverObject();
            Container container = alvoGo != null ? alvoGo.GetComponentInParent<Container>() : null;
            if (container == null) { args.Context.AddString("Olhe para um bau."); return; }
            if (!PrivateArea.CheckAccess(container.transform.position)) { args.Context.AddString("Sem acesso a esse bau."); return; }

            string nome = string.Join(" ", args.Args, 1, args.Length - 1).Trim();
            if (nome.Length == 0 || nome.Length > 32) { args.Context.AddString("Nome de 1 a 32 caracteres."); return; }

            ZNetView nview = container.GetComponent<ZNetView>();
            if (nview == null || !nview.IsValid()) return;
            nview.GetZDO().Set("dh_etiqueta", nome);
            container.m_name = nome;
            args.Context.AddString($"Bau etiquetado: '{nome}'.");
        }

        /// <summary>O bau carrega a etiqueta na propria ZDO: todo cliente que o abrir ve o nome.</summary>
        [HarmonyPatch(typeof(Container), "Awake")]
        private static class EtiquetaAwakePatch
        {
            private static void Postfix(Container __instance)
            {
                ZNetView nview = __instance.GetComponent<ZNetView>();
                string etiqueta = nview != null && nview.IsValid() ? nview.GetZDO().GetString("dh_etiqueta", "") : "";
                if (!string.IsNullOrEmpty(etiqueta)) __instance.m_name = etiqueta;
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
                if (!AesirConfig.Aesir17RestedAtivo.Value || __instance.m_character != Player.m_localPlayer) return;
                if (!VipList.VipListApi.IsLocalPlayerVip()) return;

                float k = 1f + Mathf.Max(0f, AesirConfig.Aesir17RestedBonus.Value);
                __state = new[] { __instance.m_baseTTL, __instance.m_TTLPerComfortLevel };
                __instance.m_baseTTL *= k;
                __instance.m_TTLPerComfortLevel *= k;
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
                if (__instance != Player.m_localPlayer || !AesirConfig.Aesir26Ativo.Value) return;
                if (!VipList.VipListApi.IsLocalPlayerVip()) return;
                string texto = (AesirConfig.Aesir26Mensagem.Value ?? "").Replace("{nome}", __instance.GetPlayerName());
                if (texto.Length > 0) __instance.Message(MessageHud.MessageType.Center, texto);
            }
        }
    }
}
