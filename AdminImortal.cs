using BepInEx.Configuration;
using Deadheim.Vanilla;
using HarmonyLib;

namespace Deadheim
{
    /// <summary>
    /// Admins (adminlist.txt do servidor) nao tomam dano e nao morrem na borda do mundo.
    /// O dano de jogador e aplicado no cliente dele (dono da ZDO), entao basta barrar ali.
    /// ApplyDamage e o ponto comum de golpe, queda, afogamento, veneno e fogo (esses dois
    /// chamam ApplyDamage direto, sem passar pelo RPC_Damage).
    /// </summary>
    public static class AdminImortal
    {
        public static ConfigEntry<bool> Ligado;

        public static void Bind(ConfigFile config)
        {
            Ligado = Plugin.Synced(config.Bind("Server config", "AdminsImortais", true,
                new ConfigDescription("Admins (adminlist.txt) nao tomam dano nem morrem na borda do mundo.")));
        }

        private static bool Imortal(Character character) =>
            Ligado != null && Ligado.Value && character != null
            && character == Player.m_localPlayer && Admin.LocalPlayerIsAdmin();

        [HarmonyPatch(typeof(Character), nameof(Character.ApplyDamage))]
        private static class SemDano
        {
            [HarmonyPriority(Priority.First)]
            private static bool Prefix(Character __instance) => !Imortal(__instance);
        }

        [HarmonyPatch(typeof(Player), nameof(Player.EdgeOfWorldKill))]
        private static class SemBordaDoMundo
        {
            [HarmonyPriority(Priority.First)]
            private static bool Prefix(Player __instance) => !Imortal(__instance);
        }
    }
}
