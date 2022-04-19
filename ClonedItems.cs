using Jotunn.Entities;
using Jotunn.Managers;
using UnityEngine;
using System;
using Jotunn.Configs;

namespace Deadheim
{
    public class ClonedItems
    {
        public static void LoadAssets()
        {
            PrefabManager.OnPrefabsRegistered += AddClonedItems;
            CreatureManager.OnVanillaCreaturesAvailable += AddVanillaClonedCreatures;
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
            }
        }

        private static void AddVanillaClonedCreatures()
        {
            AddBatzao();
            AddNomTameableWolf();
            CreatureManager.OnVanillaCreaturesAvailable -= AddVanillaClonedCreatures;
        }

        private static void AddNomTameableWolf()
        {
            var batzao = new CustomCreature("LoboNaoDomavel", "Wolf",
                new Jotunn.Configs.CreatureConfig
                {               

                });

            var humanoid = batzao.Prefab.GetComponent<Humanoid>();
            humanoid.m_name = "Lobo nao domavel";
            CreatureManager.Instance.AddCreature(batzao);

            UnityEngine.Object.Destroy(batzao.Prefab.GetComponent<Tameable>());
            UnityEngine.Object.Destroy(batzao.Prefab.GetComponent<Procreation>());
        }

        private static void AddBatzao()
        {
            var batzao = new CustomCreature("Morcegao", "Bat",
                new Jotunn.Configs.CreatureConfig
                {
                    DropConfigs = new[]
                    {
                        new DropConfig
                        {
                            Item = "Coins",
                            Chance = 100,
                            MinAmount = 50,
                            MaxAmount = 100,
                            OnePerPlayer = false,
                            LevelMultiplier = false
                        }
                    },
                    Faction = Character.Faction.Undead
                });

            Vector3 newScale = batzao.Prefab.transform.localScale;
            newScale.x *= 3;
            newScale.y *= 3;
            newScale.z *= 3; 
            batzao.Prefab.transform.localScale = newScale;

            var humanoid = batzao.Prefab.GetComponent<Humanoid>();
            humanoid.m_name = "Morcegão";
            humanoid.m_health = 500;
            humanoid.m_boss = true;

            CreatureManager.Instance.AddCreature(batzao);
        }
    }
}
