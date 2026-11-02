# Ranking do mundo

Abre por três caminhos, todos na mesma janela:

| Onde | Como |
| --- | --- |
| **Tab** (inventário) → Central Deadheim → **Ranking** | o botão mostra a sua posição na Lenda (`Você é #12 de 87 na Lenda`) |
| **ESC → Ranking** | logo abaixo de ESC → Deadheim; o ESC fecha o ranking e volta ao menu |
| `/ranking` | no chat |

A janela segue o visual das telas dos NPCs (`Shared/ValheimUi`: painel de madeira, fontes Norse e
Averia, botões e ícones do jogo).

## O que a janela tem

- **Categorias** (esquerda), agrupadas, cada uma com a sua posição nela (top 3 em ouro, prata e bronze):
  - Geral: **Lenda**, que junta tudo com pesos (`PesosLenda`); o detalhe diz de onde vêm os pontos de cada um (`chefes 40% · caça 30% · PvP 15%`).
  - Combate: Abates PvP (com K/D e PK), Guerra (pontos de guilda do RaidSystem), Arena (rating do NpcValheim), Chefes, Caçador.
  - Progresso: Habilidades (soma dos níveis), Missões (NpcValheim), Nível (só com EpicMMO), Tempo de jogo.
  - Ofício: Construtor, Artesão, Lenhador, Minerador, Domador.
  - Exploração: Andarilho (distância), Navegador.
  - Riqueza: Fortuna (moedas; `MostrarRiqueza`).
  - Mundo: **Feitos do mundo**, o primeiro a derrotar cada chefe, com data e quantos já derrotaram.
- **Temporada / Semana**: a semana começa na segunda 00:00 do fuso do servidor (`FusoHoras`) e conta só o que cresceu nela.
- **Vikings / Guildas**: qualquer categoria por guilda (soma dos membros, com o destaque de cada uma).
- **Filtro**: todos, só online, minha guilda. **Busca** por nome ou guilda. As posições continuam as do ranking inteiro.
- **Pódio** dos três primeiros e tabela com a **seta do dia** (`+2` subiu, `-1` desceu, `novo`, comparando com a meia-noite).
- **A sua linha**, sempre visível embaixo: posição, valor, a seta e **quanto falta para passar quem está na sua frente** (`faltam 56 pts para passar Bia (#11)`), com o botão "Ir para mim".
- **Ficha do viking**: clique em qualquer nome (tabela ou pódio) para ver a posição dele em todas as categorias, os feitos, a guilda, se é Aesir e quando foi visto.
- Atalhos: setas ↑/↓ trocam a categoria; ESC fecha a ficha e depois a janela.
- Online com o ponto verde, Aesir com a marca dourada, guilda entre colchetes.

## De onde vêm os números

O ranking já nasce cheio: na subida, o servidor importa o que o mundo tem guardado, de todos os
personagens, online ou não (`RankingImportar.cs`), e depois mantém atualizado.

| Número | Fonte | Atualiza |
| --- | --- | --- |
| Monstros, mortes, construções, fabricações, árvores, mineração, distância, navegação, domados, chefes | **perfil de cada personagem guardado no servidor** (`.fch` do ServerCharacters: os contadores `m_playerStats` que o jogo já registra) | a cada 5 min, só os arquivos que mudaram; e pelo relato do cliente a cada minuto com o jogador online |
| Habilidades (soma e maior), moedas | o mesmo perfil: o pacote do personagem, lido com os leitores do próprio ServerCharacters (`ConsumePlayerSaveUntilSkills/Inventory`) | idem |
| Quando chegou / último jogo | data de criação / gravação do `.fch`, e o diretório de jogadores do NpcValheim | idem, e a cada 2 min |
| Aesir de quem está offline | o id do arquivo (`Steam_<id>_<nome>.fch`) na VipList | a cada importação |
| Abates e mortes de PvP, contador de PK, guilda | `PvpStore` do servidor | a cada minuto |
| Pontos e conquistas da guerra | `RaidSystem.ScoreManager`, por reflexão (sem RaidSystem a categoria some) | a cada minuto |
| **Arena** (maior rating pessoal, vitórias e partidas da temporada) | motor da arena do NpcValheim (times em memória), por reflexão | a cada 2 min |
| **Missões** concluídas (repetíveis contam cada vez) | `QuestDatabase` do NpcValheim, por reflexão | a cada 2 min |
| Tempo de jogo | o servidor conta quem está conectado | a cada 10 s |
| Dia do mundo e chefes já derrotados no mundo | relógio do mundo e chaves globais `defeated_*` | a cada foto |
| Nível (EpicMMO) | relato do cliente | a cada minuto |

Sem ServerCharacters o importador não acha perfis e o ranking usa só os relatos de quem entra; sem
NpcValheim, Arena e Missões não aparecem. O log do servidor diz o que foi importado
(`Ranking: N perfil(is) de personagem importado(s)`).

O que o servidor confere em cada relato (`RankingRegras`, testado em `Testing/RankingSemJogo`):

- nada que é do servidor é aceito do cliente, e chave desconhecida é ignorada;
- contador nunca desce e sobe no máximo o que dá para jogar no tempo desde o último relato
  (ex.: 10 monstros/s, 120 m/s de distância, com folga mínima de 2 min);
- o id vem da ZDO do personagem de quem mandou (`PvpPeer`, com o remetente já conferido pelo `RemetenteRpc`), nunca do pacote;
- o perfil importado do disco é do próprio servidor e vira a base: o relato do cliente só pode
  crescer a partir dele, com o teto por segundo;
- sem perfil no servidor, o primeiro relato de um personagem entra inteiro (é o histórico dele);
- o histórico (primeira importação ou primeiro relato) **não conta na semana**.

Se alguém trapacear no primeiro relato (só possível sem ServerCharacters), o admin tira com
`/rankingadmin ocultar <nome>` (volta com `mostrar`; `apagar` zera os dados e recomeça o histórico).

## Feitos do mundo

Quando um relato ou uma importação mostra um chefe que o personagem não tinha e ninguém ainda tem o
feito, ele fica com o feito, o servidor anuncia para todos (centro da tela e chat) e o log registra.
Chefe que já estava no histórico (primeira importação ou primeiro relato) vira feito "antes do
ranking", sem anúncio. Chefe que o mundo já derrotou (chave global) sem ninguém registrado aparece
como "derrotado, sem registro". O cabeçalho mostra o dia do mundo e quantos chefes caíram. A ordem
é a do `[PvP - Chefes] BossOrder`.

## Config (`Detalhes.Deadheim.cfg`, seção `[Ranking]`, sincronizada)

| Chave | Padrão | O que faz |
| --- | --- | --- |
| `RankingAtivo` | true | Liga o ranking (Central, ESC e `/ranking`) |
| `PesosLenda` | ver abaixo | Quanto vale cada unidade na Lenda |
| `MostrarRiqueza` | true | Mostra a categoria Fortuna |
| `FusoHoras` | -3 | Fuso da virada da semana (segunda 00:00) e da seta do dia (meia-noite) |
| `DiasInativo` | 60 | Quem não entra há mais dias sai da lista (os dados ficam e voltam quando ele entrar). 0 = nunca sai |

`PesosLenda` padrão:
`skills=2|chefes=150|chefes.k=10|monstros=1|pvp.k=15|guerra=5|arena.v=20|missoes=25|build=0.2|craft=0.5|arvores=0.5|mina=0.1|dist=0.002|mar=0.004|domados=5|online=0.0005`
(cfg que já existe guarda o valor antigo: acrescente `arena.v=20|missoes=25` na aba Admin para contar arena e missões na Lenda)
(1 nível de habilidade = 2 pts, chefe diferente = 150, 500 m andados = 1, 1 hora conectado = 1,8...).
Moedas ficam fora da Lenda de propósito.

## Arquivos

| Arquivo | O que faz |
| --- | --- |
| `RankingDados.cs` | Núcleo sem Unity: registros, regras de relato, semana, categorias, classificação, guildas, números no jeito brasileiro e o arquivo |
| `RankingPerfil.cs` | Lê um `PlayerProfile`: contadores, chefes, habilidades e moedas (cliente e servidor) |
| `RankingImportar.cs` | Servidor: importa os perfis do ServerCharacters, arena, missões e diretório do NpcValheim, dia e chefes do mundo |
| `RankingRede.cs` | Config, RPCs `DH_Ranking_ToServer` / `DH_Ranking_ToClient`, coleta no cliente, servidor (relatos, PvP, guerra, tempo online, virada da semana e do dia, feitos, foto comprimida), `/ranking` e `/rankingadmin` |
| `RankingJanela.cs` | A janela e a ficha |

Servidor: `BepInEx/config/Deadheim/ranking-<mundo>.txt`, uma linha por registro (`R` cabeçalho,
`J` jogador, `F` feito), campos `chave=valor` separados por TAB, salvo a cada 2 min, com o mundo e
ao desligar. Edite só com o servidor desligado. Linha ilegível é guardada num `.linhas-ignoradas-*`
antes de qualquer save.

A foto que vai ao cliente é montada uma vez a cada 8 s e dividida entre quem pede (refeita na hora
para quem acabou de relatar), comprimida, com as chaves numa tabela. O cliente classifica sozinho:
trocar categoria, período, visão, filtro ou busca não usa a rede. Com a janela aberta ela se renova
a cada 20 s.

## Testes

- **Sem o jogo** (`Testing/RankingSemJogo`, 65 checagens; inclui a importação do perfil: entra inteiro, vira base do teto do relato, contador fica com o maior, medida não troca a do jogador online, chefe novo em importação seguinte é feito de verdade, arena só com partida): semana e dia no fuso, teto por segundo,
  contador que não desce, chave do servidor recusada, primeiro relato, feitos, virada da semana,
  setas, empate, guildas, Lenda, números e o arquivo (ida e volta, linha quebrada, nome com TAB).

      dotnet run -c Release --project Testing\RankingSemJogo

- **No jogo** (falta): abrir pela Central, pelo ESC e por `/ranking`; trocar categoria, período,
  visão, filtro e busca; ficha; ESC; conferir no log do cliente a linha
  `Ranking: contadores do perfil: ...` (os nomes que o jogo usa) e, no servidor, as linhas
  `Ranking: N perfil(is) de personagem importado(s)` e `habilidades e moedas dos perfis offline lidas
  pelo ServerCharacters`, e o `ranking-<mundo>.txt`.
