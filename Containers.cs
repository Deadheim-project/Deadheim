//using HarmonyLib;

//namespace Deadheim
//{
//    [HarmonyPatch(typeof(Container), "Awake")]
//    public static class Container_Awake_Patch
//    {
//        private const int woodChestRows = 4;
//        private const int woodChestColumns = 5;

//        private const int personalChestRows = 6;
//        private const int personalChestColumns = 6;

//        private const int ironChestRows = 8;
//        private const int ironChestColumns = 6;

//        private const int karveInventoryRows = 4;
//        private const int karveInventoryColumns = 2;

//        private const int longboatInventoryRows = 6;
//        private const int longboatInventoryColumns = 6; 

//        private const int cartInventoryRows = 5;
//        private const int cartInventoryColumns = 6;

//        static void Postfix(Container __instance, ref Inventory ___m_inventory)
//        {
//            if (__instance == null || ___m_inventory == null || !__instance.transform.parent)
//            {
//                if (___m_inventory == null) return;

//                string inventoryName = ___m_inventory.m_name;
//                ref int inventoryColumns = ref ___m_inventory.m_width;
//                ref int inventoryRows = ref ___m_inventory.m_height;

//                if (inventoryName == "$piece_chestprivate")
//                {
//                    inventoryRows = personalChestRows;
//                    inventoryColumns = personalChestColumns;
//                }
//                else if (inventoryName == "$piece_chestwood")
//                {
//                    inventoryRows = woodChestRows;
//                    inventoryColumns = woodChestColumns;
//                }
//                else if (inventoryName == "$piece_chest")
//                {
//                    inventoryRows = ironChestRows;
//                    inventoryColumns = ironChestColumns;
//                }       
//            } else
//            {
//                string containerName = __instance.transform.parent.name;
//                ref int inventoryColumns = ref ___m_inventory.m_width;
//                ref int inventoryRows = ref ___m_inventory.m_height;

//                if (containerName.Contains("Karve"))
//                {
//                    inventoryRows = karveInventoryRows;
//                    inventoryColumns = karveInventoryColumns;
//                }
//                else if (containerName.Contains("VikingShip"))
//                {
//                    inventoryRows = longboatInventoryRows;
//                    inventoryColumns = longboatInventoryColumns;
//                }
//                else if (containerName.Contains("Cart"))
//                {
//                    inventoryRows = cartInventoryRows;
//                    inventoryColumns = cartInventoryColumns;
//                }
//            }


//        }
//    }
//}
