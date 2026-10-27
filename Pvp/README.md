# Modulo de PvP do Deadheim

Fica dentro do `Deadheim.dll` (que ja e obrigatorio em todo cliente) e conversa com os mods
que o servidor ja tem, em vez de duplicar:

- **Guilds** (blaxxun): e o cla. Mesma guilda = sem fogo amigo. Lido por reflexao, como o
  Velas faz; sem o Guilds, ninguem e aliado de ninguem.
- **Groups** (blaxxun): a party. Mesmo grupo = sem fogo amigo, tambem por reflexao.
- **ServerCharacters** (blaxxun): o personagem fica no servidor. Sem ele, perda de skill e PK
  x2 se desfazem restaurando o `.fch` do PC.
- **RaidSystem**: e a guerra de castelos. As zonas dele (`Raid Zones`) sao os castelos do
  PvP, e ele recebe do Deadheim cada morte por jogador ja classificada para o Ranking de Guerra.
  Na abertura (17/10/2026) o servidor nao tem castelo (`Raid Zones` vazio): as regras de castelo
  abaixo ficam paradas, so o Ranking de Guerra conta. No lugar dos castelos vem o battleground.
- **NpcValheim**: os NPCs Cacador de Recompensas e Juiz de Guerra sao a tela da bounty e da troca
  PvE/PvP. Chamam o Deadheim por reflexao pela `Pvp/PvpApi.cs` (`PvpApi.Version`); quem decide e
  o servidor, pelo mesmo canal do PvP.
- Substitui o **TombstoneLock** (tumba por jogador), o **Combat** (Detalhes.Combat: status
  "Em combate" e teleporte bloqueado) e o **SaddleStaminaControl** (estamina da sela, agora em
  `[Montarias]` no mesmo cfg).

Config: secoes `PvP*` do `BepInEx/config/Detalhes.Deadheim.cfg`. Vale a do servidor
(ServerSync); o cliente recebe ao conectar. **Salvar o cfg com o servidor ligado ja vale**: o
arquivo e relido (`Shared/ConfigWatcher.cs`) e o ServerSync entrega o valor novo a quem esta
conectado. O mesmo vale para Wards, RaidSystem, VipList e Hearthstone. A versao minima aceita pelo servidor e **7.5.9**
(Deadheim), **2.2.2** (RaidSystem) e **2.1.1** (Hearthstone): as regras rodam no cliente de quem leva o golpe, entao um
cliente sem elas seria alvo sem nenhuma protecao, o protocolo do PvP mudou no 7.3.1, o 7.4.0 traz a Ward de Territorio, o
7.5.1 tira a conversao dos tokens que o 7.5.0 fazia, o 7.5.8 fixa as moedas no inventario e o 7.5.9 traz as respostas
que os NPCs de bounty e de PvE esperam (`bountyboard`, `pveres`).

**Estado (2026-10-14):** o **7.5.9** tira o `/bounty` e o `/pve` do jogador (os dois viram NPCs do NpcValheim,
pela `PvpApi`), deixa o PvE ir e voltar com espera entre trocas, da a todo personagem novo os
`TokensIniciais`, conta o passe de chefe do NpcValheim na faixa por chefes, tira a Ward de Protecao pequena do martelo, volta a Pedra-guardia aos 32 m do jogo e muda padroes para a abertura (ver
"Atualizacao para o 7.5.9"). Vem sobre o **7.5.8** (moedas fixas no slot, nao caem na morte), o
**7.5.1** (tokens de cada construcao: Portal, Spawner e Territory Token; o **Dead Token** e so item, sem uso), o
**7.4.0** (Ward de Territorio) e o 7.3.1 (correcoes da analise de bugs). Os itens nativos do
Deadheim nunca caem como carga (`ClonedItems.IsNativeItem`), com ou sem `PvpCargoKeep`.

## Onde a regra e decidida

O dano de jogador em jogador e decidido no **cliente da vitima** (`Character.RPC_Damage`
roda no dono da ZDO dela), que e o mesmo lugar onde o vanilla ja checa PvP. Cada jogador
publica na propria ZDO se participa de PvP agora (`pvp`) e o motivo (`dh_pvpFlags`:
imune, PK, cacado, protegido, em combate, arena, castelo). Os outros so leem.

O servidor guarda o que precisa sobreviver: K/D, marca e nivel de PK, bounties ativas, quem esta
no PvE (e quando trocou pela ultima vez, para a espera entre trocas) e moedas a entregar, num
arquivo de texto por mundo em `BepInEx/config/Deadheim/pvp-<mundo>.txt` (uma linha por registro,
campos `chave=valor` separados por TAB; edite so com o servidor desligado). O campo do PvE ainda se
chama `pvePermanent`, do tempo em que o PvE era para sempre: renomear apagaria a escolha de quem ja
e PvE. `pveChangedAt` (7.5.9) e a hora da ultima troca; quem nao tem o campo pode trocar na hora.
Ate o 7.2.0 era um JSON do `JsonUtility`, que gravava so o `version`: todo restart zerava K/D, PK,
bounties (com as moedas pagas) e o PvE. O arquivo antigo e ignorado.

## O que o servidor confere (7.3.1)

O cliente decide o dano e as bandeiras, mas nada que vale moeda, ranking ou marca e aceito so
porque o pacote disse:

- **Quem mandou.** O "sender" de um RPC roteado vem escrito pelo cliente e o vanilla nao confere.
  O servidor reescreve o remetente de todo RPC que recebe com o dono da conexao
  (`RemetenteRpc`) antes de qualquer leitura, e o repasse aos outros clientes leva o certo. Um
  cliente nao se passa por admin, por outro jogador nem pelo servidor (o canal do PvP e as
  respostas do RaidSystem no cliente so valem vindos do servidor).
- **A morte.** Uma por vitima a cada 10 s, e so vale quando a ZDO dela aparece morta no servidor.
  O matador tem que ser um jogador conectado (`KillerMustBeOnline`) a no maximo
  `KillerMaxDistance` (300 m). Arena, castelo e defesa do castelo sao calculados pela posicao que
  o servidor ve. Matador que nao passa vira PvE (e o log diz o motivo).
- **Moeda.** A bounty: o cliente tira as moedas e guarda o que tirou pelo id do pedido; o servidor
  responde quanto ficou e o cliente devolve o resto a si mesmo. O servidor nao cria moeda em
  recusa nem troco. O pote e confiado ao cliente, com teto por pagador por dia
  (`BountyDailyCapPerPlayer`). Recompensa de defesa tem teto por defensor por dia
  (`CastleRewardDailyCap`). Moeda pendente grava na hora.
- **O relogio.** Imunidade, PK, cacado, recarga do retreat e a janela de raid do castelo nao
  dependem da hora do Windows: os tempos do servidor contam pelo relogio monotonico do jogo, a
  imunidade e o retreat sao gravados como tempo restante (contam so com o jogo aberto) e a hora
  do castelo vem do servidor (`RelogioServidor`).
- **Conquista do castelo** (RaidSystem): a RaidWard tem que sumir de verdade no servidor e o
  atacante tem que estar conectado e perto.

## Morte por jogador x morte por PvE

Uma regra so (`PvpRules.ClassifyDeath`), usada por tudo que reage a morte:

1. O golpe que matou tem atacante e ele e um **jogador** (conferido pela ZDO, entao vale
   mesmo se o matador estiver longe ou ja tiver saido) -> **morte por jogador**.
2. Senao, se a vitima levou dano de jogador ha menos de `KillCreditSeconds` (20 s), ou ainda
   esta com veneno/fogo que um jogador deixou -> **morte por jogador**, credito para quem bateu
   (queda, afogamento, o tique do veneno ou mob terminando o servico).
3. Senao -> **PvE**.

| | Morte por jogador | Morte por PvE |
|---|---|---|
| Imunidade a PvP | sim, fora de arena e castelo | nao |
| Conta no `/rank` (K/D) | sim, fora da arena | nao |
| Conta no Ranking de Guerra (RaidSystem) | sim, entre guildas diferentes, fora da arena | nao |
| Matador vira PK | sim, salvo arena / castelo em raid / alvo PK / alvo cacado / alvo agressor | - |
| Saque | metade da carga (`PvpCargoDropPercent`: minerio, metal, comida, trofeu) vai para o chao, fora da tumba, seja PK ou nao; o equipado fica na tumba (nao na arena). As moedas ficam com o jogador (7.5.8, `Moedas.cs`). Tokens e o que se compra com Deadcoins nunca caem (`PvpCargoKeep`); um item que nao consegue cair fica na tumba | tudo na tumba, menos as moedas |
| Morte dentro de castelo | ninguem perde skill (atacante ou defensor) | perda normal |
| Guilda dona mata invasor no castelo | ganha a recompensa do bioma | - |
| Alvo de bounty morre | a bounty acaba; quem matou leva `BountyKillerSharePercent` do pote se o golpe final foi dele e ele nao e (nem foi nas ultimas `BountyAllyHours`) da guilda do alvo; senao o pote fica com a casa | a bounty continua |
| PK que morre | perde pelo nivel (skill x2, e itens no Unequipped/All) e deixa de ser PK | perda normal do jogo (pelo nivel so com `PkPenaltyOnPveDeath`, desligado desde a 7.5.9) e **continua PK** (`PkClearsOnPveDeath`) |
| Janela "sem perda de skill" do vanilla (10 min depois de morrer) | morte sem perda (arena, castelo) nao abre a janela; PK perde mesmo dentro dela | igual |
| Protecao do Treinador (NpcValheim, `Treinador.cs`) | nao vale com `[Trainer] ProtectionPveOnly` (padrao) e nao e gasta; desligado, corta a perda normal. **Nunca reduz a pena de PK** | corta `ProtectionFactor` (0.5) da perda e gasta uma protecao; o PK que paga pelo nivel paga inteiro. Morte sem perda (arena, castelo, janela do vanilla) nao gasta |
| Tumba | so o dono abre | so o dono abre |

O cliente registra cada morte no log (`[Deadheim PvP] Morri: causa=PlayerDirect|PlayerCredit|Pve
matador=<id> ultimoGolpe=<tipo> castelo=<nome> ...`) e o servidor tambem
(`[Deadheim PvP] Morte: <vitima> por <matador|PvE> ...`, e o RaidSystem `[RaidSystem] Abate ...`).

## Regras

| Pedido | Como ficou | Config |
|---|---|---|
| PvP para todos | Ligado fora das zonas seguras; o botao do inventario fica travado | `ForcePvp` |
| Reducao de dano PvP | Dano de jogador em jogador x0.5, aplicado **depois da armadura** (a armadura do Valheim e quadratica: cortar o golpe cru pela metade tirava ~75% do dano de quem usa armadura) | `DamageMultiplier` |
| Reducao de dano em ward | Jogador: dentro de ward **ligado e abastecido** onde voce tem permissao, o dano PvP que voce recebe e x0.5 de novo. Estrutura: ward de jogador segue o `[Wards] DamagePercent` (0 = invulneravel), a Ward de Territorio ativa nao deixa dano nenhum (nem do dono nem de quem tem acesso; desmontar com o martelo continua liberado) e a RaidWard segue o `Ward Damage Reduction %` do RaidSystem | `WardDefenseMultiplier` |
| Dono do territorio sem FF | Mesma guilda nao se fere; e quem tem permissao no mesmo ward nao se fere dentro dele | `NoFriendlyFireGuild`, `NoFriendlyFireTerritory` |
| Guilda ou party | Guilda do mod **Guilds** e grupo do mod **Groups** | `NoFriendlyFireGuild`, `NoFriendlyFireGroup` |
| Morto por jogador | Fica **imune a PvP**: nao da nem leva dano de jogador; PvE normal. Conta so com o jogo aberto | `ImmunityMinutes` (10) |
| Ilha inicial safe zone | A terra ligada ao templo inicial (ate o raio), opcionalmente so em certos biomas. Calculada do gerador do mundo, igual em cliente e servidor; o log do servidor mostra os biomas que ela cobre | `StartIslandMode` (Island/Radius/Off), `StartIslandRadius`, `StartIslandBiomes` |
| Transportes | **Carga em transito e alvo**: por padrao carroca, montaria e barco nao protegem quem esta neles e tomam dano de jogador (quebrar a carroca derruba o que ela leva). `TransportsSafe`/`TransportsInvulnerable` (carroca e montaria) e `ShipsSafe`/`ShipsInvulnerable` (barco) voltam a protecao | `TransportsSafe`, `TransportsInvulnerable`, `ShipsSafe`, `ShipsInvulnerable`, `ShipSafeMinSpeed`, `MountSafeMinSpeed` |
| Montarias | Estamina da sela (Lox, Asksvin) configuravel, valendo na hora para as selas ja carregadas; 0 = valor do jogo. | `[Montarias] MaxStamina`, `RunStaminaDrain`, `SwimStaminaDrain`, `StaminaRegen`, `StaminaRegenHungry` |
| Combate | Dar ou levar dano PvP deixa "em combate": zona segura nao protege e retreat nao funciona | `CombatTagSeconds` (30) |
| Retreat cooldown e combate | `/retreat` com recarga (conta com o jogo aberto; so gasta se o teleporte sair), bloqueado em combate (com jogador ou monstro) e cacado; a pedra do Hearthstone tambem (2.1.1+: recusada, ela nao some). O cacado so usa se `HuntedCanUsePortals` | `RetreatCooldownMinutes` (30), `RetreatBlockedByPveCombat`, `HuntedCanUsePortals` |
| Buffs de estado | Cada estado do PvP e um buff na barra de efeitos do jogo, com contagem: **Em combate** (ou **Luta com monstro**, quando ela bloqueia o retreat), **Imune a PvP**, **PK** (com o contador, `PK x3`, ou `PK permanente`), **Agressor**, **Cacado**, **Bounty** (o aviso), **Zona segura** (com o nome da zona) e o titulo do **PvE**. A linha no topo da tela continua resumindo | `CombatStatusIcon`, `StateBuffs` |
| Combate | Opcional: apanhar de/bater em monstro tambem conta para todo teleporte (zona segura continua valendo contra jogador); so para o retreat e a pedra ja vale por padrao | `CombatFromPve`, `RetreatBlockedByPveCombat`, `CombatTagSeconds` |
| Fuga de combate | Em combate nenhum teleporte longo funciona (portal, NPC teleportador, pedra, retreat). Deslogar em combate conta morte para quem saiu e abate para quem bateu e, ao voltar, ele morre onde saiu (`Death`, padrao desde a 7.5.9); com `Rank` so conta no ranking | `CombatBlocksTeleport`, `CombatLogout` (Off/Rank/Death) |
| PK por niveis | Quem mata jogador vira PK. O nivel sobe com os abates seguidos (padrao: 1 = 1 h, 2 = 2 h, 3+ = 24 h, 5+ = permanente ate ser morto por jogador, no mapa de todos). **O tempo so corre online** (deslogar nao limpa), **o PK nao tem zona segura nem transporte** (quem esta na zona pode ataca-lo) e **so sai morto por jogador** (morrer de PvE nao limpa). Ao morrer marcado perde pelo nivel: `Skills` (skill x2), `Unequipped` (+ tudo que nao esta equipado cai no chao), `All` (+ o inventario inteiro cai no chao). Matar PK, cacado, agressor, na arena ou no castelo em raid nao gera PK | `PkTiers`, `PkSkillLossMultiplier`, `PkClearsOnDeath`, `PkClearsOnPveDeath`, `PkTimeOnlineOnly`, `PkNoSafeZone`, `PkPenaltyOnPveDeath`, `PkPermanentOnMap` |
| Legitima defesa | Quem bate primeiro em alguem sem marca vira AGRESSOR por 10 min (nome e HUD); o relogio para enquanto ele esta em combate com jogador. Revidar em quem bateu em voce nesse tempo e defesa, mesmo antes de a marca dele chegar. Matar um agressor nao gera PK, e o agressor que morre nao perde nada a mais | `AggressorRule`, `AggressorSeconds`, `AggressorPausesInCombat` |
| Contador de PK | Quantos abates deram PK a cada jogador: no `/rank`, no `/pvp`, no HUD e no nome (`[PK x3]`), e no anuncio da morte (`[PK #3]`) | - |
| Arena sem perda de skill | Zonas de arena: PvP sempre, sem perda de skill, sem PK, sem imunidade, fogo amigo liberado, fora do ranking | `ArenaZones`, `ArenaNoSkillLoss`, `ArenaFriendlyFire`, `ArenaCountsLeaderboard` |
| Defesa de castelo | Castelo = zona do RaidSystem. Quem morre para jogador la nao perde skill (qualquer lado), a imunidade nao vale la e ninguem vira PK. A guilda dona que mata invasor ganha moedas pelo bioma, uma vez por invasor no cooldown (salvo, o restart nao zera) e ate o teto diario por defensor | `CastleNoSkillLoss`, `CastleIgnoresImmunity`, `CastleRewardItem`, `CastleRewardByBiome`, `CastleRewardCooldownMinutes`, `CastleRewardDailyCap` (500) |
| Faixa por chefes | Dois jogadores so se ferem se a diferenca de chefes derrotados for no maximo `BossGapMax` (1): quem matou a Massa Ossea (3) luta com quem esta entre o Anciao (2) e a Moder (4). O nivel e o chefe mais avancado que o personagem ajudou a matar (o perfil do jogo conta a morte do chefe para quem deu dano) ou, desde a 7.5.9, cujo passe de chefe do NpcValheim ele tem, ganho ou comprado no Mestre dos Passes (`PvpBossPass`, pela `BossPassApi`). Fora da faixa ninguem fere ninguem e o nome mostra `[outra faixa]`; na arena nao vale. Protege o novato do veterano | `BossGapMax`, `BossOrder`, `BossPassCounts` |
| Bounty / Hunted | No NPC Cacador de Recompensas (NpcValheim, pela `PvpApi`; o `/bounty` saiu na 7.5.9): minimo 1000, sai do inventario: o alvo e avisado e 10 min depois vira CACADO, no mapa de todos, sem zona segura nem imunidade, sem teleporte. Tempo = 1 h por 1000 moedas, contado so com o alvo online; a partir de 5000, ate morrer. Quem matar leva 75% do pote, o resto e da casa. O alvo pode pagar 1,5x o pote para a casa e se livrar (no mesmo NPC). Sem recompensa por sobreviver (nada sai do nada). Dentro do proprio ward o relogio para e o ward nao reduz o dano; com poucos jogadores online tambem para. Cada jogador poe no maximo `BountyDailyCapPerPlayer` (10000) por dia. So golpe final de jogador paga, e nunca a quem e ou foi da guilda do alvo nas ultimas `BountyAllyHours` (24 h): o pote fica com a casa. Admin: `/pvpadmin bounty <jogador> <moedas>` (a casa paga) | `Bounty*`, `HuntedCanUsePortals`, `HuntedNoWardDefense` |
| Leaderboard K/D | `/rank` (todo mundo, com ou sem guilda, com o contador de PK) e o Ranking de Guerra do RaidSystem (PageDown, pontos por guilda), que agora conta os abates de verdade | `LeaderboardSize` |
| Tumba por player | So o dono (e admin) abre a propria tumba | `TombstoneOwnerOnly`, `TombstoneGuildAccess` |
| Tirar raids | Ataques aleatorios de monstros as bases (raids do vanilla) desligados. O RaidSystem continua: e o PvP de castelos | `DisableRandomEvents` |
| Stagger no PvP | O cambalear de golpe de jogador em jogador pode ser reduzido | `StaggerMultiplier` |
| Base raidavel | Fora da zona segura, o que o ward cobre toma `DamagePercent` do dano (padrao 25%); na zona segura (`SafeArea`: 1500 m do spawn, ilha inicial, SafeZones) e sempre 0. Wards sem limite por jogador. Desde a 7.5.9 a Ward de Protecao pequena (`DeadheimWard`) sai do martelo (`PlayerWardEnabled = false`; as que existem continuam valendo): ficam a Pedra-guardia do jogo, com raio `WardRadius` (32 m desde a 7.5.9, o do jogo; era 150), e a Ward de Territorio | `[Wards] DamagePercent`, `[Wards] PlayerWardEnabled`, `[Server config] WardLimit`, `[Server config] WardRadius` |
| Ward de Territorio | **Territory Token** (`TerritoryToken`): todo personagem recebe 1 ao entrar (`TokensIniciais`, 7.5.9) e o token volta ao remover. Ele constroi a **Ward de Territorio** (`DeadheimTerritoryWard`, no martelo), 1 por jogador. Nos 1500 m do spawn (`[Server config] SafeArea`) a ward comum ja protege 100%, entao ali ela nem pode ser colocada; fora dali, abastecida e depois de `TerritoryWardActivationMinutes` (60 min com o servidor ligado), nada no raio (`TerritoryWardRadius`, 32 m desde a 7.5.9) toma dano de quem nao tem acesso. Ate ativar, vale como ward comum: e a recarga de mudar de lugar, e impede plantar a ward no meio de um raid. Nao entra em zona de raid; fica a 2 raios de outra Ward de Territorio, mesmo da propria guilda. So o dono remove (a guilda tem acesso, mas nao desmonta); o token volta ao remover (`TerritoryTokenRecover`). Nao e zona segura de PvP: quem esta dentro ainda morre | `[Wards - Territorio]` |
| Dead Token | Moeda do Deadheim (`DeadToken`): moeda grossa de ouro com um valknut (no inventario e no chao), clonada das moedas do jogo; nao vende no mercador e nunca cai como carga. Por enquanto so existe o item: nenhuma construcao custa Dead Token, e os tokens de cada construcao continuam valendo | - |
| Castelo | O castelo so e zona de guerra na janela de raid do RaidSystem; quando a RaidWard cai, vira zona segura ate a janela fechar. Guilda que segura o castelo a janela inteira ganha cargas de tributo e pontos de defesa | RaidSystem `3 - PvP`, `Defense Tribute Charges`, `Points Per Defense` |
| PvE (Juiz de Guerra) | No NPC Juiz de Guerra (NpcValheim, pela `PvpApi`; o `/pve` saiu na 7.5.9) o jogador vira `PveTitle` (padrao Mercador) e sai do PvP, ou volta a ele, com `PveSwitchCooldownHours` (24 h) entre trocas; em todo lugar (arena e castelo inclusive); nao pode receber bounty. **Sem bonus**: sobe skill x0.5 e coleta 1x, enquanto quem joga PvP coleta 2x (arvore, pedra, minerio, colheita, drop de monstro). PK, quem tem bounty e quem esta em combate nao podem virar. O admin ajusta os atributos no menu do NPC (aba Atributos, pelo canal dos Ajustes) e tira alguem do PvE com `/pvpadmin pve <jogador>` | `PveEnabled`, `PveSwitchCooldownHours`, `PveTitle`, `PveSkillMultiplier`, `PvpSkillMultiplier`, `PveResourceRate`, `PvpResourceRate` |
| Bonus de monstro so para aliados | O bonus de vida e dano do monstro por jogador perto (jogo e CreatureLevelControl) so conta quem luta e o grupo e a guilda dele: estranho passando perto nao deixa o monstro mais duro | `[Server config] MonsterScalingAlliesOnly` |
| Moedas fixas | Desde a 7.5.8 as Coins ficam numa pilha so no primeiro slot da segunda linha, pesam 0 e empilham 100 milhoes; nao vao para bau, chao nem tumba e nao saem em morte nenhuma. Substitui `CoinsWeightless`, `CoinsMaxStack` e `PvpCoinDropPercent`, que sairam | - (`Moedas.cs`) |
| Tokens iniciais | Cada personagem recebe uma vez os itens da lista ao entrar (padrao 1 Portal Token e 1 Territory Token); a marca `dh_tokensIniciais` no `m_customData` impede a segunda entrega, mesmo se a lista mudar | `[Server config] TokensIniciais` |
| Ward sem quebrar chao, pedra e arvore | Dentro do ward alheio: sem picareta/enxada no terreno, sem quebrar pedra, arvore, tronco e toco. Veio de minerio fica livre (senao guilda tranca os veios com ward), e a protecao pode ficar so perto do ward | `[Wards] ProtectTerrain`, `ProtectNature`, `ProtectNatureOres`, `NatureOreDrops`, `ProtectNatureRadius` |

Formato das zonas (arena e seguras): `Nome,x,z,raio|Nome2,x,z,raio`. Castelos sao configurados
no RaidSystem (`Raid Zones`). O servidor escreve no log, ao subir, onde fica o templo inicial e
o tamanho da zona segura calculada:

```
[Deadheim PvP] Templo inicial em x=3 z=5. Zona segura inicial: ... celulas=3674 (~3,76 km2)
```

## Comandos (chat)

| Comando | O que faz |
|---|---|
| `/pvp` | Seu estado de PvP, sua guilda, o castelo onde voce esta e ajuda |
| `/rank` | Ranking K/D |
| `/pvpadmin zona` | (admin) coordenadas, bioma, zona, castelo, guilda e bandeiras onde voce esta |
| `/pvpadmin imune <min>` / `limpar` | (admin) mexe na propria imunidade, para testar. Minutos com ponto ou virgula |
| `/pvpadmin pk <min> [jogador]` | (admin) o servidor marca PK por `<min>` minutos online, permanente (`-1`) ou limpa (`0`); sem jogador, o proprio admin. Sem o numero, so mostra o uso |
| `/pvpadmin bounty <jogador> <moedas>` | (admin) bounty paga pela casa, pode ser em si mesmo |
| `/pvpadmin pve <jogador>` | (admin) tira o jogador do PvE na hora, sem esperar a espera entre trocas |

## Teste de ponta a ponta

`Testing/run-pvp-test.ps1` sobe um servidor dedicado local e clientes reais com Deadheim,
VipList, Guilds, RaidSystem e Hearthstone, e o `Testing/PvpTestDriver`, que segue um roteiro e escreve
`[PVPTEST] PASS|FAIL` no log do cliente. Nada e instalado nas pastas do jogo; os personagens
de teste ficam numa pasta propria e as preferencias do Valheim sao restauradas no fim. O
script cria um castelo de teste (`CasteloTeste`, dono `Lobos`) do lado do templo.

```
powershell -ExecutionPolicy Bypass -File Testing\run-pvp-test.ps1 -Root D:\tmp\pvptest -Solo
```

- `-Solo`: um cliente real + um segundo jogador ("Dummy") criado pelo driver. Cabe numa
  maquina comum; e o modo usado para validar o modulo. O driver le o log do servidor (mesma
  maquina) para conferir o que o cliente nao ve: quem virou PK, quem ganhou recompensa, o que
  o RaidSystem registrou. A guilda de cada um e dada pelo teste (`PvpGuilds.TestOverride`),
  porque o boneco nao entra numa guilda de verdade. O cfg do teste solo desliga
  `KillerMustBeOnline`: o Dummy nao e um jogador conectado.
- Sem `-Solo`: dois clientes reais (Alfa e Bravo) em roteiro sincronizado. Precisa de
  memoria para servidor + dois clientes (uns 10 GB de commit livre).
- `-Steps eu-bato,pk,arena`: roda so esses passos do solo (o setup sempre roda).
- `-Admin -AdminId <SteamID64>`: o cliente vira admin. Precisam disso a bounty (`bounty`, `bounty-pagar`,
  `bounty-expira`) e a parte do servidor do `pk-sem-protecao`; admin passa por cima de ward, entao rode so
  esses passos assim. A pausa da bounty no proprio ward usa o ward que o passo `territorio` cria: rode os dois juntos.
- Fotos de alguns passos (`ajustes`, `forja`, `tokens`) ficam em `<Root>\fotos`.

`Testing/PvpSemJogo` testa sem o jogo o arquivo de estado (ida e volta, arquivo editado a mao, o
JSON antigo), o relogio de PK, a devolucao e o teto da bounty, a entrega de item com inventario
cheio, os tetos diarios, o historico de guilda e a leitura de numero com ponto ou virgula:
`dotnet run -c Release` na pasta. `Testing/AjustesSemJogo` testa a janela de ajustes e o
remetente de verdade dos RPCs.

## Atualizacao para o 7.3.1 (cfg do servidor)

Nenhum padrao de chave existente mudou, entao nada precisa ser trocado a mao no cfg do servidor.
As chaves novas entram sozinhas com o padrao na primeira subida: `[PvP] KillerMustBeOnline`
(true), `[PvP] KillerMaxDistance` (300), `[PvP - Bounty] BountyDailyCapPerPlayer` (10000),
`[PvP - Bounty] BountyAllyHours` (24) e `[PvP - Castelo] CastleRewardDailyCap` (500). Os itens do
proprio Deadheim (tokens, Garantia de Refino, kits) deixaram de cair como carga pelo codigo, sem
depender de `PvpCargoKeep`. O `pvp-<mundo>.txt` ganha campos novos (versao 4) e continua lendo o
arquivo antigo. Imunidade e recarga do retreat no formato antigo (horario) sao convertidas na
primeira entrada de cada personagem.

## Atualizacao para o 7.5.9 (cfg do servidor)

Aqui padroes mudaram, e o BepInEx nao troca um valor que ja esta escrito no cfg: num servidor que
ja subiu com uma versao anterior, grave estes a mao (ou pelo menu de ajustes do servidor):

| Chave | Antes | 7.5.9 |
|---|---|---|
| `[PvP - Fuga] CombatLogout` | `Rank` | `Death` |
| `[PvP - PK] PkPenaltyOnPveDeath` | `true` | `false` |
| `[Wards] PlayerWardEnabled` | `true` | `false` |
| `[Wards - Territorio] TerritoryWardRadius` | `20` | `32` |
| `[Server config] WardRadius` | `150` | `32` |

Entram sozinhas: `[Server config] TokensIniciais` (`PortalToken:1,TerritoryToken:1`), `[PvP - Chefes] BossPassCounts` (true),
`[PvP - PvE permanente] PveSwitchCooldownHours` (24) e `PvpSkillMultiplier` (1). A secao do PvE
guarda o nome antigo de proposito, para nao perder os valores ja ajustados. O `pvp-<mundo>.txt`
ganha o campo `pveChangedAt` e continua lendo o arquivo antigo.
