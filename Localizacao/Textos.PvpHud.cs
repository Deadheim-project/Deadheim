namespace Deadheim
{
    internal static class TextosPvpHud
    {
        public static void Registrar()
        {
            L.Add("pvphud.b_combat", "Em combate",
                "In combat");
            L.Add("pvphud.t_combat", "Sem retreat nem pedra de retorno até a luta esfriar; luta com jogador também trava portal e tira a zona segura.",
                "No retreat or return stone until the fight cools down; fighting a player also locks portals and removes the safe zone.");
            L.Add("pvphud.b_immune", "Imune a PvP",
                "PvP immune");
            L.Add("pvphud.t_immune", "Morreu para um jogador: não dá nem leva dano de jogador. Monstro continua ferindo.",
                "Killed by a player: you neither deal nor take player damage. Monsters still hurt.");
            L.Add("pvphud.b_pk", "PK",
                "PK");
            L.Add("pvphud.t_pk", "Matou quem não estava lutando. Sem zona segura; se morrer perde mais. O tempo só corre online e a marca só sai morto por jogador.",
                "Killed someone who was not fighting. No safe zone; if you die you lose more. Time only runs online and the mark only clears if a player kills you.");
            L.Add("pvphud.b_aggressor", "Agressor",
                "Aggressor");
            L.Add("pvphud.t_aggressor", "Bateu primeiro: quem te matar não vira PK. O tempo para enquanto você luta.",
                "Hit first: whoever kills you does not become PK. The timer stops while you fight.");
            L.Add("pvphud.b_hunted", "Caçado",
                "Hunted");
            L.Add("pvphud.t_hunted", "Há uma bounty na sua cabeça: aparece no mapa, sem zona segura, imunidade nem teleporte.",
                "There is a bounty on your head: you show on the map, with no safe zone, immunity or teleport.");
            L.Add("pvphud.b_bounty", "Bounty",
                "Bounty");
            L.Add("pvphud.t_bounty", "Colocaram moedas na sua cabeça. Quando o tempo acabar você vira CAÇADO. O Caçador de Recompensas encerra se você pagar.",
                "Coins were placed on your head. When time runs out you become HUNTED. The Bounty Hunter ends it if you pay.");
            L.Add("pvphud.b_safe", "Zona segura",
                "Safe zone");
            L.Add("pvphud.t_safe", "Ninguém te fere aqui, e você não fere ninguém, enquanto não estiver em combate.",
                "Nobody can hurt you here, and you cannot hurt anyone, while not in combat.");
            L.Add("pvphud.b_pve", "PvE",
                "PvE");
            L.Add("pvphud.t_pve", "PvE: não luta com jogadores em lugar nenhum. O Juiz de Guerra troca de volta.",
                "PvE: you do not fight players anywhere. The War Judge switches you back.");
            L.Add("pvphud.b_cdhs", "Hearthstone em recarga",
                "Hearthstone on cooldown");
            L.Add("pvphud.t_cdhs", "A pedra de retorno está em recarga. O tempo só corre com o jogo aberto.",
                "The return stone is on cooldown. Time only runs while the game is open.");
            L.Add("pvphud.b_cdr1", "Retreat 1 em recarga",
                "Retreat 1 on cooldown");
            L.Add("pvphud.t_cdr1", "O /retreat para o ponto 1 está em recarga. O tempo só corre com o jogo aberto.",
                "The /retreat to point 1 is on cooldown. Time only runs while the game is open.");
            L.Add("pvphud.b_cdr2", "Retreat 2 em recarga",
                "Retreat 2 on cooldown");
            L.Add("pvphud.t_cdr2", "O /retreat 2 (segundo ponto) está em recarga. O tempo só corre com o jogo aberto.",
                "The /retreat 2 (second point) is on cooldown. Time only runs while the game is open.");
            L.Add("pvphud.luta_monstro", "Luta com monstro",
                "Fighting a monster");
            L.Add("pvphud.em_combate", "Em combate",
                "In combat");
            L.Add("pvphud.pk_perm", "PK permanente",
                "Permanent PK");
            L.Add("pvphud.cacado_morrer", "Caçado até morrer",
                "Hunted until death");
            L.Add("pvphud.cacado", "Caçado",
                "Hunted");
            L.Add("pvphud.bounty_n", "Bounty {0}",
                "Bounty {0}");
            L.Add("pvphud.zona_segura", "Zona segura",
                "Safe zone");
            L.Add("pvphud.pvp_ativo_t", "PvP ATIVO",
                "PvP ACTIVE");
            L.Add("pvphud.cuidado", "Cuidado: outros jogadores podem atacar você.",
                "Careful: other players can attack you.");
            L.Add("pvphud.pvp_off_t", "PvP desligado",
                "PvP off");
            L.Add("pvphud.ninguem_ataca", "Ninguém pode atacar você agora.",
                "Nobody can attack you right now.");
            L.Add("pvphud.h_pve", "<color=#9acd32>PvE: {0}</color>",
                "<color=#9acd32>PvE: {0}</color>");
            L.Add("pvphud.h_cacado", "<color=#ff5050>CAÇADO {0} ({1} moedas){2}</color>",
                "<color=#ff5050>HUNTED {0} ({1} coins){2}</color>");
            L.Add("pvphud.h_imune", "<color=#7fd4ff>IMUNE A PvP {0}</color>",
                "<color=#7fd4ff>PvP IMMUNE {0}</color>");
            L.Add("pvphud.h_zona", "<color=#7CFC00>ZONA SEGURA: {0}</color>",
                "<color=#7CFC00>SAFE ZONE: {0}</color>");
            L.Add("pvphud.h_ativo", "<color=#ff5050>PvP ATIVO</color>",
                "<color=#ff5050>PvP ACTIVE</color>");
            L.Add("pvphud.h_desl", "<color=#c0c0c0>PvP desligado</color>",
                "<color=#c0c0c0>PvP off</color>");
            L.Add("pvphud.h_pkperm", "<color=#ff3030>PK PERMANENTE{0}</color>",
                "<color=#ff3030>PERMANENT PK{0}</color>");
            L.Add("pvphud.h_pk", "<color=#ff3030>PK {0}{1}</color>",
                "<color=#ff3030>PK {0}{1}</color>");
            L.Add("pvphud.h_agressor", "<color=#ff7a3d>AGRESSOR</color>",
                "<color=#ff7a3d>AGGRESSOR</color>");
            L.Add("pvphud.h_pend", "<color=#ff8c00>Bounty de {0}: caçado em {1}{2}</color>",
                "<color=#ff8c00>Bounty of {0}: hunted in {1}{2}</color>");
            L.Add("pvphud.h_comb", "<color=#ffb347>Em combate {0}s</color>",
                "<color=#ffb347>In combat {0}s</color>");
            L.Add("pvphud.h_monstro", "<color=#ffb347>Luta com monstro {0}s</color>",
                "<color=#ffb347>Fighting a monster {0}s</color>");
            L.Add("pvphud.rank_titulo", "--- Ranking PvP (K/D) ---",
                "--- PvP Ranking (K/D) ---");
            L.Add("pvphud.voce", "Você: {0}",
                "You: {0}");
            L.Add("pvphud.rank_painel", "Ranking PvP (K/D)",
                "PvP Ranking (K/D)");
            L.Add("pvphud.ninguem_pont", "Ninguém pontuou ainda.",
                "Nobody has scored yet.");
            L.Add("pvphud.fechar", "Fechar",
                "Close");
            L.Add("pvphud.pausado", " (pausado)",
                " (paused)");
            L.Add("pvphud.pausada", " (pausada)",
                " (paused)");
            L.Add("pvphud.ate_morrer", "até morrer",
                "until death");
        }
    }
}
