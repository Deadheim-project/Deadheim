/*
 * MIT License

Copyright (c) 2021 Joshua Shaffer

Permission is hereby granted, free of charge, to any person obtaining a copy
of this software and associated documentation files (the "Software"), to deal
in the Software without restriction, including without limitation the rights
to use, copy, modify, merge, publish, distribute, sublicense, and/or sell
copies of the Software, and to permit persons to whom the Software is
furnished to do so, subject to the following conditions:

The above copyright notice and this permission notice shall be included in all
copies or substantial portions of the Software.

THE SOFTWARE IS PROVIDED "AS IS", WITHOUT WARRANTY OF ANY KIND, EXPRESS OR
IMPLIED, INCLUDING BUT NOT LIMITED TO THE WARRANTIES OF MERCHANTABILITY,
FITNESS FOR A PARTICULAR PURPOSE AND NONINFRINGEMENT. IN NO EVENT SHALL THE
AUTHORS OR COPYRIGHT HOLDERS BE LIABLE FOR ANY CLAIM, DAMAGES OR OTHER
LIABILITY, WHETHER IN AN ACTION OF CONTRACT, TORT OR OTHERWISE, ARISING FROM,
OUT OF OR IN CONNECTION WITH THE SOFTWARE OR THE USE OR OTHER DEALINGS IN THE
SOFTWARE. */


using HarmonyLib;
using System.Reflection;
using UnityEngine;

namespace MassFarming
{
    [HarmonyPatch]
    public static class MassPickup
    {
        static FieldInfo m_interactMaskField = AccessTools.Field(typeof(Player), "m_interactMask");
        static MethodInfo _ExtractMethod = AccessTools.Method(typeof(Beehive), "Extract");

        [HarmonyPatch(typeof(Player), "Interact")]
        public static void Prefix(Player __instance, GameObject go, bool hold, bool alt)
        {
            try { Colher(__instance, go, hold, alt); }
            catch (System.Exception e) { Deadheim.AesirLog.Erro(5, e); }
        }

        private static void Colher(Player __instance, GameObject go, bool hold, bool alt)
        {
            // Aesir 5 (14/10/2026, decisao do usuario): colheita/extracao em massa so para VIP.
            if (Deadheim.AesirConfig.Aesir5SoVip != null && Deadheim.AesirConfig.Aesir5SoVip.Value
                && !VipList.VipListApi.IsLocalPlayerVip())
            {
                // So loga a tentativa de verdade (Shift+E de quem esta jogando), nao toda interacao.
                if (!hold && __instance == Player.m_localPlayer && Input.GetKey(KeyCode.LeftShift))
                    Deadheim.AesirLog.D(5, "Shift+E negado: nao e VIP (Aesir5SoVip=true).");
                return;
            }

            if (__instance.InAttack() || __instance.InDodge())
            {
                return;
            }

            if (hold)
            {
                return;
            }

            if (!Input.GetKey(KeyCode.LeftShift))
            {
                return;
            }

            var interactible = go.GetComponentInParent<Interactable>();
            if (interactible is Pickable targetedPickable)
            {
                var interactMask = (int)m_interactMaskField.GetValue(__instance);
                var colliders = Physics.OverlapSphere(go.transform.position, 10f, interactMask);
                int colhidos = 0;

                foreach (var collider in colliders)
                {
                    if (collider?.gameObject?.GetComponentInParent<Pickable>() is Pickable nearbyPickable &&
                        nearbyPickable != targetedPickable)
                    {
                        if (nearbyPickable.m_itemPrefab.name == targetedPickable.m_itemPrefab.name)
                        {
                            // O retorno do Interact e se a animacao toca, nao se colheu: conta antes.
                            if (nearbyPickable.CanBePicked()) colhidos++;
                            nearbyPickable.Interact(__instance, false, alt);
                        }
                    }
                }
                Deadheim.AesirLog.D(5, "colheita em massa: " + colhidos + "x " + targetedPickable.m_itemPrefab.name + " a mais num raio de 10 m.");
            }
            else if (interactible is Beehive beehive)
            {
                var interactMask = (int)m_interactMaskField.GetValue(__instance);
                var colliders = Physics.OverlapSphere(go.transform.position, 10f, interactMask);
                var vistas = new System.Collections.Generic.HashSet<Beehive>();
                int semAcesso = 0;

                foreach (var collider in colliders)
                {
                    if (collider?.gameObject?.GetComponentInParent<Beehive>() is Beehive nearbyBeehive &&
                        nearbyBeehive != beehive && vistas.Add(nearbyBeehive))
                    {
                        if (PrivateArea.CheckAccess(nearbyBeehive.transform.position))
                        {
                            _ExtractMethod.Invoke(nearbyBeehive, null);
                        }
                        else semAcesso++;
                    }
                }
                Deadheim.AesirLog.D(5, "extracao em massa: " + (vistas.Count - semAcesso) + " colmeia(s) a mais"
                    + (semAcesso > 0 ? ", " + semAcesso + " sem acesso (ward)" : "") + ".");
            }
        }
    }
}
