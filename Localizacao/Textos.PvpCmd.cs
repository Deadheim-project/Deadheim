namespace Deadheim
{
    internal static class TextosPvpCmd
    {
        public static void Registrar()
        {
            L.Add("pvpcmd.modulo_off", "O módulo de PvP está desligado neste servidor.",
                "The PvP module is turned off on this server.");
            L.Add("pvpcmd.entre", "Entre no mundo primeiro.",
                "Enter the world first.");
            L.Add("pvpcmd.estado", "Estado: {0}",
                "Status: {0}");
            L.Add("pvpcmd.dano", "Dano PvP x{0}; no seu território x{1}.",
                "PvP damage x{0}; in your territory x{1}.");
            L.Add("pvpcmd.bounty_cab", "Bounty na sua cabeça: {0} moedas{1}",
                "Bounty on your head: {0} coins{1}");
            L.Add("pvpcmd.pagar", " (pagar: {0} no Caçador de Recompensas).",
                " (pay: {0} at the Bounty Hunter).");
            L.Add("pvpcmd.ninguem_bounty", "Ninguém pode colocar bounty em você por {0}.",
                "Nobody can place a bounty on you for {0}.");
            L.Add("pvpcmd.guilda", "Guilda: {0} (sem fogo amigo entre membros)",
                "Guild: {0} (no friendly fire between members)");
            L.Add("pvpcmd.sem_guilda", "Sem guilda: entre numa pelo mod Guilds para ter aliados sem fogo amigo.",
                "No guild: join one through the Guilds mod to have allies without friendly fire.");
            L.Add("pvpcmd.grupo", "Grupo (Groups): quem está no seu grupo também é aliado, sem fogo amigo.",
                "Group (Groups): members of your group are also allies, no friendly fire.");
            L.Add("pvpcmd.contador", "Contador de PK: {0} abate(s) que deram PK.",
                "PK counter: {0} kill(s) that gave PK.");
            L.Add("pvpcmd.contador0", "Contador de PK: 0.",
                "PK counter: 0.");
            L.Add("pvpcmd.voce_pk_perm", "Você é PK PERMANENTE (só sai morto por jogador)",
                "You are a PERMANENT PK (only cleared by being killed by a player)");
            L.Add("pvpcmd.voce_pk", "Você é PK",
                "You are PK");
            L.Add("pvpcmd.se_morrer", ": se morrer, perde {0}x skill{1}.",
                ": if you die, you lose {0}x skill{1}.");
            L.Add("pvpcmd.voce_pve", "Você é PvE ({0}): não luta com jogadores; skill x{1}. Para voltar ao PvP, fale com o Juiz de Guerra.",
                "You are PvE ({0}): you do not fight players; skill x{1}. To return to PvP, talk to the War Judge.");
            L.Add("pvpcmd.sair_pvp", "Para sair do PvP (virar PvE), fale com o Juiz de Guerra.",
                "To leave PvP (become PvE), talk to the War Judge.");
            L.Add("pvpcmd.agressor", "Você é AGRESSOR: bateu primeiro. Quem te matar agora não vira PK (você não perde nada a mais).",
                "You are AGGRESSOR: you hit first. Whoever kills you now does not become PK (you lose nothing extra).");
            L.Add("pvpcmd.castelo", "Castelo {0}, dono: {1}.",
                "Castle {0}, owner: {1}.");
            L.Add("pvpcmd.ninguem", "ninguém",
                "nobody");
            L.Add("pvpcmd.bounty_info", "Bounty (cabeça a prêmio): fale com o Caçador de Recompensas.",
                "Bounty (price on a head): talk to the Bounty Hunter.");
            L.Add("pvpcmd.rank_cmd", "/rank - ranking K/D",
                "/rank - K/D ranking");
            L.Add("pvpcmd.so_admin", "Só admin.",
                "Admin only.");
        }
    }
}
