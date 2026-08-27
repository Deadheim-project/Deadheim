# RaidSystem — Especificação de Economia de Território

Spec fechado para implementação. **Todas as decisões de design já foram tomadas** — quem
implementa não precisa escolher nada, só executar. Toda assinatura do jogo citada aqui foi
verificada no decompilado em `C:\Users\Werner\Documents\ChatGPT\valheim\_reference\ValheimDecompiled`.

Implementar **na ordem das fases**. Cada fase compila e é testável sozinha.

---

## 0. Contexto e regras de negócio

O sistema de raid atual só entrega ao dominante: acesso a porta (`DoorInteractPatch`) e pontos.
Este spec adiciona **motivo econômico** para conquistar e defender.

Três regras que não devem ser renegociadas durante a implementação:

1. **Minério só ENTRA em território, nunca SAI.** Portal de minério é permitido quando o
   *destino* é território da sua guild. Consequência: tributo só viaja por terra.
2. **Tributo acumula e tem teto.** Guild inativa não capitaliza; precisa aparecer para colher.
3. **Conquista herda o tributo pendente.** Castelo gordo é alvo mais valioso. Não zerar.

### Fato-chave do vanilla que define a Fase 1

A checagem de itens não-teleportáveis acontece no portal em que o jogador **entra**, não no de
saída — `TeleportWorld.cs:120`:

```csharp
if (!m_allowAllItems && !player.IsTeleportable())
```

Portanto **não adianta marcar o portal do castelo**: o jogador embarca na mina, e o portal da
mina é comum. A regra tem que olhar o *destino*, que o próprio `Teleport` resolve em
`TeleportWorld.cs:126`:

```csharp
ZDO zDO = ZDOMan.instance.GetZDO(m_nview.GetZDO().GetConnectionZDOID(ZDOExtraData.ConnectionType.Portal));
Vector3 position = zDO.GetPosition();
```

### Limite de segurança (aceito conscientemente)

`TeleportWorld.Teleport` roda **no cliente**. Cliente modificado burla. O vanilla já é
client-authoritative nesse ponto (`player.TeleportTo`), então não estamos abrindo buraco novo.
Não tentar "consertar" isso nesta implementação.

---

## Fase 0 — Tiers de território

Base das Fases 1–3. Tudo aditivo: `RaidDataWrapper` usa Newtonsoft, campo ausente vira default,
**não há migração de dados**.

### 0.1 `Models.cs`

Em `RaidZone`, adicionar duas propriedades:

```csharp
public int Tier { get; set; } = 1;
public int MinToolTier { get; set; } = 0;
```

Em `TerritoryInfo`, adicionar duas propriedades:

```csharp
public int PendingTribute { get; set; }
public long LastTributeUtc { get; set; }
```

### 0.2 `Util.cs` — novo formato de zona

Formato final da config `Raid Zones`:

```
nome,x,z,wardRadius,pvpRadius[,horasUtc[,tier[,minToolTier]]]
```

Exemplo:
```
DayCastle,12000,12000,150,300,10-22,3,2|Posto,500,300,80,150,*,1,0
```

Em `ParseZones`, dentro do `foreach`, **antes** do `zones.Add`:

```csharp
int tier = 1;
if (p.Length > 6 && int.TryParse(p[6].Trim(), out int tierValue))
    tier = Mathf.Max(1, tierValue);

int minToolTier = 0;
if (p.Length > 7 && int.TryParse(p[7].Trim(), out int toolValue))
    minToolTier = Mathf.Max(0, toolValue);
```

E no inicializador do objeto acrescentar `Tier = tier, MinToolTier = minToolTier,`.

> Config antiga (5 ou 6 campos) continua válida: vira `Tier = 1, MinToolTier = 0`.

### 0.3 `Util.cs` — helpers novos

```csharp
public static int GetTierAt(Vector3 pos) => GetRaidZoneAt(pos)?.Tier ?? 0;

/// <summary>Remove o sufixo "(Clone)" do nome de um GameObject instanciado.</summary>
public static string CleanPrefabName(string name)
{
    if (string.IsNullOrEmpty(name)) return string.Empty;
    int i = name.IndexOf("(Clone)", StringComparison.Ordinal);
    return i >= 0 ? name.Substring(0, i) : name;
}

/// <summary>
/// playerId do personagem de um peer conectado, ou 0.
/// Existe para o servidor NAO confiar no playerId que o cliente manda no pacote:
/// sem isso um cliente se passa por membro da guild dominante e resgata o tributo dela.
/// </summary>
public static long ResolvePlayerId(long peerId)
{
    if (ZNet.instance == null || ZDOMan.instance == null) return 0L;
    ZNetPeer peer = ZNet.instance.GetPeer(peerId);
    if (peer == null || peer.m_characterID.IsNone()) return 0L;
    ZDO zdo = ZDOMan.instance.GetZDO(peer.m_characterID);
    return zdo != null ? zdo.GetLong(ZDOVars.s_playerID, 0L) : 0L;
}
```

`ZNet.GetPeer(long)` existe em `ZNet.cs:1483`; `ZNetPeer.m_characterID` em `ZNetPeer.cs:19`.

> **Caso host local (não dedicado):** se `ResolvePlayerId` devolver 0 e `peerId ==
> ZRoutedRpc.instance.GetServerPeerID()`, usar `Player.m_localPlayer.GetPlayerID()`.

---

## Fase 1 — Portal de minério

### 1.1 Arquivo novo: `RaidSystem/OrePortal.cs`

```csharp
using System;
using HarmonyLib;
using UnityEngine;

namespace RaidSystem
{
    /// <summary>
    /// Portal de minerio: um portal aceita itens nao-teleportaveis quando o DESTINO dele e
    /// territorio dominado pela guild do jogador.
    ///
    /// A checagem do vanilla e no portal de ENTRADA (TeleportWorld.cs:120), nao no de saida.
    /// Por isso a regra olha o destino — e o unico jeito de "minerio entra no castelo"
    /// funcionar quando o jogador embarca la na mina.
    ///
    /// Efeito colateral desejado: minerio nunca SAI de territorio por portal, entao o tributo
    /// so viaja por terra. A regra "onde tem renda, nao tem portal de saida" vale por
    /// construcao, sem excecao no codigo.
    /// </summary>
    public static class OrePortal
    {
        public static bool AllowsOre(TeleportWorld portal, Player player, ZNetView nview)
        {
            try
            {
                if (portal == null || player == null) return false;
                if (RaidSystemPlugin.OrePortalEnabled.Value != Toggle.On) return false;

                ZDO self = nview != null ? nview.GetZDO() : null;
                if (self == null) return false;

                ZDOID targetId = self.GetConnectionZDOID(ZDOExtraData.ConnectionType.Portal);
                if (targetId == ZDOID.None || ZDOMan.instance == null) return false;

                ZDO destination = ZDOMan.instance.GetZDO(targetId);
                if (destination == null) return false;

                Vector3 destinationPos = destination.GetPosition();

                RaidZone zone = Util.GetRaidZoneAt(destinationPos);
                if (zone == null || zone.Tier < RaidSystemPlugin.OrePortalMinTier.Value)
                    return false;

                string owner = Util.GetTerritoryOwner(destinationPos);
                if (string.IsNullOrEmpty(owner)) return false;

                string team = GuildsIntegration.GetPlayerTeam(player);
                return !string.IsNullOrEmpty(team)
                       && string.Equals(team, owner, StringComparison.OrdinalIgnoreCase);
            }
            catch (Exception ex)
            {
                Debug.LogWarning("[RaidSystem] OrePortal check failed: " + ex.Message);
                return false;
            }
        }

        /// <summary>
        /// m_allowAllItems e campo de INSTANCIA do MonoBehaviour: mexer nele afeta so aquele
        /// portal na cena, nunca o prefab. Ligamos, deixamos o vanilla usar o portao dele, e
        /// devolvemos no Finalizer — Finalizer e nao Postfix porque o campo tem que voltar
        /// mesmo se o Teleport lancar excecao.
        /// </summary>
        [HarmonyPatch(typeof(TeleportWorld), nameof(TeleportWorld.Teleport))]
        public static class TeleportPatch
        {
            private static void Prefix(TeleportWorld __instance, Player player,
                                       ZNetView ___m_nview, out bool __state)
            {
                __state = __instance.m_allowAllItems;
                if (!__state && AllowsOre(__instance, player, ___m_nview))
                    __instance.m_allowAllItems = true;
            }

            private static void Finalizer(TeleportWorld __instance, bool __state)
                => __instance.m_allowAllItems = __state;
        }

        /// <summary>
        /// Mesmo truque no UpdatePortal, que le m_allowAllItems em TeleportWorld.cs:92 para
        /// acender o efeito. Sem isso o portal aceita minerio mas nao mostra que aceita.
        /// </summary>
        [HarmonyPatch(typeof(TeleportWorld), "UpdatePortal")]
        public static class UpdatePortalPatch
        {
            private static void Prefix(TeleportWorld __instance, ZNetView ___m_nview,
                                       out bool __state)
            {
                __state = __instance.m_allowAllItems;
                if (__state || __instance.m_proximityRoot == null) return;

                Player closest = Player.GetClosestPlayer(
                    __instance.m_proximityRoot.position, __instance.m_activationRange);
                if (closest != null && AllowsOre(__instance, closest, ___m_nview))
                    __instance.m_allowAllItems = true;
            }

            private static void Finalizer(TeleportWorld __instance, bool __state)
                => __instance.m_allowAllItems = __state;
        }

        [HarmonyPatch(typeof(TeleportWorld), nameof(TeleportWorld.GetHoverText))]
        public static class HoverTextPatch
        {
            private static void Postfix(TeleportWorld __instance, ZNetView ___m_nview,
                                        ref string __result)
            {
                Player lp = Player.m_localPlayer;
                if (lp == null) return;
                if (!AllowsOre(__instance, lp, ___m_nview)) return;
                __result += "\n<color=#FFD700>[Aceita minério]</color>";
            }
        }
    }
}
```

### 1.2 Notas de implementação da Fase 1

- `___m_nview` é injeção de campo privado do Harmony. Funciona mesmo sem assembly publicizada.
  **Não** trocar por `GetComponent<ZNetView>()` — é chamado a cada 0,5 s por portal.
- `Prefix` e `Finalizer` precisam estar na **mesma classe** para compartilhar `__state`.
- Não patchar `Player.IsTeleportable()`: é global e não sabe de qual portal se trata.

### 1.3 Registro

Nenhum. `_harmony.PatchAll()` no `Awake` já varre o assembly inteiro e pega as classes
aninhadas com `[HarmonyPatch]`.

---

## Fase 2 — Tributo

### 2.1 Por que não usar baú (não reverter esta decisão)

Num servidor dedicado o baú quase nunca está instanciado, e o inventário mora serializado em
`ZDOVars.s_items`. Escrever ali com o objeto descarregado é frágil.

**Modelo adotado:** o território acumula *cargas* (um `int` no `TerritoryInfo`), e um membro da
guild vai ao ward resgatar. 100% server-authoritative, sem container, e obriga a ir fisicamente
ao castelo e sair de lá carregado.

### 2.2 Arquivo novo: `RaidSystem/TributeManager.cs`

```csharp
using System;
using System.Collections.Generic;
using System.IO;
using Newtonsoft.Json;
using UnityEngine;

namespace RaidSystem
{
    [Serializable]
    public class TributeEntry
    {
        public string prefab;
        public int min;
        public int max;
    }

    public static class TributeManager
    {
        private static float _nextTick;
        private static Dictionary<int, List<TributeEntry>> _tables;

        private static string TablePath =>
            Path.Combine(RaidSystemPlugin.FileDirectory, "Tribute.json");

        // ---------- tabela ----------

        public static void LoadTables()
        {
            _tables = new Dictionary<int, List<TributeEntry>>();
            try
            {
                if (!Directory.Exists(RaidSystemPlugin.FileDirectory))
                    Directory.CreateDirectory(RaidSystemPlugin.FileDirectory);
                if (!File.Exists(TablePath)) WriteDefaultTable();

                var raw = JsonConvert.DeserializeObject<Dictionary<string, List<TributeEntry>>>(
                    File.ReadAllText(TablePath));
                if (raw == null) return;

                foreach (var kv in raw)
                    if (int.TryParse(kv.Key, out int tier) && kv.Value != null)
                        _tables[tier] = kv.Value;
            }
            catch (Exception ex)
            {
                Debug.LogError("[RaidSystem] Tribute.json invalido: " + ex.Message);
            }
        }

        /// <summary>Chamar depois que o ObjectDB existir. Nome errado no JSON tem que avisar.</summary>
        public static void ValidateTables()
        {
            if (_tables == null) LoadTables();
            if (ObjectDB.instance == null) return;

            foreach (var kv in _tables)
                foreach (TributeEntry e in kv.Value)
                {
                    if (string.IsNullOrEmpty(e.prefab)) continue;
                    if (ObjectDB.instance.GetItemPrefab(e.prefab) == null)
                        Debug.LogWarning($"[RaidSystem] Tribute.json: prefab '{e.prefab}' " +
                                         $"(tier {kv.Key}) nao existe no ObjectDB.");
                }
        }

        // ---------- acumulo ----------

        public static void Update()
        {
            if (ZNet.instance == null || !ZNet.instance.IsServer()) return;
            if (Time.time < _nextTick) return;
            _nextTick = Time.time + 60f;

            long now = DateTimeOffset.UtcNow.ToUnixTimeSeconds();
            long interval = Math.Max(1, RaidSystemPlugin.TributeIntervalMinutes.Value) * 60L;
            int cap = Math.Max(1, RaidSystemPlugin.TributeMaxCharges.Value);
            bool changed = false;

            DataStore.Modify(data =>
            {
                foreach (TerritoryInfo t in data.Territories)
                {
                    if (string.IsNullOrEmpty(t.OwnerTeamId)) continue;

                    if (t.LastTributeUtc == 0L) { t.LastTributeUtc = now; changed = true; continue; }

                    long due = (now - t.LastTributeUtc) / interval;
                    if (due <= 0) continue;

                    int before = t.PendingTribute;
                    t.PendingTribute = Mathf.Min(t.PendingTribute + (int)due, cap);
                    t.LastTributeUtc += due * interval;
                    if (t.PendingTribute != before) changed = true;
                }
            });

            if (changed) RPCManager.BroadcastFullSync();
        }

        // ---------- sorteio ----------

        /// <summary>Soma o sorteio de N cargas do tier. Chave = prefab, valor = quantidade.</summary>
        public static Dictionary<string, int> Roll(int tier, int charges)
        {
            var result = new Dictionary<string, int>();
            if (_tables == null) LoadTables();
            if (charges <= 0) return result;
            if (!_tables.TryGetValue(tier, out List<TributeEntry> entries) || entries == null)
            {
                Debug.LogWarning($"[RaidSystem] Sem tabela de tributo para o tier {tier}.");
                return result;
            }

            for (int c = 0; c < charges; c++)
                foreach (TributeEntry e in entries)
                {
                    if (string.IsNullOrEmpty(e.prefab)) continue;
                    int amount = UnityEngine.Random.Range(e.min, e.max + 1);
                    if (amount <= 0) continue;
                    result[e.prefab] = result.TryGetValue(e.prefab, out int cur)
                        ? cur + amount : amount;
                }

            return result;
        }

        // ---------- default ----------

        private static void WriteDefaultTable()
        {
            var def = new Dictionary<string, List<TributeEntry>>
            {
                ["1"] = new List<TributeEntry> {
                    new TributeEntry { prefab = "SurtlingCore", min = 1,  max = 2  },
                    new TributeEntry { prefab = "Coal",         min = 8,  max = 15 },
                    new TributeEntry { prefab = "Resin",        min = 10, max = 20 },
                },
                ["2"] = new List<TributeEntry> {
                    new TributeEntry { prefab = "IronScrap",    min = 5,  max = 10 },
                    new TributeEntry { prefab = "ElderBark",    min = 10, max = 20 },
                    new TributeEntry { prefab = "Guck",         min = 2,  max = 5  },
                },
                ["3"] = new List<TributeEntry> {
                    new TributeEntry { prefab = "SilverOre",    min = 4,  max = 8  },
                    new TributeEntry { prefab = "Obsidian",     min = 6,  max = 12 },
                    new TributeEntry { prefab = "FreezeGland",  min = 2,  max = 4  },
                },
                ["4"] = new List<TributeEntry> {
                    new TributeEntry { prefab = "BlackMetalScrap", min = 4, max = 8 },
                    new TributeEntry { prefab = "Tar",             min = 5, max = 10 },
                    new TributeEntry { prefab = "Needle",          min = 3, max = 6 },
                },
                ["5"] = new List<TributeEntry> {
                    new TributeEntry { prefab = "BlackCore",     min = 1, max = 2 },
                    new TributeEntry { prefab = "Sap",           min = 3, max = 6 },
                    new TributeEntry { prefab = "YggdrasilWood", min = 8, max = 15 },
                    new TributeEntry { prefab = "Carapace",      min = 4, max = 8 },
                },
                ["6"] = new List<TributeEntry> {
                    new TributeEntry { prefab = "FlametalOre",  min = 3, max = 6 },
                    new TributeEntry { prefab = "CharredBone",  min = 5, max = 10 },
                    new TributeEntry { prefab = "AskHide",      min = 2, max = 5 },
                    new TributeEntry { prefab = "MorgenSinew",  min = 1, max = 3 },
                },
            };

            File.WriteAllText(TablePath,
                JsonConvert.SerializeObject(def, Formatting.Indented),
                System.Text.Encoding.UTF8);
            Debug.Log("[RaidSystem] Tribute.json default escrito em " + TablePath);
        }
    }
}
```

> **Nomes de prefab acima estão verificados** contra a lista do Jotunn
> (`valheim-modding.github.io/Jotunn/data/objects/item-list.html`). O único de capitalização
> duvidosa é `SoftTissue` — por isso ficou fora do default. Se for adicionar, confirmar no log
> do `ValidateTables` antes.

### 2.3 RPCs — `RPCManager.cs`

Adicionar os dois métodos e registrar em `GameStartPatch.Postfix`:

```csharp
ZRoutedRpc.instance.Register<ZPackage>("RaidSystem_ClaimTribute",  new Action<long, ZPackage>(RPC_ClaimTribute));
ZRoutedRpc.instance.Register<ZPackage>("RaidSystem_GrantTribute",  new Action<long, ZPackage>(RPC_GrantTribute));
```

E, na mesma `Postfix`, depois dos registros:

```csharp
TributeManager.LoadTables();
TributeManager.ValidateTables();
```

#### Servidor

```csharp
/// <summary>
/// Resgate de tributo. O playerId vem no pacote mas NAO e confiado: o servidor confere que
/// o peer que mandou e mesmo o dono daquele personagem (Util.ResolvePlayerId). Sem isso um
/// cliente se passa por membro da guild dominante e saca o tributo dela.
/// </summary>
public static void RPC_ClaimTribute(long sender, ZPackage pkg)
{
    if (!ZNet.instance.IsServer()) return;

    Vector3 pos = pkg.ReadVector3();

    long playerId = Util.ResolvePlayerId(sender);
    if (playerId == 0L && sender == ZRoutedRpc.instance.GetServerPeerID() && Player.m_localPlayer != null)
        playerId = Player.m_localPlayer.GetPlayerID();
    if (playerId == 0L) return;

    string team = GuildsIntegration.GetPlayerTeam(playerId);
    if (string.IsNullOrEmpty(team)) return;

    RaidZone zone = Util.GetRaidZoneAt(pos);
    if (zone == null) return;

    TerritoryInfo territory = Util.GetTerritoryAt(pos);
    if (territory == null
        || string.IsNullOrEmpty(territory.OwnerTeamId)
        || !string.Equals(territory.OwnerTeamId, team, StringComparison.OrdinalIgnoreCase))
        return;

    int charges = territory.PendingTribute;
    if (charges <= 0) return;

    Dictionary<string, int> loot = TributeManager.Roll(zone.Tier, charges);
    if (loot.Count == 0) return;

    DataStore.Modify(_ => { territory.PendingTribute = 0; });

    ZPackage response = new ZPackage();
    response.Write(loot.Count);
    foreach (var kv in loot) { response.Write(kv.Key); response.Write(kv.Value); }
    ZRoutedRpc.instance.InvokeRoutedRPC(sender, "RaidSystem_GrantTribute", response);

    dWebHook.SendRaidMessage(
        $"**[Tributo]** **{team}** resgatou **{charges}** carga(s) em **{zone.Name}**.");

    BroadcastFullSync();
}
```

#### Cliente

```csharp
public static void RPC_GrantTribute(long sender, ZPackage pkg)
{
    Player lp = Player.m_localPlayer;
    if (lp == null) return;

    int count = pkg.ReadInt();
    var received = new List<string>();

    for (int i = 0; i < count; i++)
    {
        string prefabName = pkg.ReadString();
        int amount = pkg.ReadInt();

        GameObject prefab = ObjectDB.instance != null
            ? ObjectDB.instance.GetItemPrefab(prefabName) : null;
        if (prefab == null) continue;

        // O que nao couber cai no chao, nunca some.
        if (lp.GetInventory().CanAddItem(prefab, amount))
            lp.GetInventory().AddItem(prefab, amount);
        else
            DropOnGround(lp, prefab, amount);

        received.Add($"{amount}x {prefabName}");
    }

    lp.Message(MessageHud.MessageType.Center,
        received.Count > 0 ? "Tributo: " + string.Join(", ", received) : "Nada a resgatar.");
}

private static void DropOnGround(Player player, GameObject prefab, int amount)
{
    Vector3 pos = player.transform.position + player.transform.forward * 1.5f + Vector3.up;
    GameObject go = UnityEngine.Object.Instantiate(prefab, pos, Quaternion.identity);
    ItemDrop drop = go.GetComponent<ItemDrop>();
    if (drop != null && drop.m_itemData != null)
    {
        drop.m_itemData.m_stack = Mathf.Min(amount, drop.m_itemData.m_shared.m_maxStackSize);
        drop.Save();
    }
}
```

> `Inventory.CanAddItem(GameObject, int)` está em `Inventory.cs:69`;
> `Inventory.AddItem(GameObject, int)` em `Inventory.cs:88`.

### 2.4 Interação com o ward — `Patches.cs`

O módulo de Wards do Deadheim ignora o prefab `RaidWard` (o `WardProfile.For` devolve `null`
para ele), então o RaidSystem pode patchar `PrivateArea.Interact` sem conflito. **Guardar pelo
nome do prefab logo na primeira linha** — sem isso o patch pegaria todo `guard_stone` do mundo.

```csharp
/// <summary>
/// RaidWard nao abre o menu de permissoes do guard_stone: interagir com ele resgata tributo.
/// O guard por nome de prefab e obrigatorio — sem ele isso valeria para toda ward do servidor.
/// </summary>
[HarmonyPatch(typeof(PrivateArea), nameof(PrivateArea.Interact))]
public static class RaidWardInteractPatch
{
    private static bool Prefix(PrivateArea __instance, Humanoid human, bool hold, bool alt)
    {
        if (hold) return true;
        if (Util.CleanPrefabName(__instance.gameObject.name) != "RaidWard") return true;

        Player player = human as Player;
        if (player == null) return true;

        int free = player.GetInventory().GetEmptySlots();
        if (free < RaidSystemPlugin.TributeRequiredFreeSlots.Value)
        {
            player.Message(MessageHud.MessageType.Center,
                $"Libere pelo menos {RaidSystemPlugin.TributeRequiredFreeSlots.Value} espaços na mochila.");
            return false;
        }

        ZPackage pkg = new ZPackage();
        pkg.Write(__instance.transform.position);
        ZRoutedRpc.instance.InvokeRoutedRPC(
            ZRoutedRpc.instance.GetServerPeerID(), "RaidSystem_ClaimTribute", pkg);
        return false;
    }
}
```

E mostrar o pendente no hover (`PrivateArea.GetHoverText`, mesmo guard por nome):

```csharp
[HarmonyPatch(typeof(PrivateArea), nameof(PrivateArea.GetHoverText))]
public static class RaidWardHoverPatch
{
    private static void Postfix(PrivateArea __instance, ref string __result)
    {
        if (Util.CleanPrefabName(__instance.gameObject.name) != "RaidWard") return;

        Vector3 pos = __instance.transform.position;
        TerritoryInfo t = Util.GetTerritoryAt(pos);
        string owner = string.IsNullOrEmpty(t?.OwnerTeamId) ? "sem dono" : t.OwnerTeamId;
        int pending = t?.PendingTribute ?? 0;

        __result = $"Raid Ward\nDomínio: {owner}\nTributo pendente: {pending}\n" +
                   "[<color=yellow><b>$KEY_Use</b></color>] resgatar";
    }
}
```

### 2.5 Conquista herda o pendente — `RPCManager.HandleConquest`

**Não zerar `PendingTribute`.** Dentro do `DataStore.Modify` já existente, capturar o valor
para a mensagem e deixá-lo intacto:

```csharp
int inherited = territory.PendingTribute;   // herdado de proposito: castelo gordo vale mais
```

E na mensagem do webhook:

```csharp
string webhookMessage = $"**[Conquista]** **{teamId}** conquistou **{zoneName}** com **{nick}**";
if (inherited > 0) webhookMessage += $" e herdou **{inherited}** carga(s) de tributo";
webhookMessage += ".\n";
```

> `inherited` precisa ser declarada **fora** da lambda para ser lida depois.

### 2.6 Tick — `RaidSystemPlugin.Update()`

Na primeira linha do `Update`, ao lado do `RaidDoorManager.Update()`:

```csharp
TributeManager.Update();
```

---

## Fase 3 — Cerco

### 3.1 O achado que torna isto viável

`HitData.m_hitType` é um `byte` **serializado incondicionalmente** (`HitData.cs:851`), e o
catapult marca os próprios tiros (`Catapult.cs:474`):

```csharp
hitData.m_hitType = HitData.HitType.Catapult;
```

Então `hit.m_hitType == HitData.HitType.Catapult` é uma checagem confiável dentro de
`WearNTear.RPC_Damage`, mesmo quando o RPC chega pela rede. `m_toolTier` (`short`) também é
serializado sempre (`HitData.cs:832`).

### 3.2 Armadilha: tiro de catapulta não tem atacante

`Catapult.cs:487` chama `m_lastProjectile.Setup(null, ...)` — **owner nulo**. Logo
`hit.GetAttacker()` é `null` num acerto de catapulta, e a atribuição de conquista que hoje
depende do atacante **não funciona** para quem derruba o ward só com cerco.

Correção em duas partes:

**(a) Carimbar quem atirou.** `Catapult.Shoot()` (`Catapult.cs:372`) roda no cliente do
operador — ele chama `InvokeRPC(Everybody, "RPC_Shoot")`. Postfix ali:

```csharp
/// <summary>
/// Catapult.ShootProjectile faz Setup(null, ...): o projetil nao tem dono, entao um ward
/// derrubado por catapulta chegaria em RPC_Damage sem atacante e ninguem conquistaria.
/// Shoot() roda no cliente de quem operou, entao aqui sabemos quem foi.
/// </summary>
[HarmonyPatch(typeof(Catapult), "Shoot")]
public static class CatapultShootPatch
{
    private static void Postfix(Catapult __instance)
    {
        Player lp = Player.m_localPlayer;
        if (lp == null) return;
        ZNetView nview = __instance.GetComponent<ZNetView>();
        if (nview == null || !nview.IsValid() || !nview.IsOwner()) return;
        nview.GetZDO().Set("rs_shooter", lp.GetPlayerID());
    }
}
```

**(b) Resolver na hora do dano.** Em `Patches.cs`, no ponto onde o atacante é registrado em
`_wardAttackers`, quando não houver atacante e o tipo for `Catapult`:

```csharp
/// <summary>Catapulta mais proxima do ponto de impacto, para atribuir o tiro sem dono.</summary>
private static long ResolveSiegeShooter(Vector3 point)
{
    float best = RaidSystemPlugin.SiegeAttributionRadius.Value;
    long shooter = 0L;
    foreach (Catapult c in UnityEngine.Object.FindObjectsByType<Catapult>(FindObjectsSortMode.None))
    {
        if (c == null) continue;
        float d = Vector3.Distance(c.transform.position, point);
        if (d > best) continue;
        ZDO zdo = c.GetComponent<ZNetView>()?.GetZDO();
        long candidate = zdo != null ? zdo.GetLong("rs_shooter", 0L) : 0L;
        if (candidate == 0L) continue;
        best = d; shooter = candidate;
    }
    return shooter;
}
```

### 3.3 Dano do ward — `Patches.cs`, dentro de `RPCDamagePatch.Prefix`

Substituir a linha única de hoje (`Patches.cs:162`):

```csharp
hit.ApplyModifier(1f - RaidSystemPlugin.WardReductionDamage.Value / 100f);
```

por:

```csharp
bool siege = hit.m_hitType == HitData.HitType.Catapult;

if (RaidSystemPlugin.LogWardHits.Value == Toggle.On)
    Debug.Log($"[RaidSystem] Ward hit: hitType={hit.m_hitType} toolTier={hit.m_toolTier} " +
              $"dano={hit.GetTotalDamage():F1} siege={siege}");

if (RaidSystemPlugin.SiegeOnly.Value == Toggle.On && !siege)
    return false;   // ward so cai por cerco

RaidZone zone = Util.GetRaidZoneAt(pos);
if (!siege && zone != null && hit.m_toolTier < zone.MinToolTier)
    return false;   // ferramenta fraca demais para este territorio

float reduction = siege
    ? RaidSystemPlugin.WardReductionDamageSiege.Value
    : RaidSystemPlugin.WardReductionDamage.Value;

hit.ApplyModifier(1f - reduction / 100f);
```

### 3.4 Calibração (fazer no servidor, não chutar)

Os valores de `m_toolTier` por arma **não estão no código decompilado** — vêm dos assets. Por
isso existe `Log Ward Hits`: ligar, bater no ward com cada arma que interessa, ler os
`toolTier=` no log do servidor, e só então preencher o `minToolTier` das zonas.

**Ligar `Siege Only` só depois disso**, e com `Ward HP` reduzido (sugestão inicial: 1500) —
senão a primeira raid vira uma sessão de duas horas de catapulta.

---

## Config completa a adicionar em `RaidSystemPlugin.BindConfigs()`

```csharp
OrePortalEnabled = config("10 - Economia", "Ore Portal Enabled", Toggle.On,
    "Portais aceitam minério quando o destino é território da sua guild.");
OrePortalMinTier = config("10 - Economia", "Ore Portal Min Tier", 1,
    "Tier mínimo de território para conceder portal de minério.");
TributeIntervalMinutes = config("10 - Economia", "Tribute Interval Minutes", 120,
    "Minutos por carga de tributo.");
TributeMaxCharges = config("10 - Economia", "Tribute Max Charges", 6,
    "Teto de cargas acumuladas. Guild inativa para de render.");
TributeRequiredFreeSlots = config("10 - Economia", "Tribute Required Free Slots", 6,
    "Espaços livres exigidos na mochila para resgatar.");

WardReductionDamageSiege = config("2 - Raid Rules", "Ward Damage Reduction % (Siege)", 0f,
    "Redução de dano aplicada a acertos de catapulta.");
SiegeOnly = config("2 - Raid Rules", "Siege Only", Toggle.Off,
    "Ward só recebe dano de cerco. Ligar apenas após calibrar.");
LogWardHits = config("2 - Raid Rules", "Log Ward Hits", Toggle.Off,
    "Loga hitType e toolTier de cada acerto no ward. Use para calibrar minToolTier.");
SiegeAttributionRadius = config("2 - Raid Rules", "Siege Attribution Radius", 200f,
    "Raio de busca da catapulta que atribui o tiro sem dono.");
```

Declarar os `ConfigEntry<>` estáticos correspondentes no topo da classe. **Todos sincronizados**
(padrão `sync = true`) — são regras de servidor. `Tribute.json` **não** entra no ServerSync.

Atualizar também `WriteDefaultConfigExample()` com as mesmas chaves, e a descrição de
`Raid Zones` para o formato de 8 campos.

---

## Ordem de implementação e build

1. Fase 0 — compila, sem efeito visível
2. Fase 1 — testável sozinha
3. Fase 2 — depende da 0
4. Fase 3 — depende da 0; deixar `Siege Only = Off` no primeiro deploy

**Deadheim precisa ser construído antes do RaidSystem** (`RaidSystem.csproj` referencia
`bin\Release\Deadheim.dll`):

```bash
"D:/Visual Studio/MSBuild/Current/Bin/MSBuild.exe" Deadheim.csproj /t:Build /p:Configuration=Release
```

```bash
"D:/Visual Studio/MSBuild/Current/Bin/MSBuild.exe" RaidSystem.csproj /t:Build /p:Configuration=Release
```

Usar `/t:Build` e **não** `/t:Compile` — `Compile` só escreve em `obj\` e o ILRepack não roda.
Se aparecer `Failed to resolve assembly: 'BepInEx, Version=5.4.23.3'`, conferir
`<ILRepackTargetsFile>` no csproj antes de mexer em qualquer referência.

---

## Checklist de teste

**Fase 1**
- [ ] Portal comum → portal comum, carregando minério: bloqueia (comportamento vanilla intacto)
- [ ] Portal na mina → portal em território **da sua guild**: passa minério, hover mostra `[Aceita minério]`
- [ ] Portal no castelo → portal na sua base: **bloqueia** (a regra "só entra")
- [ ] Território de outra guild como destino: bloqueia
- [ ] Território sem dono como destino: bloqueia
- [ ] `Ore Portal Enabled = Off`: tudo volta ao vanilla

**Fase 2**
- [ ] `Tribute.json` é criado no primeiro boot
- [ ] Prefab inválido no JSON gera warning no log, não crash
- [ ] Cargas sobem após o intervalo e param no teto
- [ ] Resgate por membro da guild dominante entrega os itens
- [ ] Resgate por não-membro: nada acontece
- [ ] Mochila cheia: mensagem de bloqueio; excedente cai no chão
- [ ] Conquista com tributo pendente: novo dono herda, Discord informa o número

**Fase 3**
- [ ] `Log Ward Hits = On` registra `toolTier` de cada arma
- [ ] Acerto de catapulta aparece como `hitType=Catapult`
- [ ] Ward derrubado só com catapulta atribui a conquista corretamente
- [ ] `Siege Only = On` faz golpe de mão não causar dano nenhum

---

## Armadilhas conhecidas

| Risco | Como evitar |
|---|---|
| Patchar `PrivateArea` sem guard de prefab | Toda ward do servidor vira caixa de tributo. **Sempre** checar `CleanPrefabName == "RaidWard"` na primeira linha |
| Usar `Postfix` no lugar de `Finalizer` no portal | Se `Teleport` lançar, `m_allowAllItems` fica ligado para sempre naquele portal |
| Confiar no `playerId` do pacote | Cliente saca tributo de guild alheia. Usar `Util.ResolvePlayerId(sender)` |
| Zerar `PendingTribute` na conquista | Mata a melhor parte do desenho |
| `/t:Compile` no lugar de `/t:Build` | ILRepack não roda, DLL sai sem as dependências |
| Ligar `Siege Only` antes de calibrar | Ward fica praticamente indestrutível |
