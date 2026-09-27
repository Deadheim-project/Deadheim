# Modulo de PvP do Deadheim

Tudo dentro do `Deadheim.dll` (que ja e obrigatorio em todo cliente). Nenhum mod novo.
Substitui: **TombstoneLock**, **Guilds**, **Groups** e o **RaidSystem** (raids).

Config: secoes `PvP*` do `BepInEx/config/Detalhes.Deadheim.cfg`. Vale a do servidor
(ServerSync); o cliente recebe ao conectar. A versao minima aceita pelo servidor passou a
ser **7.0.0**: um cliente sem o modulo seria alvo sem nenhuma das protecoes abaixo.

## Onde a regra e decidida

O dano de jogador em jogador e decidido no **cliente da vitima** (`Character.RPC_Damage`
roda no dono da ZDO dela), que e o mesmo lugar onde o vanilla ja checa PvP. Cada jogador
publica na propria ZDO se participa de PvP agora (`pvp`) e o motivo (`dh_pvpFlags`:
imune, PK, cacado, protegido, em combate, arena). Os outros so leem.

O servidor guarda o que precisa sobreviver: K/D, marca de PK, cooldown de desafio e clas,
num JSON por mundo em `BepInEx/config/Deadheim/pvp-<mundo>.json`.

## Morte por jogador x morte por PvE

Uma regra so (`PvpRules.ClassifyDeath`), usada por tudo que reage a morte:

1. O golpe que matou tem atacante e ele e um **jogador** (conferido pela ZDO, entao vale
   mesmo se o matador estiver longe ou ja tiver saido) -> **morte por jogador**.
2. Senao, se a vitima levou dano de jogador ha menos de `KillCreditSeconds` (20 s) -> **morte
   por jogador**, credito para quem bateu (queda, afogamento, fogo ou mob terminando o servico).
3. Senao -> **PvE**.

| | Morte por jogador | Morte por PvE |
|---|---|---|
| Imunidade a PvP | sim (fora da arena) | nao |
| Conta no ranking K/D | sim (fora da arena) | nao |
| Matador vira PK | sim, salvo arena / defesa / alvo PK / alvo cacado | - |
| Defensor morto no proprio territorio | sem perda de skill | perda normal |
| Invasor morto no territorio | defensor ganha recompensa do bioma | - |
| Cacado morre | quem matou ganha a recompensa | desafio acaba sem recompensa |
| PK que morre | perde skill x2 e deixa de ser PK | perde skill x2 e deixa de ser PK |
| Tumba | so o dono abre | so o dono abre |

O cliente registra cada morte no log (`[Deadheim PvP] Morri: causa=PlayerDirect|PlayerCredit|Pve
matador=<id> ultimoGolpe=<tipo> ...`) e o servidor tambem (`[Deadheim PvP] Morte: <vitima> por <matador|PvE> ...`).

## Regras

| Pedido | Como ficou | Config |
|---|---|---|
| PvP para todos | Ligado fora das zonas seguras; o botao do inventario fica travado | `ForcePvp` |
| Reducao de dano PvP | Dano de jogador em jogador x0.5 | `DamageMultiplier` |
| Reducao de dano em ward | Dentro de ward **ligado e abastecido** onde voce tem permissao, o dano PvP que voce recebe e x0.5 de novo | `WardDefenseMultiplier` |
| Dono do territorio sem FF | Quem tem permissao no mesmo ward nao se fere dentro dele | `NoFriendlyFireTerritory` |
| Guilda ou party | Cla embutido (`/cla`): sem fogo amigo, acesso automatico aos wards do cla, membros no mapa | `ClanEnabled`, `ClanMaxMembers`, `ClanShowOnMap` |
| Morto por jogador | Fica **imune a PvP**: nao da nem leva dano de jogador; PvE normal | `ImmunityMinutes` (10) |
| Ilha inicial safe zone | A terra ligada ao templo inicial (ate o raio). Calculada do gerador do mundo, igual em cliente e servidor | `StartIslandMode` (Island/Radius/Off), `StartIslandRadius` |
| Transportes safe zone | Barco, carroca e montaria protegem quem esta neles; barco e carroca nao tomam dano de jogador | `TransportsSafe`, `TransportsInvulnerable` |
| Combate | Dar ou levar dano PvP deixa "em combate": zona segura nao protege e retreat nao funciona | `CombatTagSeconds` (30) |
| Retreat cooldown e combate | `/retreat` com recarga, bloqueado em combate e cacado (a pedra do Hearthstone tambem) | `RetreatCooldownMinutes` (30) |
| PK dobro de perda de skill | Quem mata jogador vira PK; PK que morre perde skill x2 e deixa de ser PK. Matar PK, cacado, na arena ou defendendo o proprio territorio nao gera PK | `PkMinutes`, `PkSkillLossMultiplier`, `PkClearsOnDeath` |
| Arena sem perda de skill | Zonas de arena: PvP sempre, sem perda de skill, sem PK, sem imunidade, fogo amigo liberado, fora do ranking | `ArenaZones`, `ArenaNoSkillLoss`, `ArenaFriendlyFire`, `ArenaCountsLeaderboard` |
| Defesa de castelo | Castelo = seu territorio (ward). Morrer para jogador nele nao tira skill; matar invasor nele rende recompensa por bioma | `DefenseNoSkillLoss`, `DefenseRewardItem`, `DefenseRewardByBiome`, `DefenseRewardCooldownMinutes` |
| Challenge / Hunted | `/desafio`: em 3 min vira CACADO por 1 h, visivel no mapa de todos, sem zona segura nem imunidade, sem portal. Sobreviveu: recompensa. Quem matar: recompensa | `Challenge*`, `HuntedCanUsePortals` |
| Leaderboard K/D | `/rank`: janela e chat com o top e a sua linha | `LeaderboardSize` |
| Tumba por player | So o dono (e admin) abre a propria tumba | `TombstoneOwnerOnly`, `TombstoneClanAccess` |
| Tirar raids | Ataques aleatorios de monstros as bases desligados. O mod RaidSystem sai do pacote | `DisableRandomEvents` |
| Gold sem peso, pilha 5k | Coins pesam 0 e empilham 5000 | `CoinsWeightless`, `CoinsMaxStack` |
| Ward sem quebrar chao, pedra e arvore | Dentro do ward alheio: sem picareta/enxada no terreno, sem quebrar pedra, minerio, arvore, tronco e toco | `[Wards] ProtectTerrain`, `ProtectNature` |

Formato das zonas: `Nome,x,z,raio|Nome2,x,z,raio`. O servidor escreve no log, ao subir,
onde fica o templo inicial e o tamanho da zona segura calculada:

```
[Deadheim PvP] Templo inicial em x=3 z=5. Zona segura inicial: ... celulas=3674 (~3,76 km2)
```

## Comandos (chat)

| Comando | O que faz |
|---|---|
| `/pvp` | Seu estado de PvP e ajuda |
| `/cla criar <nome>` / `convidar <jogador>` / `aceitar` / `recusar` / `sair` / `expulsar <jogador>` / `lider <jogador>` / `desfazer` | Cla (`/clan` tambem funciona) |
| `/desafio` / `/desafio cancelar` / `/desafio status` | Desafio |
| `/rank` | Ranking K/D |
| `/pvpadmin zona` | (admin) coordenadas, bioma, zona e bandeiras onde voce esta |
| `/pvpadmin imune <min>` / `limpar` / `pk <min>` | (admin) mexe no proprio estado, para testar |

## Teste de ponta a ponta

`Testing/run-pvp-test.ps1` sobe um servidor dedicado local e dois clientes reais (Alfa e
Bravo) com o `Testing/PvpTestDriver`, que segue um roteiro sincronizado e escreve
`[PVPTEST] PASS|FAIL` no log de cada cliente. Nada e instalado nas pastas do jogo; os
personagens de teste ficam numa pasta propria e as preferencias do Valheim sao restauradas
no fim.

```
powershell -ExecutionPolicy Bypass -File Testing\run-pvp-test.ps1 -Root D:\tmp\pvptest -Solo
```

- `-Solo`: um cliente real + um segundo jogador ("Dummy") criado pelo driver. Cabe numa
  maquina comum; e o modo usado para validar o modulo. O driver le o log do servidor (mesma
  maquina) para conferir o que o cliente nao ve: quem virou PK, quem ganhou recompensa.
- Sem `-Solo`: dois clientes reais (Alfa e Bravo) em roteiro sincronizado. Precisa de
  memoria para servidor + dois clientes (uns 10 GB de commit livre).
- `-Steps eu-bato,pk,arena`: roda so esses passos do solo (o setup sempre roda).
