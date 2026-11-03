using BepInEx.Configuration;
using HarmonyLib;
using System;
using UnityEngine;

namespace Deadheim
{
    /// <summary>
    /// Aesir 33: lapide personalizada (03/11/2026). Quando um Aesir morre, a tumba que ele deixa
    /// ganha uma cruz dourada giratoria e um brilho suave, visiveis a todos. So visual: a tumba
    /// continua com o mesmo inventario, a mesma recuperacao e o mesmo colisor. O cliente do dono
    /// marca a ZDO da tumba ("dh_lapide") ao criar; qualquer cliente que ve a tumba desenha o enfeite.
    /// O enfeite nao tem colisor e a luz tem alcance pequeno (nao serve de farol nem de armadilha).
    /// </summary>
    [HarmonyPatch]
    internal static class AesirLapide
    {
        private const string ZdoLapide = "dh_lapide";
        private const string NomeEnfeite = "DH_LapideAesir";

        public static ConfigEntry<bool> Ativo;

        public static void Bind(ConfigFile config)
        {
            Ativo = Plugin.Synced(config.Bind("Aesir cosmeticos", "Aesir33LapideAtivo", true,
                "Aesir 33: a tumba de um Aesir ganha uma cruz dourada e um brilho (so visual)."));
        }

        [HarmonyPatch(typeof(TombStone), nameof(TombStone.Setup))]
        [HarmonyPostfix]
        private static void Setup(TombStone __instance, long ownerUID)
        {
            try
            {
                Player local = Player.m_localPlayer;
                if (local == null || ownerUID != local.GetPlayerID() || Ativo == null || !Ativo.Value) return;
                if (!VipList.VipListApi.IsLocalPlayerVip()) return;
                ZNetView nview = __instance.GetComponent<ZNetView>();
                if (nview == null || !nview.IsValid() || !nview.IsOwner()) return;
                nview.GetZDO().Set(ZdoLapide, true);
            }
            catch (Exception e) { Debug.LogError("[Deadheim] Aesir 33 (lapide): " + e.Message); }
        }

        [HarmonyPatch(typeof(TombStone), "Start")]
        [HarmonyPostfix]
        private static void Start(TombStone __instance)
        {
            try
            {
                ZNetView nview = __instance.GetComponent<ZNetView>();
                if (nview == null || !nview.IsValid() || !nview.GetZDO().GetBool(ZdoLapide, false)) return;
                if (__instance.transform.Find(NomeEnfeite) != null) return;
                Enfeitar(__instance);
            }
            catch (Exception e) { Debug.LogError("[Deadheim] Aesir 33 (lapide): " + e.Message); }
        }

        private static void Enfeitar(TombStone tumba)
        {
            var raiz = new GameObject(NomeEnfeite);
            raiz.transform.SetParent(tumba.transform, false);
            raiz.transform.localPosition = new Vector3(0f, 1.9f, 0f);

            // Cruz de dois blocos, com o material (shader) de uma peca da propria tumba, so mais dourado.
            Material baseMat = null;
            var renderer = tumba.GetComponentInChildren<MeshRenderer>();
            if (renderer != null && renderer.sharedMaterial != null) baseMat = renderer.sharedMaterial;
            var ouro = new Color(1f, 0.78f, 0.24f, 1f);
            Material mat = null;
            if (baseMat != null)
            {
                mat = new Material(baseMat);
                mat.color = ouro;
                if (mat.HasProperty("_EmissionColor")) { mat.EnableKeyword("_EMISSION"); mat.SetColor("_EmissionColor", ouro * 0.8f); }
            }
            Bloco(raiz.transform, new Vector3(0f, 0f, 0f), new Vector3(0.12f, 0.8f, 0.12f), mat);
            Bloco(raiz.transform, new Vector3(0f, 0.2f, 0f), new Vector3(0.5f, 0.12f, 0.12f), mat);
            raiz.AddComponent<Girar>();

            var luzGo = new GameObject("Brilho");
            luzGo.transform.SetParent(raiz.transform, false);
            var luz = luzGo.AddComponent<Light>();
            luz.type = LightType.Point;
            luz.color = ouro;
            luz.range = 4f;
            luz.intensity = 0.8f;
            luz.shadows = LightShadows.None;
        }

        private static void Bloco(Transform pai, Vector3 pos, Vector3 escala, Material mat)
        {
            GameObject cubo = GameObject.CreatePrimitive(PrimitiveType.Cube);
            cubo.name = "Bloco";
            UnityEngine.Object.Destroy(cubo.GetComponent<Collider>());   // sem colisor: so enfeite
            cubo.transform.SetParent(pai, false);
            cubo.transform.localPosition = pos;
            cubo.transform.localScale = escala;
            var mr = cubo.GetComponent<MeshRenderer>();
            if (mat != null) mr.sharedMaterial = mat;
            mr.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            mr.receiveShadows = false;
        }

        private sealed class Girar : MonoBehaviour
        {
            private void Update() => transform.Rotate(0f, 40f * Time.deltaTime, 0f, Space.World);
        }
    }
}
