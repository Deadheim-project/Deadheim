using HarmonyLib;
using Jotunn.Managers;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;

namespace RaidSystem
{
    [HarmonyPatch]
    public static class WardSetup
    {
        public static GameObject RaidWardPrefab { get; private set; }
        private static bool _registeredInHammer;

        public static void LoadAssets()
        {
            Jotunn.Managers.PrefabManager.OnPrefabsRegistered += AddRaidWardPrefab;
            Jotunn.Managers.PieceManager.OnPiecesRegistered += RegisterRaidWardPiece;
        }

        private static void AddRaidWardPrefab()
        {
            if (RaidWardPrefab != null) return;

            GameObject originalWard = Jotunn.Managers.PrefabManager.Instance.GetPrefab("guard_stone");
            RaidWardPrefab = Jotunn.Managers.PrefabManager.Instance.CreateClonedPrefab("RaidWard", "guard_stone");
            if (RaidWardPrefab == null)
            {
                Debug.LogError("[RaidSystem] guard_stone not found for RaidWard clone!");
                return;
            }

            float scale = RaidSystemPlugin.Scale.Value;
            RaidWardPrefab.transform.localScale = Vector3.one * scale;

            var wnt = RaidWardPrefab.GetComponent<WearNTear>();
            if (wnt != null) { wnt.m_health = RaidSystemPlugin.HitPoints.Value; wnt.m_noRoofWear = false; wnt.m_noSupportWear = false; }

            var piece = RaidWardPrefab.GetComponent<Piece>();
            if (piece != null)
            {
                Piece originalPiece = originalWard != null ? originalWard.GetComponent<Piece>() : null;
                piece.m_name = "Raid Ward"; piece.m_description = "Territorial ward for faction warfare."; piece.m_comfort = 0;
                if (piece.m_icon == null && originalPiece != null) piece.m_icon = originalPiece.m_icon;
                ConfigureRequirements(piece);
            }

            var area = RaidWardPrefab.GetComponent<PrivateArea>();
            if (area != null)
            {
                area.m_radius = RaidSystemPlugin.AreaRadius.Value;
                area.m_name = "RaidWard";
            }

            TintVisuals(RaidWardPrefab);
            AddGlow(RaidWardPrefab);
            RemoveServerUnsafeVisualComponents(RaidWardPrefab);

            Jotunn.Managers.PrefabManager.OnPrefabsRegistered -= AddRaidWardPrefab;
            Debug.Log($"[RaidSystem] RaidWard created: scale={scale}, HP={RaidSystemPlugin.HitPoints.Value}");
        }

        private static void RegisterRaidWardPiece()
        {
            if (_registeredInHammer) return;
            if (RaidWardPrefab == null) AddRaidWardPrefab();
            if (RaidWardPrefab == null) return;

            Jotunn.Managers.PieceManager.Instance.RegisterPieceInPieceTable(RaidWardPrefab, "Hammer", "Misc");
            _registeredInHammer = true;
            Jotunn.Managers.PieceManager.OnPiecesRegistered -= RegisterRaidWardPiece;
            Debug.Log("[RaidSystem] RaidWard added to hammer using Jotunn PieceManager.");
        }

        /// <summary>
        /// O vanilla percorre m_namedPrefabs e estoura NullReference se algum valor foi
        /// destruido. Antes este patch substituia o metodo inteiro e ainda lia m_prefabs,
        /// que e outra colecao: o resultado devolvido nao era o mesmo do jogo. Agora so
        /// limpa as entradas mortas e deixa o vanilla rodar.
        /// </summary>
        [HarmonyPatch(typeof(ZNetScene), "GetPrefabNames")][HarmonyPrefix][HarmonyPriority(Priority.Last)]
        private static void ZNetScene_GetPrefabNames(ZNetScene __instance)
        {
            List<int> dead = null;
            foreach (KeyValuePair<int, GameObject> entry in __instance.m_namedPrefabs)
            {
                if (entry.Value != null) continue;
                (dead ??= new List<int>()).Add(entry.Key);
            }

            if (dead == null) return;
            foreach (int key in dead) __instance.m_namedPrefabs.Remove(key);
            __instance.m_prefabs.RemoveAll(prefab => prefab == null);
        }

        [HarmonyPatch(typeof(Player), "UpdateKnownRecipesList")][HarmonyPrefix]
        private static void Player_UpdateKnownRecipesList()
        {
            CleanPieceTables();
            ApplyAdminOnlyVisibility();
        }

        /// <summary>
        /// Tira a RaidWard do martelo de quem nao e admin. Barrar so no PlacePiece funciona,
        /// mas deixa a peca visivel e o jogador gastando tempo com ela.
        /// </summary>
        private static void ApplyAdminOnlyVisibility()
        {
            if (RaidWardPrefab == null) return;
            if (RaidSystemPlugin.WardOnlyAdminCanBuild.Value != Toggle.On) return;
            if (SynchronizationManager.Instance == null) return;

            bool isAdmin = SynchronizationManager.Instance.PlayerIsAdmin;

            foreach (PieceTable table in Resources.FindObjectsOfTypeAll<PieceTable>())
            {
                if (table?.m_pieces == null) continue;

                bool listed = table.m_pieces.Contains(RaidWardPrefab);
                if (isAdmin && !listed) table.m_pieces.Add(RaidWardPrefab);
                else if (!isAdmin && listed) table.m_pieces.Remove(RaidWardPrefab);
            }
        }

        /// <summary>
        /// FindObjectsOfTypeAll varre todos os objetos carregados e e caro. Isto roda a cada
        /// UpdateKnownRecipesList, que dispara a cada mudanca de inventario, entao vai por
        /// intervalo: pecas nulas so aparecem quando um prefab e registrado ou destruido.
        /// </summary>
        private static float _nextPieceTableClean;

        private static void CleanPieceTables()
        {
            if (Time.time < _nextPieceTableClean) return;
            _nextPieceTableClean = Time.time + 30f;

            foreach (PieceTable table in Resources.FindObjectsOfTypeAll<PieceTable>())
                table?.m_pieces?.RemoveAll(piece => piece == null || !piece);
        }

        private static void ConfigureRequirements(Piece piece)
        {
            ItemDrop stone = GetItemDrop("Stone");
            ItemDrop core = GetItemDrop("SurtlingCore");
            var requirements = new List<Piece.Requirement>();

            if (stone != null)
                requirements.Add(new Piece.Requirement { m_resItem = stone, m_amount = 1000, m_recover = false });
            if (core != null)
                requirements.Add(new Piece.Requirement { m_resItem = core, m_amount = 10, m_recover = false });

            if (requirements.Count > 0)
            {
                piece.m_resources = requirements.ToArray();
                return;
            }

            piece.m_resources = piece.m_resources?
                .Where(requirement => requirement != null && requirement.m_resItem != null)
                .ToArray() ?? new Piece.Requirement[0];
        }

        private static void TintVisuals(GameObject ward)
        {
            Color warColor = new(0.9f, 0.2f, 0.15f, 0.4f);
            Color emission = new(0.8f, 0.1f, 0.1f, 1f);
            foreach (var r in ward.GetComponentsInChildren<MeshRenderer>(true))
            {
                if (!r.gameObject.name.Contains("shield") && !r.gameObject.name.Contains("Shield") && !r.gameObject.name.Contains("dome") && !r.gameObject.name.Contains("AreaMarker")) continue;
                foreach (var mat in r.sharedMaterials)
                {
                    if (mat == null) continue;
                    var m = new Material(mat);
                    if (m.HasProperty("_Color")) m.SetColor("_Color", warColor);
                    if (m.HasProperty("_EmissionColor")) { m.SetColor("_EmissionColor", emission); m.EnableKeyword("_EMISSION"); }
                    r.material = m;
                }
            }
        }

        private static void AddGlow(GameObject ward)
        {
            var existing = ward.GetComponentInChildren<Light>(true);
            if (existing != null) { existing.color = new Color(0.9f, 0.2f, 0.1f); existing.intensity = 2f; existing.range = 15f; return; }
            var go = new GameObject("RaidWardGlow"); go.transform.SetParent(ward.transform); go.transform.localPosition = new Vector3(0, 2f, 0);
            var l = go.AddComponent<Light>(); l.type = LightType.Point; l.color = new Color(0.9f, 0.2f, 0.1f); l.intensity = 1.5f; l.range = 12f; l.shadows = LightShadows.None;
        }

        private static void RemoveServerUnsafeVisualComponents(GameObject ward)
        {
            foreach (var behaviour in ward.GetComponentsInChildren<MonoBehaviour>(true))
            {
                if (behaviour == null) continue;
                if (behaviour.GetType().Name != "ShieldDomeImageEffect") continue;

                UnityEngine.Object.DestroyImmediate(behaviour);
                Debug.Log("[RaidSystem] Removed ShieldDomeImageEffect from RaidWard prefab.");
            }
        }

        private static ItemDrop GetItemDrop(string name)
        {
            GameObject prefab = Jotunn.Managers.PrefabManager.Instance.GetPrefab(name);
            return prefab != null ? prefab.GetComponent<ItemDrop>() : null;
        }
    }
}
