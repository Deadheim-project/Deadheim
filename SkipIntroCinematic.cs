using HarmonyLib;

namespace Deadheim
{
    /// <summary>
    /// Pula a cinematica de introducao do Valheim 1.0 para quem joga com o Deadheim.
    ///
    /// O 1.0 toca uma introducao de uns 3 minutos toda vez que o jogo abre, e
    /// durante ela o jogo ignora a entrada (Esc nao pula). Para quem abre pelo
    /// launcher so para entrar no servidor, isso e so espera.
    ///
    /// Em vez de reescrever a coroutine do jogo, respondemos "sim" para a propria
    /// opcao "Skip intro cinematic" das Configuracoes. O FejdStartup ja tem esse
    /// caminho: faz o FadeIn do menu e segue sem chamar CinematicsManager.Play. A
    /// preferencia salva do jogador nao e alterada; so vale enquanto o mod estiver
    /// carregado. A cinematica continua disponivel pelo menu Cinematics.
    /// </summary>
    [HarmonyPatch(typeof(PlatformPrefs), nameof(PlatformPrefs.GetBool))]
    internal static class SkipIntroCinematic
    {
        private const string SkipIntroKey = "SkipIntroCinematic";

        // __0 = primeiro argumento (a chave), por posicao: nao depende do nome do parametro no jogo.
        private static bool Prefix(string __0, ref bool __result)
        {
            if (__0 != SkipIntroKey) return true;
            __result = true;
            return false;
        }
    }
}
