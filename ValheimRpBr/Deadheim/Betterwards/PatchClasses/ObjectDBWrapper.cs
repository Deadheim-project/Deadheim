using UnityEngine;
using HarmonyLib;

namespace BetterWards.PatchClasses
{
    [HarmonyPatch]
    public static class ObjectDBWrapper
    {
        public static ItemDrop GetItem(string name)
        {
            foreach (GameObject gameObject in ObjectDB.instance.m_items)
            {
                ItemDrop component = gameObject.GetComponent<ItemDrop>();
                if (component.m_itemData.m_shared.m_name == name)
                    return component;
            }
            return null;
        }

    }
}
