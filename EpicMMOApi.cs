using System;
using System.Reflection;
using UnityEngine;

namespace Deadheim
{
    public static class EpicMMOApi
    {
        private static string pluginKey = "EpicMMOSystem";

        private static API_State state = API_State.NotReady;
        private static MethodInfo eGetLevel;
        private static MethodInfo eAddExp;
        private static MethodInfo eGetAttribute;

        private enum API_State
        {
            NotReady, NotInstalled, Ready
        }



        public static int GetLevel()
        {
            int result = 0;
            Init();
            if (eGetLevel != null) result = (int)eGetLevel.Invoke(null, null);
            return result;
        }

        public static int GetAttribute(string attribute)
        {
            string value = 0.ToString();
            Player.m_localPlayer.m_knownTexts.TryGetValue(pluginKey + "_LevelSystem_" + attribute, out value);
            return Convert.ToInt32(value);
        }

        public static void AddExp(int value)
        {
            Init();
            eAddExp?.Invoke(null, new object[] { value });
        }

        private static void Init()
        {
            if (state is API_State.Ready or API_State.NotInstalled) return;
            if (Type.GetType("EpicMMOSystem.EpicMMOSystem, EpicMMOSystem") == null)
            {
                state = API_State.NotInstalled;
                return;
            }

            state = API_State.Ready;

            Type actionsMO = Type.GetType("API.EMMOS_API, EpicMMOSystem");
            eGetLevel = actionsMO.GetMethod("GetLevel", BindingFlags.Public | BindingFlags.Static);
            eAddExp = actionsMO.GetMethod("AddExp", BindingFlags.Public | BindingFlags.Static);
            eGetAttribute = actionsMO.GetMethod("GetAttribute", BindingFlags.Public | BindingFlags.Static);
        }
    }
}
