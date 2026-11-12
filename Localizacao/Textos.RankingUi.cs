namespace Deadheim
{
    internal static class TextosRankingUi
    {
        public static void Registrar()
        {
            L.Add("rankingui.desligado", "O ranking está desligado neste servidor.",
                "The ranking is turned off on this server.");
            L.Add("rankingui.melhores", "Os melhores do mundo, em cada categoria",
                "The best in the world, in every category");
            L.Add("rankingui.na_lista", "{0} vikings na Pontuação geral · entre para a lista",
                "{0} vikings in the Overall score · join the list");
            L.Add("rankingui.voce_e", "Você é #{0} de {1} na Pontuação geral · {2} pts",
                "You are #{0} of {1} in the Overall score · {2} pts");
            L.Add("rankingui.titulo", "Ranking",
                "Ranking");
            L.Add("rankingui.mundo", "MUNDO",
                "WORLD");
            L.Add("rankingui.feitos", "Feitos do mundo",
                "World firsts");
            L.Add("rankingui.feitos_desc", "O primeiro viking a derrotar cada chefe neste mundo, com a data. Quem já tinha o chefe antes do ranking existir aparece como \"antes do ranking\".",
                "The first viking to defeat each boss in this world, with the date. Anyone who already had the boss before the ranking existed shows as \"before the ranking\".");
            L.Add("rankingui.temporada", "Temporada",
                "Season");
            L.Add("rankingui.semana", "Semana",
                "Week");
            L.Add("rankingui.dica_temp", "Temporada: o total acumulado de cada jogador desde que o ranking existe neste mundo.",
                "Season: each player's accumulated total since the ranking has existed in this world.");
            L.Add("rankingui.dica_sem", "Semana: só o que cada jogador fez desde segunda-feira 00:00 (horário do servidor). Zera toda segunda.",
                "Week: only what each player did since Monday 00:00 (server time). Resets every Monday.");
            L.Add("rankingui.vikings", "Vikings",
                "Vikings");
            L.Add("rankingui.guildas", "Guildas",
                "Guilds");
            L.Add("rankingui.dica_vik", "Vikings: cada jogador na sua posição. Admins do servidor não entram no ranking.",
                "Vikings: each player in their position. Server admins are not in the ranking.");
            L.Add("rankingui.dica_gui", "Guildas: a soma da categoria entre todos os membros da guilda. Aparece também o melhor membro.",
                "Guilds: the category sum across all guild members. The best member is shown too.");
            L.Add("rankingui.todos", "Todos",
                "All");
            L.Add("rankingui.buscar", "Buscar viking...",
                "Search viking...");
            L.Add("rankingui.col_viking", "Viking",
                "Viking");
            L.Add("rankingui.col_valor", "Valor",
                "Value");
            L.Add("rankingui.col_detalhe", "Detalhe",
                "Detail");
            L.Add("rankingui.ir_mim", "Ir para mim",
                "Go to me");
            L.Add("rankingui.so_online", "Só online",
                "Online only");
            L.Add("rankingui.minha_guilda", "Minha guilda",
                "My guild");
            L.Add("rankingui.feitos_curto", "O primeiro viking a derrotar cada chefe neste mundo.",
                "The first viking to defeat each boss in this world.");
            L.Add("rankingui.por_guilda", " <size=18><color=#9e9480>· por guilda</color></size>",
                " <size=18><color=#9e9480>· by guild</color></size>");
            L.Add("rankingui.semana_desde", "  <color=#ffd75a>Semana desde {0}.</color>",
                "  <color=#ffd75a>Week since {0}.</color>");
            L.Add("rankingui.carregando", "Carregando o ranking do servidor...",
                "Loading the server ranking...");
            L.Add("rankingui.ninguem_sem", "Ninguém pontuou aqui nesta semana ainda.\nSeja o primeiro!",
                "Nobody has scored here this week yet.\nBe the first!");
            L.Add("rankingui.ninguem_aqui", "Ninguém pontuou aqui ainda.\nSeja o primeiro!",
                "Nobody has scored here yet.\nBe the first!");
            L.Add("rankingui.ninguem_busca", "Ninguém com \"{0}\" no nome ou na guilda.",
                "Nobody with \"{0}\" in their name or guild.");
            L.Add("rankingui.ninguem_online", "Ninguém desta lista está online agora.",
                "Nobody on this list is online now.");
            L.Add("rankingui.sem_guilda", "Você não tem guilda.",
                "You have no guild.");
            L.Add("rankingui.ninguem_guilda", "Ninguém da sua guilda nesta lista.",
                "Nobody from your guild on this list.");
            L.Add("rankingui.so_podio", "Só estes vikings pontuaram: estão todos no pódio.",
                "Only these vikings scored: they are all on the podium.");
            L.Add("rankingui.os_primeiros", "Detalhe <color=#7a7262>· os {0} primeiros de {1}; busque um nome</color>",
                "Detail <color=#7a7262>· the first {0} of {1}; search for a name</color>");
            L.Add("rankingui.fora_lista", "<color=#9e9480>ainda fora desta lista.</color>",
                "<color=#9e9480>not on this list yet.</color>");
            L.Add("rankingui.fora_lista_t", "ainda fora desta lista.",
                "not on this list yet.");
            L.Add("rankingui.faltam", "· faltam",
                "· missing");
            L.Add("rankingui.para_passar", "para passar",
                "to pass");
            L.Add("rankingui.voce_lidera", "· você lidera!",
                "· you lead!");
            L.Add("rankingui.de_n", "· de {0}",
                "· of {0}");
            L.Add("rankingui.col_guilda", "Guilda",
                "Guild");
            L.Add("rankingui.col_destaque", "Destaque",
                "Top member");
            L.Add("rankingui.nenhuma_guilda", "Nenhuma guilda pontuou aqui ainda.",
                "No guild has scored here yet.");
            L.Add("rankingui.nenhuma_busca", "Nenhuma guilda com \"{0}\" no nome.",
                "No guild with \"{0}\" in its name.");
            L.Add("rankingui.so_guildas", "Só estas guildas pontuaram.",
                "Only these guilds scored.");
            L.Add("rankingui.sem_guilda_npc", "Você não tem guilda. Crie ou entre numa no NPC Registrador de Guildas.",
                "You have no guild. Create or join one at the Guild Registrar NPC.");
            L.Add("rankingui.guilda_nao", "ainda não pontuou nesta categoria.",
                "has not scored in this category yet.");
            L.Add("rankingui.guilda_lidera", "· sua guilda lidera!",
                "· your guild leads!");
            L.Add("rankingui.col_chefe", "Chefe",
                "Boss");
            L.Add("rankingui.col_primeiro", "Primeiro",
                "First");
            L.Add("rankingui.col_quando", "Quando",
                "When");
            L.Add("rankingui.ninguem_derrotou", "ninguém derrotou ainda",
                "nobody has defeated it yet");
            L.Add("rankingui.um_derrotou", "1 viking derrotou",
                "1 viking defeated it");
            L.Add("rankingui.n_derrotaram", "{0} vikings derrotaram",
                "{0} vikings defeated it");
            L.Add("rankingui.sem_registro", "derrotado, sem registro",
                "defeated, no record");
            L.Add("rankingui.seja_primeiro", "seja o primeiro!",
                "be the first!");
            L.Add("rankingui.antes", "antes do ranking",
                "before the ranking");
            L.Add("rankingui.antes_data", "antes do ranking · {0}",
                "before the ranking · {0}");
            L.Add("rankingui.sem_chefe", "Nenhum chefe configurado (PvP > BossOrder).",
                "No boss configured (PvP > BossOrder).");
            L.Add("rankingui.dia_mundo", "  <color=#ffd75a>Dia {0} · o mundo já derrotou {1} de {2} chefes.</color>",
                "  <color=#ffd75a>Day {0} · the world has defeated {1} of {2} bosses.</color>");
            L.Add("rankingui.voce_tem", "<color=#ffd75a>Você tem {0} {1} do mundo.</color> <color=#9e9480>Ninguém tira: é seu para sempre.</color>",
                "<color=#ffd75a>You have {0} world {1}.</color> <color=#9e9480>Nobody can take it: it is yours forever.</color>");
            L.Add("rankingui.feito_1", "feito",
                "first");
            L.Add("rankingui.feito_n", "feitos",
                "firsts");
            L.Add("rankingui.seu_nome", "<color=#9e9480>Derrote um chefe antes de todo mundo e o seu nome fica aqui para sempre.</color>",
                "<color=#9e9480>Defeat a boss before everyone else and your name stays here forever.</color>");
            L.Add("rankingui.lugar_livre", "lugar livre",
                "free spot");
            L.Add("rankingui.srv_nao_resp", "O servidor não respondeu.",
                "The server did not respond.");
            L.Add("rankingui.conectando", "Conectando ao servidor...",
                "Connecting to the server...");
            L.Add("rankingui.srv_nao_mandou", "O servidor não mandou o ranking.\nEle precisa estar com a mesma versão do Deadheim que você.",
                "The server did not send the ranking.\nIt must be running the same Deadheim version as you.");
            L.Add("rankingui.att_agora", "atualizado agora",
                "updated just now");
            L.Add("rankingui.att_s", "atualizado há {0} s",
                "updated {0} s ago");
            L.Add("rankingui.att_min", "atualizado há {0} min",
                "updated {0} min ago");
            L.Add("rankingui.dia", "dia {0}",
                "day {0}");
            L.Add("rankingui.subtitulo", "{0}{1} vikings  ·  <color=#7fd08a>{2} online</color>  ·  {3}  ·  <color=#7a7262>setas do teclado trocam a categoria · clique num nome para ver a ficha</color>",
                "{0}{1} vikings  ·  <color=#7fd08a>{2} online</color>  ·  {3}  ·  <color=#7a7262>arrow keys change category · click a name to see the sheet</color>");
            L.Add("rankingui.online_pt", "• online",
                "• online");
            L.Add("rankingui.visto", "visto {0}",
                "seen {0}");
            L.Add("rankingui.desde", "no mundo desde {0}",
                "in the world since {0}");
            L.Add("rankingui.fecha", "Fechar",
                "Close");
            L.Add("rankingui.novo", "novo",
                "new");
            L.Add("rankingui.sem_guilda_t", "sem guilda",
                "no guild");
            L.Add("rankingui.membro_1", "1 membro",
                "1 member");
            L.Add("rankingui.membros_n", "{0} membros",
                "{0} members");
            L.Add("rankingui.cta_pvp", "Abata um jogador fora da arena para entrar.",
                "Kill a player outside the arena to join.");
            L.Add("rankingui.cta_guerra", "Entre numa guilda e lute na guerra de castelos.",
                "Join a guild and fight in the castle war.");
            L.Add("rankingui.cta_chefes", "Derrote o primeiro chefe para entrar.",
                "Defeat the first boss to join.");
            L.Add("rankingui.cta_sem", "Jogue nesta semana para aparecer aqui.",
                "Play this week to show up here.");
            L.Add("rankingui.cta_num", "Seus números chegam em até um minuto.",
                "Your numbers arrive within a minute.");
            L.Add("rankingui.rel_agora", "agora há pouco",
                "just now");
            L.Add("rankingui.rel_min", "há {0} min",
                "{0} min ago");
            L.Add("rankingui.rel_h", "há {0} h",
                "{0} h ago");
            L.Add("rankingui.ontem", "ontem",
                "yesterday");
            L.Add("rankingui.rel_dias", "há {0} dias",
                "{0} days ago");
            L.Add("rankingui.resumo", "Mortes: {0}  ·  PvP: {1} abates / {2} mortes  ·  maior habilidade {3}",
                "Deaths: {0}  ·  PvP: {1} kills / {2} deaths  ·  highest skill {3}");
        }
    }
}
