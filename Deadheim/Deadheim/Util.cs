using UnityEngine;

namespace Deadheim
{
    public class Util
    {
        public static void SavePlayer()
        {
            Debug.Log("SavePlayer");

            if (Game.instance.m_saveTimer == 0f || Game.instance.m_saveTimer < 60)
            {
                return;
            }

            if (!Game.instance) return;

            Game.instance.m_saveTimer = 0f;
            Game.instance.SavePlayerProfile(false);
        }
    }
}
