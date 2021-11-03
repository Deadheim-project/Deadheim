using Jotunn.Entities;
using Jotunn.Managers;
using System;

namespace Deadheim
{
    public class PortalToken
    {
        public static void LoadAssets()
        {
            PrefabManager.OnPrefabsRegistered += AddClonedItems;
        }

        private static void AddClonedItems()
        {
            try
            {
                CustomItem CI = new CustomItem("PortalToken", "Thunderstone");
                ItemDrop itemDrop = CI.ItemDrop;
                itemDrop.m_itemData.m_shared.m_name = "Portal Token";
                itemDrop.m_itemData.m_shared.m_description = "Use to help deadheim keep going";
                itemDrop.m_itemData.m_shared.m_maxStackSize = 10;
                ItemManager.Instance.AddItem(CI);
            }
            catch (Exception ex)
            {
                Jotunn.Logger.LogError($"Error while adding cloned item: {ex.Message}");
            }
            finally
            {
                ItemService.AddPortal();
                PrefabManager.OnPrefabsRegistered -= AddClonedItems;
            }
        }
    }
}
