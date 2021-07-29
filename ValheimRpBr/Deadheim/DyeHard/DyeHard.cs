using BepInEx;
using HarmonyLib;
using UnityEngine;

namespace Deadheim.DyeHard
{
    [BepInPlugin(PluginGUID, PluginName, PluginVersion)]
    public class DyeHard : BaseUnityPlugin
    {
        public const string PluginGUID = "redseiko.valheim.dyehard";
        public const string PluginName = "DyeHard";
        public const string PluginVersion = "1.0.0";

        private static readonly int _hairColorHashCode = "HairColor".GetStableHashCode();

        private static Color _playerHairColor = Color.white;
        private static float _playerHairGlow = 1f;
        private static bool _isModEnabled = true;
        private static Player _localPlayer;

        public static void UpdatePlayerHairColorValue(string colorString)
        {
            if (!Plugin.playerIsVip)
            {
                Player.m_localPlayer.Message(MessageHud.MessageType.Center, "Apenas vips podem pintar seus cabelos.");
                return;
            }

            if (ColorUtility.TryParseHtmlString(colorString, out Color color))
            {
                color.a = 1f;
                _playerHairColor = color;

                SetPlayerZdoHairColor();
            }
        }

        private static Vector3 GetPlayerHairColorVector()
        {
            Vector3 colorVector = Utils.ColorToVec3(_playerHairColor);

            if (colorVector != Vector3.zero)
            {
                colorVector *= _playerHairGlow / colorVector.magnitude;
            }

            return colorVector;
        }

        [HarmonyPatch(typeof(FejdStartup))]
        private class FejdStartupPatch
        {
            [HarmonyPostfix]
            [HarmonyPatch(nameof(FejdStartup.SetupCharacterPreview))]
            private static void FejdStartupSetupCharacterPreviewPostfix(ref FejdStartup __instance)
            {
                _localPlayer = __instance.m_playerInstance.GetComponent<Player>();
                SetPlayerZdoHairColor();
            }
        }

        [HarmonyPatch(typeof(VisEquipment))]
        private class VisEquipmentPatch
        {
            [HarmonyPrefix]
            [HarmonyPatch(nameof(VisEquipment.SetHairColor))]
            private static void VisEquipmentSetHairColorPrefix(ref VisEquipment __instance, ref Vector3 color)
            {
                if (_isModEnabled && __instance.TryGetComponent(out Player player) && player == _localPlayer)
                {
                    color = GetPlayerHairColorVector();
                }
            }
        }

        [HarmonyPatch(typeof(Player))]
        private class PlayerPatch
        {
            [HarmonyPostfix]
            [HarmonyPatch(nameof(Player.SetLocalPlayer))]
            private static void PlayerSetLocalPlayerPostfix(ref Player __instance)
            {
                _localPlayer = __instance;
                SetPlayerZdoHairColor();
            }

            [HarmonyPostfix]
            [HarmonyPatch(nameof(Player.OnSpawned))]
            private static void PlayerOnSpawnedPostfix(ref Player __instance)
            {
                _localPlayer = __instance;
                SetPlayerZdoHairColor();
            }
        }

        private static void SetPlayerZdoHairColor()
        {
            if (!_localPlayer || !_localPlayer.m_visEquipment)
            {
                return;
            }

            Vector3 color = _isModEnabled ? GetPlayerHairColorVector() : _localPlayer.m_hairColor;
            _localPlayer.m_visEquipment.m_hairColor = color;

            if (!_localPlayer.m_nview || !_localPlayer.m_nview.IsValid())
            {
                return;
            }

            if (_localPlayer.m_nview.m_zdo.m_vec3 == null
                || !_localPlayer.m_nview.m_zdo.m_vec3.ContainsKey(_hairColorHashCode)
                || _localPlayer.m_nview.m_zdo.m_vec3[_hairColorHashCode] != color)
            {
                _localPlayer.m_nview.GetZDO().Set(_hairColorHashCode, color);
            }
        }
    }
}