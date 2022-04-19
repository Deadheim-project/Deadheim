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

                foreach (var collider in colliders)
                {
                    if (collider?.gameObject?.GetComponentInParent<Pickable>() is Pickable nearbyPickable &&
                        nearbyPickable != targetedPickable)
                    {
                        if (nearbyPickable.m_itemPrefab.name == targetedPickable.m_itemPrefab.name)
                        {
                            nearbyPickable.Interact(__instance, false, alt);
                        }
                    }
                }
            }
            else if (interactible is Beehive beehive)
            {
                var interactMask = (int)m_interactMaskField.GetValue(__instance);
                var colliders = Physics.OverlapSphere(go.transform.position, 10f, interactMask);

                foreach (var collider in colliders)
                {
                    if (collider?.gameObject?.GetComponentInParent<Beehive>() is Beehive nearbyBeehive &&
                        nearbyBeehive != beehive)
                    {
                        if (PrivateArea.CheckAccess(nearbyBeehive.transform.position))
                        {
                            _ExtractMethod.Invoke(nearbyBeehive, null);
                        }
                    }
                }
            }
        }
    }
}
