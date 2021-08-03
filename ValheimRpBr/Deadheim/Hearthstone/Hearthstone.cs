using BepInEx;
using Deadheim;
using HarmonyLib;
using Jotunn.Entities;
using Jotunn.Managers;
using System;
using UnityEngine;

namespace Hearthstone
{
    [BepInPlugin("Detalhes.Hearthstone", "Hearthstone", "1.0.0")]
    [BepInProcess("valheim.exe")]
    public class Hearthstone : BaseUnityPlugin
    {
        public const string PluginGUID = "Detalhes.Hearthstone";

        private void Awake()
        {
            LoadAssets();
        }

        private void LoadAssets()
        {
            ItemManager.OnVanillaItemsAvailable += AddClonedItems;
        }

        private void AddClonedItems()
        {
            try
            {
                CustomItem CI = new CustomItem("Hearthstone", "YagluthDrop");
                ItemManager.Instance.AddItem(CI);

                ItemDrop itemDrop = CI.ItemDrop;
                itemDrop.m_itemData.m_shared.m_name = "Hearthstone";
                itemDrop.m_itemData.m_shared.m_description = "Go back to spawn point!";
                itemDrop.m_itemData.m_shared.m_maxStackSize = 1;
                itemDrop.m_itemData.m_shared.m_itemType = ItemDrop.ItemData.ItemType.Consumable;

                RecipeHearthStone(itemDrop);
            }
            catch (Exception ex)
            {
                Jotunn.Logger.LogError($"Error while adding cloned item: {ex.Message}");
            }
            finally
            {
                ItemManager.OnVanillaItemsAvailable -= AddClonedItems;
            }
        }

        private void RecipeHearthStone(ItemDrop itemDrop)
        {
            Recipe recipe = ScriptableObject.CreateInstance<Recipe>();
            recipe.name = "Recipe_Hearthstone";
            recipe.m_item = itemDrop;
            recipe.m_craftingStation = PrefabManager.Cache.GetPrefab<CraftingStation>("piece_workbench");
            recipe.m_resources = new Piece.Requirement[]
            {
                new Piece.Requirement()
                {
                    m_resItem = PrefabManager.Cache.GetPrefab<ItemDrop>("Coins"),
                    m_amount = 250
                },
                new Piece.Requirement()
                {
                    m_resItem = PrefabManager.Cache.GetPrefab<ItemDrop>("Resin"),
                    m_amount = 30
                },
                new Piece.Requirement()
                {
                    m_resItem = PrefabManager.Cache.GetPrefab<ItemDrop>("BoneFragments"),
                    m_amount = 30
                }
            };
            CustomRecipe CR = new CustomRecipe(recipe, fixReference: false, fixRequirementReferences: false);
            ItemManager.Instance.AddRecipe(CR);
        }

        [HarmonyPatch(typeof(Player), "ConsumeItem")]
        public static class ConsumePatch
        {
            private static bool Prefix(ItemDrop.ItemData item)
            {
                if (item.m_shared.m_name == "Hearthstone")                
                {
                    if (!Player.m_localPlayer.IsTeleportable())
                    {
                        Player.m_localPlayer.Message(MessageHud.MessageType.Center, "Você não pode teleportar carregando esses items");
                        return false;
                    }

                    Player.m_localPlayer.Message(MessageHud.MessageType.Center, "Você comeu o fruto do criador");
                    Player.m_localPlayer.TeleportTo(Plugin.hearthStoneSpawn, Player.m_localPlayer.transform.rotation, true);
                }

                return true;
            }
        }
    }
}
