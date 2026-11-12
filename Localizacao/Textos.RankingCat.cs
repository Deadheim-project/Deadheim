namespace Deadheim
{
    internal static class TextosRankingCat
    {
        public static void Registrar()
        {
            L.Add("rankingcat.c_monstro_titulo", "Mortes por monstros",
                "Deaths by monsters");
            L.Add("rankingcat.c_monstro_descricao", "Vezes que um monstro ou criatura matou você.",
                "Times a monster or creature killed you.");
            L.Add("rankingcat.c_jogador_titulo", "Mortes por jogadores",
                "Deaths by players");
            L.Add("rankingcat.c_jogador_descricao", "Vezes que outro jogador matou você (PvP, inclusive na arena se o jogo registrar).",
                "Times another player killed you (PvP, including the arena if the game records it).");
            L.Add("rankingcat.c_queda_titulo", "Mortes por queda",
                "Deaths by falling");
            L.Add("rankingcat.c_queda_descricao", "Quedas e impactos fatais.",
                "Fatal falls and impacts.");
            L.Add("rankingcat.c_afogado_titulo", "Mortes por afogamento",
                "Deaths by drowning");
            L.Add("rankingcat.c_afogado_descricao", "Afogado ou na água/oceano de cinzas.",
                "Drowned, or in water/Ashlands ocean.");
            L.Add("rankingcat.c_fogo_titulo", "Mortes por fogo e lava",
                "Deaths by fire and lava");
            L.Add("rankingcat.c_fogo_descricao", "Queimado, fogo de cinzas, incinerador, lava ou fumaça.",
                "Burned, Ashlands fire, incinerator, lava or smoke.");
            L.Add("rankingcat.c_clima_titulo", "Mortes por frio e veneno",
                "Deaths by cold and poison");
            L.Add("rankingcat.c_clima_descricao", "Congelado ou envenenado.",
                "Frozen or poisoned.");
            L.Add("rankingcat.c_outras_titulo", "Mortes por acidente",
                "Deaths by accident");
            L.Add("rankingcat.c_outras_descricao", "Árvore caindo, carroça, barco, construção, torre, estalactite, fim do mundo, suicídio e causas não identificadas.",
                "Falling tree, cart, boat, building, tower, stalactite, world end, suicide and unidentified causes.");
            L.Add("rankingcat.c_lenda_titulo", "Pontuação geral",
                "Overall score");
            L.Add("rankingcat.c_lenda_unidade", "pts",
                "pts");
            L.Add("rankingcat.c_lenda_descricao", "Soma de pontos de tudo o que você faz no mundo (como é calculada: passe o mouse).",
                "Sum of points for everything you do in the world (how it is calculated: hover).");
            L.Add("rankingcat.c_pvp_titulo", "Abates PvP",
                "PvP kills");
            L.Add("rankingcat.c_pvp_unidade", "abates",
                "kills");
            L.Add("rankingcat.c_pvp_descricao", "Jogadores abatidos fora da arena, conferido pelo servidor.",
                "Players killed outside the arena, verified by the server.");
            L.Add("rankingcat.c_pvp_calculo", "Jogadores que você abateu fora da arena. Contado e conferido pelo servidor, não pelo seu jogo. K/D = abates ÷ mortes em PvP; PK = abates de inocentes.",
                "Players you killed outside the arena. Counted and verified by the server, not by your game. K/D = kills ÷ PvP deaths; PK = kills of innocents.");
            L.Add("rankingcat.c_guerra_titulo", "Guerra de guildas",
                "Guild war");
            L.Add("rankingcat.c_guerra_unidade", "pts",
                "pts");
            L.Add("rankingcat.c_guerra_descricao", "Pontos de guerra de guildas (categoria oculta: o RaidSystem foi removido).",
                "Guild war points (hidden category: RaidSystem was removed).");
            L.Add("rankingcat.c_guerra_calculo", "Sem fonte de dados desde a remocao do RaidSystem.",
                "No data source since RaidSystem was removed.");
            L.Add("rankingcat.c_arena_titulo", "Arena (rating)",
                "Arena (rating)");
            L.Add("rankingcat.c_arena_unidade", "de rating",
                "rating");
            L.Add("rankingcat.c_arena_descricao", "Maior rating pessoal nos times de arena (na semana: vitórias).",
                "Highest personal rating in arena teams (weekly: wins).");
            L.Add("rankingcat.c_arena_calculo", "Na temporada: seu maior rating pessoal nos times de arena (só entra quem já jogou uma partida). Na semana: vitórias ganhas na semana. Contado pelo servidor.",
                "Season: your highest personal rating in arena teams (only players who have played a match). Weekly: wins earned this week. Counted by the server.");
            L.Add("rankingcat.c_chefes_titulo", "Chefes derrotados",
                "Bosses defeated");
            L.Add("rankingcat.c_chefes_unidade", "chefes",
                "bosses");
            L.Add("rankingcat.c_chefes_descricao", "Chefes diferentes derrotados (na semana: chefes novos).",
                "Different bosses defeated (weekly: new bosses).");
            L.Add("rankingcat.c_chefes_calculo", "Quantos chefes DIFERENTES você já derrotou (cada um conta uma vez, não importa quantas vezes o mate). Lido do perfil do personagem. Na semana: chefes novos.",
                "How many DIFFERENT bosses you have defeated (each counts once, no matter how many times you kill it). Read from the character profile. Weekly: new bosses.");
            L.Add("rankingcat.c_monstros_titulo", "Monstros abatidos",
                "Monsters killed");
            L.Add("rankingcat.c_monstros_unidade", "abates",
                "kills");
            L.Add("rankingcat.c_monstros_descricao", "Monstros e criaturas abatidos.",
                "Monsters and creatures killed.");
            L.Add("rankingcat.c_monstros_calculo", "Monstros e criaturas que você matou (contador EnemyKills do jogo). Chefes NÃO entram aqui, têm categoria própria.",
                "Monsters and creatures you killed (the game's EnemyKills counter). Bosses do NOT count here, they have their own category.");
            L.Add("rankingcat.c_habilidades_titulo", "Soma das habilidades",
                "Skill total");
            L.Add("rankingcat.c_habilidades_unidade", "níveis",
                "levels");
            L.Add("rankingcat.c_habilidades_descricao", "Soma dos níveis de todas as habilidades (na semana: níveis ganhos).",
                "Sum of all skill levels (weekly: levels gained).");
            L.Add("rankingcat.c_habilidades_calculo", "Soma dos níveis de todas as habilidades do personagem, lida do arquivo do personagem. Cai se você perder nível ao morrer. Na semana: níveis ganhos.",
                "Sum of all of your character's skill levels, read from the character file. Drops if you lose levels on death. Weekly: levels gained.");
            L.Add("rankingcat.c_missoes_titulo", "Missões dos NPCs",
                "NPC quests");
            L.Add("rankingcat.c_missoes_unidade", "missões",
                "quests");
            L.Add("rankingcat.c_missoes_descricao", "Missões concluídas nos NPCs (as repetíveis contam cada vez).",
                "Quests completed with NPCs (repeatable ones count every time).");
            L.Add("rankingcat.c_missoes_calculo", "Missões concluídas nos NPCs. As repetíveis contam a cada vez. Contado pelo servidor.",
                "Quests completed with NPCs. Repeatable ones count every time. Counted by the server.");
            L.Add("rankingcat.c_nivel_titulo", "Nível do personagem",
                "Character level");
            L.Add("rankingcat.c_nivel_unidade", "níveis",
                "levels");
            L.Add("rankingcat.c_nivel_descricao", "Nível do personagem (EpicMMO).",
                "Character level (EpicMMO).");
            L.Add("rankingcat.c_nivel_calculo", "Nível do personagem no EpicMMO. Na semana: níveis ganhos.",
                "Character level in EpicMMO. Weekly: levels gained.");
            L.Add("rankingcat.c_online_titulo", "Tempo de jogo",
                "Play time");
            L.Add("rankingcat.c_online_descricao", "Tempo conectado neste mundo, contado pelo servidor.",
                "Time connected to this world, counted by the server.");
            L.Add("rankingcat.c_online_calculo", "Tempo conectado neste mundo, contado pelo servidor de minuto em minuto.",
                "Time connected to this world, counted by the server minute by minute.");
            L.Add("rankingcat.c_mortes_titulo", "Mortes (total)",
                "Deaths (total)");
            L.Add("rankingcat.c_mortes_unidade", "mortes",
                "deaths");
            L.Add("rankingcat.c_mortes_descricao", "Quantas vezes você morreu, de qualquer causa.",
                "How many times you died, from any cause.");
            L.Add("rankingcat.c_mortes_calculo", "Todas as mortes do personagem (contador Deaths do jogo), de qualquer causa. Quem morre mais fica em cima. Na semana: mortes da semana.",
                "All of the character's deaths (the game's Deaths counter), from any cause. Whoever dies the most is on top. Weekly: this week's deaths.");
            L.Add("rankingcat.c_construcoes_titulo", "Peças construídas",
                "Pieces built");
            L.Add("rankingcat.c_construcoes_unidade", "peças",
                "pieces");
            L.Add("rankingcat.c_construcoes_descricao", "Peças construídas com o martelo.",
                "Pieces built with the hammer.");
            L.Add("rankingcat.c_construcoes_calculo", "Cada peça colocada com o martelo conta 1 (contador Builds do jogo). Desmanchar não desconta.",
                "Each piece placed with the hammer counts 1 (the game's Builds counter). Dismantling does not subtract.");
            L.Add("rankingcat.c_fabricacoes_titulo", "Itens fabricados",
                "Items crafted");
            L.Add("rankingcat.c_fabricacoes_unidade", "itens",
                "items");
            L.Add("rankingcat.c_fabricacoes_descricao", "Itens fabricados e melhorados nas bancadas.",
                "Items crafted and upgraded at workbenches.");
            L.Add("rankingcat.c_fabricacoes_calculo", "Cada vez que você fabrica ou melhora um item numa bancada (conta a operação, não a quantidade do lote).",
                "Each time you craft or upgrade an item at a station (counts the operation, not the batch size).");
            L.Add("rankingcat.c_lenhador_titulo", "Árvores derrubadas",
                "Trees felled");
            L.Add("rankingcat.c_lenhador_unidade", "árvores",
                "trees");
            L.Add("rankingcat.c_lenhador_descricao", "Árvores derrubadas.",
                "Trees felled.");
            L.Add("rankingcat.c_lenhador_calculo", "Árvores que você derrubou (contador Tree do jogo: a árvore que cai, não cada machadada).",
                "Trees you felled (the game's Tree counter: the tree that falls, not each axe swing).");
            L.Add("rankingcat.c_mineracao_titulo", "Mineração (golpes)",
                "Mining (hits)");
            L.Add("rankingcat.c_mineracao_unidade", "golpes",
                "hits");
            L.Add("rankingcat.c_mineracao_descricao", "Pedra e minério quebrados com a picareta.",
                "Stone and ore broken with the pickaxe.");
            L.Add("rankingcat.c_mineracao_calculo", "Golpes de picareta em pedra e minério (contador MineHits: cada golpe conta, não a rocha inteira).",
                "Pickaxe hits on stone and ore (MineHits counter: each hit counts, not the whole rock).");
            L.Add("rankingcat.c_domados_titulo", "Animais domados",
                "Animals tamed");
            L.Add("rankingcat.c_domados_unidade", "animais",
                "animals");
            L.Add("rankingcat.c_domados_descricao", "Criaturas domadas.",
                "Creatures tamed.");
            L.Add("rankingcat.c_domados_calculo", "Criaturas que o jogo registrou como domadas no seu personagem (contador CreatureTamed). Atenção: o jogo credita quem é o dono do animal na hora da doma, normalmente o jogador mais próximo, que pode não ser quem o alimentou.",
                "Creatures the game registered as tamed on your character (CreatureTamed counter). Note: the game credits whoever owns the animal at taming time, usually the nearest player, who may not be the one who fed it.");
            L.Add("rankingcat.c_distancia_titulo", "Distância percorrida",
                "Distance traveled");
            L.Add("rankingcat.c_distancia_descricao", "Distância percorrida a pé, de montaria e de barco.",
                "Distance traveled on foot, mounted and by boat.");
            L.Add("rankingcat.c_distancia_calculo", "Tudo o que você se deslocou: a pé, correndo, de montaria, de barco e voando (contador DistanceTraveled). Inclui a distância navegada.",
                "Everything you moved: on foot, running, mounted, by boat and flying (DistanceTraveled counter). Includes sailed distance.");
            L.Add("rankingcat.c_navegacao_titulo", "Distância navegada",
                "Distance sailed");
            L.Add("rankingcat.c_navegacao_descricao", "Distância navegada.",
                "Distance sailed.");
            L.Add("rankingcat.c_navegacao_calculo", "Distância percorrida dentro de um barco (contador DistanceSail), no leme ou como passageiro.",
                "Distance traveled inside a boat (DistanceSail counter), at the helm or as a passenger.");
            L.Add("rankingcat.c_moedas_titulo", "Moedas na bolsa",
                "Coins in purse");
            L.Add("rankingcat.c_moedas_unidade", "moedas",
                "coins");
            L.Add("rankingcat.c_moedas_descricao", "Moedas na bolsa (na semana: quanto juntou).",
                "Coins in purse (weekly: how much you gathered).");
            L.Add("rankingcat.c_moedas_calculo", "Moedas que você carrega agora. Sobe e desce: gastar derruba. Na semana: quanto juntou a mais.",
                "Coins you carry right now. Goes up and down: spending lowers it. Weekly: how much extra you gathered.");
            L.Add("rankingcat.c_mt__unidade", "mortes",
                "deaths");
        }
    }
}
