using Deadheim;
using NameYourTame.Main;

namespace NameYourTame.Utils
{
    class ChangeName
    {
        public static void SetName(ref Tameable instance, ref string name)
        {
            if (!Plugin.playerIsVip)
            {
                Player.m_localPlayer.Message(MessageHud.MessageType.Center, "Apenas vips podem renomear seus pets.");
                return;
            }
            if (name != null)
            {
                if (instance.m_nview.GetZDO().GetString("TameName") == Namer.NotChanged)
                {
                    instance.m_nview.GetZDO().Set("TameName", name);
                    Namer.character = instance.m_character;
                    instance.m_character.m_name = instance.m_nview.GetZDO().GetString("TameName");
                    Namer.update = true;
                    Namer.GUI = false;
                }
            }
        }
    }
}
