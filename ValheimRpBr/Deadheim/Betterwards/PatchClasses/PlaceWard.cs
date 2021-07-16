using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using UnityEngine;
using BepInEx.Logging;
using UnityEngine.UI;
using HarmonyLib;

namespace BetterWards.PatchClasses
{
    [HarmonyPatch]
    public class PlaceWard
    {

        //[HarmonyPatch(typeof(PieceTable), "GetSelectedPiece")]
        //[HarmonyPostfix]
        //public static void Postfix(Piece __result)
        //{
        //    GameObject pArea = __result.gameObject;
        //    Player.m_localPlayer.Message(MessageHud.MessageType.Center, string.Format("{0}", (object)__result.ToString()));
        //    if (__result.ToString().Contains("guard"))
        //    {


        //        //Player.m_localPlayer.m_placementStatus = !Player.PlacementStatus.BlockedbyPlayer;
        //    }
        //}


        //[HarmonyPatch(typeof(Player), "GetPiece")]
        //[HarmonyPostfix]
        //private static void GetPiece(Piece __result)
        //{
        //    if (__result != null)
        //        ZLog.Log("Better Wards - Updating player GETPiece " + __result.ToString());

        //}

        //[HarmonyPatch(typeof(Player), "PlacePiece")]
        //[HarmonyPrefix]
        //private static bool PlacePiece(Player __instance, Piece piece)
        //{

        //    if (piece != null && piece.ToString().Contains("guard"))
        //    {
        //        ZLog.Log("Better Wards - Updating player PlacePiece " + piece.ToString());
        //        while (BetterWardsPlugin.wardCount <= 2)
        //        {
        //            BetterWardsPlugin.wardCount++;
        //            return true;
        //        }
        //        return false;
        //    }
        //    return true;
        //}

        [HarmonyPatch]
        public static class RegisterAndCheckVersion
        {
            [HarmonyPatch(typeof(ZNet), "RPC_CharacterID")]
            [HarmonyPrefix]
            private static void Prefix(ref ZNet __instance)
            {

                long playerID = Game.instance.m_playerProfile.m_playerID;
                int i = 0;
                int gsh = "guard_stone".GetStableHashCode();
                int ch = "creator".GetStableHashCode();
                foreach (ZDO zdo in ZDOMan.instance.m_objectsByID.Values)
                {
                    if (zdo.m_prefab == gsh && zdo.GetLong(ch, 0L) == playerID) i++;


                }
                ZLog.Log($"{i} Wards placed by player");
            }


        }
    }

    /*
     long playerID = Game.instance.m_playerProfile.m_playerID;
                int i = 0;
                foreach (KeyValuePair<ZDOID, ZDO> keyValuePair in ZDOMan.instance.m_objectsByID)
                {
                    ZDO getZDO = keyValuePair.Value;
                    if (getZDO.m_prefab == "guard_stone".GetStableHashCode() && getZDO.GetLong("creator".GetStableHashCode(), 0L) == playerID)
                    {
                        i++;
                    }
                }
                print($"{i} Wards placed by player");
    */


    // SECOND METHOD

    //        long playerID = Game.instance.m_playerProfile.m_playerID;
    //        int count = 0;
    //        int gsh = "guard_stone".GetStableHashCode();
    //        int ch = "creator".GetStableHashCode();
    //foreach (ZDO zdo in ZDOMan.instance.m_objectsByID.Values)
    //{
    //    if (getZDO.m_prefab == gsh && getZDO.GetLong(ch, 0L) == playerID) count++
    //}
    //    print($"{count} Wards placed by player");
}
