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
- Substitui o **TombstoneLock** (tumba por jogador), o **Combat** (Detalhes.Combat: status
  "Em combate" e teleporte bloqueado) e o **SaddleStaminaControl** (estamina da sela, agora em
  `[Montarias]` no mesmo cfg).

Config: secoes `PvP*` do `BepInEx/config/Detalhes.Deadheim.cfg`. Vale a do servidor
(ServerSync); o cliente recebe ao conectar. **Salvar o cfg com o servidor ligado ja vale**: o
arquivo e relido (`Shared/ConfigWatcher.cs`) e o ServerSync entrega o valor novo a quem esta
conectado. O mesmo vale para Wards, RaidSystem, VipList e Hearthstone. A versao minima aceita pelo servidor e **7.0.0**
(Deadheim) e **2.1.0** (RaidSystem): as regras rodam no cliente de quem leva o golpe, entao um
cliente sem elas seria alvo sem nenhuma protecao.

## Onde a regra e decidida

O dano de jogador em jogador e decidido no **cliente da vitima** (`Character.RPC_Damage`
roda no dono da ZDO dela), que e o mesmo lugar onde o vanilla ja checa PvP. Cada jogador
publica na propria ZDO se participa de PvP agora (`pvp`) e o motivo (`dh_pvpFlags`:
imune, PK, cacado, protegido, em combate, arena, castelo). Os outros so leem.

O servidor guarda o que precisa sobreviver: K/D, marca de PK e cooldown de desafio, num
JSON por mundo em `BepInEx/config/Deadheim/pvp-<mundo>.json`.

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
| Matador vira PK | sim, salvo arena / castelo / alvo PK / alvo cacado / alvo agressor | - |
| Moedas | `PvpCoinDropPercent` das moedas vai para o chao, fora da tumba (nao na arena) | ficam na tumba |
| Morte dentro de castelo | ninguem perde skill (atacante ou defensor) | perda normal |
| Guilda dona mata invasor no castelo | ganha a recompensa do bioma | - |
| Cacado morre | quem matou ganha a recompensa | desafio acaba sem recompensa |
| PK que morre | perde skill x2 e deixa de ser PK | perde skill x2 e deixa de ser PK |
| Janela "sem perda de skill" do vanilla (10 min depois de morrer) | morte sem perda (arena, castelo) nao abre a janela; PK perde mesmo dentro dela | igual |
| Tumba | so o dono abre | so o dono abre |

O cliente registra cada morte no log (`[Deadheim PvP] Morri: causa=PlayerDirect|PlayerCredit|Pve
matador=<id> ultimoGolpe=<tipo> castelo=<nome> ...`) e o servidor tambem
(`[Deadheim PvP] Morte: <vitima> por <matador|PvE> ...`, e o RaidSystem `[RaidSystem] Abate ...`).

## Regras

| Pedido | Como ficou | Config |
|---|---|---|
| PvP para todos | Ligado fora das zonas seguras; o botao do inventario fica travado | `ForcePvp` |
| Reducao de dano PvP | Dano de jogador em jogador x0.5, aplicado **depois da armadura** (a armadura do Valheim e quadratica: cortar o golpe cru pela metade tirava ~75% do dano de quem usa armadura) | `DamageMultiplier` |
| Reducao de dano em ward | Jogador: dentro de ward **ligado e abastecido** onde voce tem permissao, o dano PvP que voce recebe e x0.5 de novo. Estrutura: ward de jogador segue o `[Wards] DamagePercent` (0 = invulneravel) e a RaidWard o `Ward Damage Reduction %` do RaidSystem | `WardDefenseMultiplier` |
| Dono do territorio sem FF | Mesma guilda nao se fere; e quem tem permissao no mesmo ward nao se fere dentro dele | `NoFriendlyFireGuild`, `NoFriendlyFireTerritory` |
| Guilda ou party | Guilda do mod **Guilds** e grupo do mod **Groups** | `NoFriendlyFireGuild`, `NoFriendlyFireGroup` |
| Morto por jogador | Fica **imune a PvP**: nao da nem leva dano de jogador; PvE normal | `ImmunityMinutes` (10) |
| Ilha inicial safe zone | A terra ligada ao templo inicial (ate o raio), opcionalmente so em certos biomas. Calculada do gerador do mundo, igual em cliente e servidor; o log do servidor mostra os biomas que ela cobre | `StartIslandMode` (Island/Radius/Off), `StartIslandRadius`, `StartIslandBiomes` |
| Transportes safe zone | Barco andando, carroca sendo puxada e montaria andando protegem quem esta neles; barco ou montaria parados nao sao abrigo. Barco, carroca e montaria com sela nao tomam dano de jogador | `TransportsSafe`, `TransportsInvulnerable`, `ShipSafeMinSpeed`, `MountSafeMinSpeed` |
| Montarias | Estamina da sela (Lox, Asksvin) configuravel, valendo na hora para as selas ja carregadas; 0 = valor do jogo. Lembrete: com `LoxTameable = false` o Lox nem e domavel | `[Montarias] MaxStamina`, `RunStaminaDrain`, `SwimStaminaDrain`, `StaminaRegen`, `StaminaRegenHungry` |
| Combate | Dar ou levar dano PvP deixa "em combate": zona segura nao protege e retreat nao funciona | `CombatTagSeconds` (30) |
| Retreat cooldown e combate | `/retreat` com recarga, bloqueado em combate e cacado (a pedra do Hearthstone tambem) | `RetreatCooldownMinutes` (30) |
| Combate | Status "Em combate" com contagem na barra de efeitos. Opcional: apanhar de/bater em monstro tambem conta, so para teleporte (zona segura continua valendo contra jogador) | `CombatStatusIcon`, `CombatFromPve`, `CombatTagSeconds` |
| Fuga de combate | Em combate nenhum teleporte longo funciona (portal, NPC teleportador, pedra, retreat). Deslogar em combate conta morte para quem saiu e abate para quem bateu; com `Death`, ao voltar ele morre onde saiu | `CombatBlocksTeleport`, `CombatLogout` (Off/Rank/Death) |
| PK dobro de perda de skill | Quem mata jogador vira PK; PK que morre perde skill x2 e deixa de ser PK. Matar PK, cacado, agressor, na arena ou no castelo nao gera PK | `PkMinutes`, `PkSkillLossMultiplier`, `PkClearsOnDeath` |
| Legitima defesa | Quem bate primeiro em alguem sem marca vira AGRESSOR (nome e HUD). Matar um agressor nao gera PK | `AggressorRule`, `AggressorSeconds` |
| Contador de PK | Quantos abates deram PK a cada jogador: no `/rank`, no `/pvp`, no HUD e no nome (`[PK x3]`), e no anuncio da morte (`[PK #3]`) | - |
| Arena sem perda de skill | Zonas de arena: PvP sempre, sem perda de skill, sem PK, sem imunidade, fogo amigo liberado, fora do ranking | `ArenaZones`, `ArenaNoSkillLoss`, `ArenaFriendlyFire`, `ArenaCountsLeaderboard` |
| Defesa de castelo | Castelo = zona do RaidSystem. Quem morre para jogador la nao perde skill (qualquer lado), a imunidade nao vale la e ninguem vira PK. A guilda dona que mata invasor ganha moedas pelo bioma | `CastleNoSkillLoss`, `CastleIgnoresImmunity`, `CastleRewardItem`, `CastleRewardByBiome`, `CastleRewardCooldownMinutes` |
| Challenge / Hunted | `/desafio`: em 3 min vira CACADO por 1 h, visivel no mapa de todos, sem zona segura nem imunidade, sem teleporte. Dentro do proprio ward o relogio para e o ward nao reduz o dano; com poucos jogadores online tambem para. Sobreviveu: recompensa. Quem matar: recompensa | `Challenge*`, `HuntedCanUsePortals`, `ChallengePausesInOwnWard`, `HuntedNoWardDefense`, `ChallengeMinPlayers` |
| Leaderboard K/D | `/rank` (todo mundo, com ou sem guilda, com o contador de PK) e o Ranking de Guerra do RaidSystem (PageDown, pontos por guilda), que agora conta os abates de verdade | `LeaderboardSize` |
| Tumba por player | So o dono (e admin) abre a propria tumba | `TombstoneOwnerOnly`, `TombstoneGuildAccess` |
| Tirar raids | Ataques aleatorios de monstros as bases (raids do vanilla) desligados. O RaidSystem continua: e o PvP de castelos | `DisableRandomEvents` |
| Gold sem peso, pilha 5k | Coins pesam 0 e empilham 5000 | `CoinsWeightless`, `CoinsMaxStack` |
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
| `/desafio` / `/desafio cancelar` / `/desafio status` | Desafio |
| `/rank` | Ranking K/D |
| `/pvpadmin zona` | (admin) coordenadas, bioma, zona, castelo, guilda e bandeiras onde voce esta |
| `/pvpadmin imune <min>` / `limpar` / `pk <min>` | (admin) mexe no proprio estado, para testar |

## Teste de ponta a ponta

`Testing/run-pvp-test.ps1` sobe um servidor dedicado local e clientes reais com Deadheim,
VipList, Guilds e RaidSystem, e o `Testing/PvpTestDriver`, que segue um roteiro e escreve
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
  porque o boneco nao entra numa guilda de verdade.
- Sem `-Solo`: dois clientes reais (Alfa e Bravo) em roteiro sincronizado. Precisa de
  memoria para servidor + dois clientes (uns 10 GB de commit livre).
- `-Steps eu-bato,pk,arena`: roda so esses passos do solo (o setup sempre roda).
