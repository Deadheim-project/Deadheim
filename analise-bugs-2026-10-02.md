# Análise de bugs do Deadheim (7.3.0, commit 2accba5), 2026-10-02

Escopo: todo o C# do repositório. Prioridade para Pvp/ e para o que mudou no 7.3.0. A worktree `claude/vigorous-chatterjee-36a09f` está no commit 2accba5 (o mesmo de `main` e `feat/pontuacoes`).

Como foi feito:
- Li todos os arquivos de Pvp/, Wards/, Ajustes/, Shared/, RaidSystem/, Hearthstone/, VipList/ e os da raiz. No Testing/ li os passos do PvpSemJogo e do PvpTestDriver ligados ao 7.3.0. ThirdParty/ConfigSync.cs não foi revisado (é código de terceiros).
- Para conferir o que o jogo faz de verdade, decompilei o `assembly_valheim.dll` de `D:\valheim-ref` e o `0Harmony.dll` (HarmonyX 2.9) do BepInEx com o ilspycmd, numa pasta temporária fora do repositório. Quando cito "vanilla `Arquivo.cs:linha`", a linha é desse código decompilado.
- Testes offline: PvpSemJogo deu 21/0 e AjustesSemJogo deu 40/0. Os dois testam o formato do arquivo e a janela de ajustes, não as regras de combate. O teste dentro do jogo (run-pvp-test.ps1) não foi rodado, como pedido.
- Nada foi corrigido, commitado ou publicado.

Legenda de confiança: **confirmado** = fluxo rastreado no nosso código e no vanilla. **provável** = depende de um comportamento do Valheim ou do ambiente que não deu para verificar aqui.

---

## Resumo

| Gravidade | Quantidade |
|---|---|
| Crítico | 3 |
| Alto | 4 |
| Médio | 8 |
| Baixo | 18 |
| **Total** | **33** |

Há também 2 testes que verificam a coisa errada (seção final).

### Os 5 mais urgentes

1. **C2: a bounty cria moedas do nada.** O servidor devolve, como recompensa, o valor que o cliente *diz* ter pago. Um `pay` sem bounty, ou um `place` com nome inexistente, rende qualquer quantidade de moedas.
2. **C1: o servidor de PvP confia no remetente que o próprio cliente escreve no pacote.** Um cliente modificado se passa por um admin online e usa `/pvpadmin pk -1 <qualquer um>`, bounty paga pela casa e `pve admin-off`. Também consegue falar em nome de outro jogador.
3. **C3: o pacote de morte não é validado.** Dá para registrar uma morte que não aconteceu, com qualquer matador. Isso mexe no K/D, gera PK (até permanente) em inocentes, paga bounty a um cúmplice, cria moedas de "defesa de castelo" e mexe no Ranking de Guerra.
4. **A4: Alt+F4 em combate escapa do saque do 7.3.0.** Com `CombatLogout=Rank` (o padrão), quem está perdendo a luta fecha o jogo e fica com 100% das moedas e da carga. O preço é só uma morte no `/rank`. Não precisa de mod.
5. **A1: a pedra do Hearthstone ignora o bloqueio do Deadheim.** O HarmonyX roda todos os prefixos, então a pedra teleporta mesmo em luta com monstro. Em combate PvP ou caçado, a pedra é gasta e o teleporte não acontece.

Logo depois vêm a **A2** ("protegido pode atacar PK/caçado" não funciona no jogo real, e o teste passa por um atalho) e a **A3** (adiantar ou atrasar o relógio do Windows apaga PK e caçado ou dá imunidade longa, sem mod).

**C1, C2, C3 e M7 exigem um cliente modificado.** O AzuAntiCheat dificulta, mas não impede: basta um plugin que mande um RPC. As outras exigem só um cliente comum.

---

## Crítico

### C1. O servidor de PvP aceita o `sender` escrito pelo cliente: dá para se passar por admin ou por outro jogador
- **Onde:** `Pvp/PvpNet.cs:98-110` (`OnToServer` repassa `sender`), `Pvp/PvpServer.cs:35-63` (`Handle` resolve o jogador por `PvpPeer.TryResolve(sender)`), `Pvp/PvpServer.cs:26-33` (`IsAdminPeer(peerId)`), `Pvp/PvpBounty.cs:101-105` e `Pvp/PvpPve.cs:87-90` (os ramos de admin).
- **Gravidade:** crítico. **Confiança:** confirmado.
- **Por quê:** no vanilla, `ZRoutedRpc.RPC_RoutedRPC` / `HandleRoutedRPC` (vanilla `ZRoutedRpc.cs:175-196`) chama o handler com `data.m_senderPeerID`, que vem do pacote. O servidor não confere esse valor contra a conexão. O próprio `Ajustes/AjustesRede.cs:28-49` diz isso e resolve usando a `ZRpc` atual. O módulo de PvP não faz o mesmo.
- **Cenário:** um cliente modificado envia `DH_Pvp_ToServer` com `m_senderPeerID` igual ao uid de um admin online. Esse uid é fácil de descobrir: é o dono da ZDO do personagem do admin. O pacote leva `admin`/`pk`/`-1`/`Fulano`. `IsAdminPeer` responde true e Fulano vira PK PERMANENTE com perda `All`: ele perde o inventário inteiro em qualquer morte, porque `PkPenaltyOnPveDeath=true`. Da mesma forma dá para criar bounty "paga pela casa" em qualquer jogador, tirar alguém do PvE, ou mandar `death`, `bounty` e `pve join` em nome de outro jogador.
- **Correção:** em `OnToServer`, descobrir o peer pela `ZRpc` da conexão, com o mesmo `ConexaoAtualPatch` do AjustesRede (deixá-lo `internal` e reaproveitar) e `ZNet.GetPeer(ZRpc)`. Ignorar `sender`.

### C2. Bounty: a recusa e o troco devolvem moedas que o cliente só diz ter pago
- **Onde:** `Pvp/PvpBounty.cs:110-114` (`Refund` manda `Coins` = `paid`), `116-136` (`Place`), `203-215` (`Buyout`), `Pvp/PvpClient.cs:190-224` (o cliente tira as moedas *antes* de pedir, e o servidor não tem como saber se tirou).
- **Gravidade:** crítico. **Confiança:** confirmado.
- **Cenário:** um cliente modificado envia `bounty`/`pay`/``/`1000000` sem bounty na cabeça. O servidor chama `Refund(peer, 1000000, "Nao ha bounty...")` e o cliente recebe 1.000.000 de moedas. Um `place` com nome inexistente faz o mesmo, assim como um `pay` com valor maior que o custo (o servidor devolve a diferença como "troco"). Variante: um `place` com pote alto sem pagar nada, e um cúmplice mata o alvo e leva 75% do pote.
- **Correção:** o servidor nunca cria moeda por causa de uma recusa. O cliente guarda o que tirou por id de pedido e só ele devolve, quando o servidor responde "recusado". O pote que entra é inevitavelmente confiado ao cliente; para limitar o estrago, ponha teto por jogador por dia e registre no log quem paga o quê.

### C3. O pacote de morte (`OpDeath`) não é validado
- **Onde:** `Pvp/PvpServer.cs:121-215` (`OnDeath`), `Pvp/PvpClient.cs:152-165`.
- **Gravidade:** crítico. **Confiança:** confirmado.
- **Cenário:** sem morrer, o cliente envia `death` com qualquer `ZDOID` de jogador como matador, `castle="X"` e `killerDefendingCastle=true`, quantas vezes quiser. O servidor faz `kills++`/`deaths++` e `MarkPk` (cada envio sobe um nível, até chegar ao permanente). Também paga a bounty do "morto" ao "matador" (`PvpBounty.OnDeath`), cria moedas de defesa de castelo (`GiveCastleReward`, com um cooldown que só vive em memória) e conta no Ranking de Guerra (`RaiseKilled`). Junto com C1, o "morto" pode ser outro jogador: dá até para limpar o PK de um amigo, já que `victimWasPk && clearsPk` chama `ClearPk`.
- **Correção:** limitar a uma morte por vítima a cada ~10 s, o tempo do respawn. Conferir em seguida que a ZDO da vítima passou a `s_dead=true`. Exigir que o matador seja um jogador online a no máximo N metros da posição (`peer.m_refPos`). Calcular `castle` e o dono no servidor (`PvpBridge.Castle(position)`) em vez de aceitar o bool do pacote.

---

## Alto

### A1. Pedra do Hearthstone: o bloqueio do Deadheim não segura o prefixo do mod Hearthstone
- **Onde:** `Pvp/PvpPatches.cs:525-540` (`HearthstoneCombatPatch`, prioridade First, devolve false) e `Hearthstone/Patches.cs:13-43` (`ConsumePatch`).
- **Gravidade:** alto (é recurso do 7.3.0 e faz perder item). **Confiança:** confirmado.
- **Por quê:** no HarmonyX 2.9 (`HarmonyManipulator.WritePrefixes`), um prefixo que devolve false só faz um AND em `__runOriginal`. **Os prefixos seguintes rodam do mesmo jeito.** O do Hearthstone remove a pedra, chama `TeleportTo` e põe `__result = true`.
- **Cenário 1**, luta com monstro (`RetreatBlockedByPveCombat=true`, `CombatFromPve=false`, os padrões): aparece "Em combate com monstro!" e mesmo assim a pedra é gasta e o jogador teleporta, porque o `TeleportEscapePatch` não bloqueia combate PvE. **Cenário 2**, em combate PvP ou caçado: a pedra é gasta, o `TeleportEscapePatch` recusa o teleporte e o jogador fica sem a pedra.
- **Correção:** no `ConsumePatch` do Hearthstone, receber `bool __runOriginal` e sair com `return false` se ele vier false. Remover a pedra só se `TeleportTo` devolver true.

### A2. "Quem está na zona segura pode atacar PK/caçado" não funciona no jogo real
- **Onde:** `Pvp/PvpRules.cs:31,44-52` (`MayStrikeOutlaw`). Filtros do vanilla: `Attack.cs:1153` (corpo a corpo), `Projectile.cs:553`, `Aoe.cs:489`.
- **Gravidade:** alto (é recurso do 7.3.0). **Confiança:** confirmado.
- **Por quê:** antes de chegar ao `Character.Damage`, o vanilla pula qualquer alvo que não seja inimigo quando o atacante é jogador com PvP desligado: `m_character.IsPlayer() && !m_character.IsPVPEnabled() && !flag`. Para o `BaseAI.IsEnemy`, dois jogadores são da mesma facção, logo não são inimigos. Quem está protegido tem `m_pvp=false`, então o golpe nunca sai. `MayStrikeOutlaw` só é consultado depois, no `AttackerDamagePatch` e no `VictimDamagePatch`.
- **Cenário:** um PK entra na ilha inicial. Os jogadores protegidos tentam bater nele e nada acontece. Ele também não fere ninguém, porque as vítimas estão protegidas. Só quem já está em combate ou fora da zona alcança o PK.
- **Correção:** fazer um postfix em `BaseAI.IsEnemy(Character a, Character b)` que devolva true quando `a` é o jogador local, `b` é um Player e `PvpRules.MayStrikeOutlaw(a, b)`. Testar com um golpe que passe pelo `Attack` ou pelo `Character.Damage`, não por RPC direto (ver T1).

### A3. O relógio do PC controla imunidade, PK, caçado, retreat e o estado do castelo (sem mod)
- **Onde:** `Pvp/PvpState.cs:46` (`Now = DateTime.UtcNow`), `71-77` (`IsImmune`, `IsPk`, `IsHunted`), `140-166` (imunidade gravada como horário absoluto, timers do servidor viram horário local), `Pvp/PvpModule.cs:78-93` (recarga do retreat), `RaidSystem/CastleDefense.cs:26-44` (janela de raid pelo relógio do cliente).
- **Gravidade:** alto. **Confiança:** provável. O caminho no código está confirmado; não testei se o Valheim continua estável depois de uma troca de hora no Windows com o jogo aberto.
- **Cenário:** um PK ou caçado com bounty que não é "até morrer" adianta o relógio do Windows. `_pkUntil`/`_huntedUntil` ficam no passado, e o jogador volta a ter zona segura e transporte, teleporta, perde o `[PK]`/`[CACADO]` do nome e morre sem a perda de PK, porque `pkPays` é decidido no cliente. Ao contrário: depois de morrer para jogador, atrasar o relógio deixa a imunidade valendo por meses, ou seja, o jogador fica intocável por outros jogadores enquanto farma. Adiantar o relógio também zera a recarga do `/retreat`.
- **Correção:** guardar a duração restante e descontá-la com `Time.realtimeSinceStartup`, que é monotônico, para tudo o que vem do servidor (PK e caçado). Para a imunidade e o retreat, gravar o tempo restante (online) em vez de um horário. Ou, melhor, manter a imunidade no servidor, no PvpStore, e mandá-la no `OpState`.

### A4. Deslogar em combate escapa do saque do 7.3.0
- **Onde:** `Pvp/PvpServer.cs:543-577` (`OnDisconnect`), `Pvp/PvpClient.cs:125-138` (a punição no modo Death é uma morte PvE).
- **Gravidade:** alto. **Confiança:** confirmado.
- **Cenário:** com `CombatLogout=Rank` (padrão), um jogador com pouca vida e muitas moedas e minério dá Alt+F4. O servidor só soma uma morte no `/rank` e um abate a quem bateu. Nada cai: moedas e carga ficam com ele. No modo `Death` a punição é uma morte PvE (sem atacante), que também não derruba moedas nem carga, e tudo vai para a tumba.
- **Correção:** no Rank e no Death, marcar `combatLogPending` no servidor. Na volta, o cliente aplica o mesmo saque da morte por jogador (`DropCoins` e `DropCargo` onde ele saiu). No modo Death, classificar a morte da punição como PlayerCredit, com o atacante guardado.

---

## Médio

### M1. A recompensa duplica moedas quando o inventário está cheio
- **Onde:** `Pvp/PvpClient.cs:103-113` (`GiveReward`).
- **Gravidade:** médio. **Confiança:** confirmado (vanilla `Inventory.cs:112-145`).
- **Por quê:** o `Inventory.AddItem(ItemData)` do vanilla primeiro completa as pilhas que já existem. Se depois não acha slot vazio, devolve false *com essa parte já adicionada*. O código então joga no chão o `chunk` inteiro.
- **Cenário:** o inventário está sem slot livre e tem uma pilha de 4.000/5.000 moedas. Chega o pagamento da bounty: 1.000 entram na pilha, e 5.000 caem no chão. Vale para pagamento de bounty, devolução e recompensa de castelo.
- **Correção:** adicionar só o que cabe (`CanAddItem`, ou contar antes e depois) e jogar no chão apenas o que sobrou.

### M2. O resgate de tributo do RaidSystem perde tudo o que passa de uma pilha
- **Onde:** `RaidSystem/RPCManager.cs:236-263` (`RPC_GrantTribute`, `DropOnGround`).
- **Gravidade:** médio. **Confiança:** confirmado (vanilla `Inventory.cs:102-110`: `AddItem(GameObject, int)` corta em `m_maxStackSize`).
- **Cenário:** 9 cargas de tier 1 rendem cerca de 135 RoundLog, cuja pilha é 50. `CanAddItem` diz que cabe, `AddItem` põe só 50 e 85 somem. No chão também cai só uma pilha (`Mathf.Min(amount, maxStack)`). O comentário "nunca some" está errado.
- **Correção:** entregar em pedaços de `maxStack`, como o `GiveReward`, mas já com a correção de M1.

### M3. Corrida na legítima defesa: quem revida rápido também vira agressor
- **Onde:** `Pvp/PvpState.cs:194-202` (`MarkAttack` lê a flag Aggressor da ZDO do alvo).
- **Gravidade:** médio. **Confiança:** provável (depende da latência da ZDO).
- **Cenário:** A bate em B. A flag de agressor de A só chega a B depois do próximo `Tick` (0,25 s) e da sincronização da ZDO. Se B revida nesse intervalo, B também vira AGRESSOR. Quando A mata B, o servidor vê `victimWasAggressor=true` e o agressor original não vira PK.
- **Correção:** o `VictimDamagePatch` já sabe quem bateu. Guarde os jogadores que atingiram o jogador local nos últimos `AggressorSeconds` e não marque agressor ao bater de volta neles.

### M4. `dh_lastPvpAttacker` nunca é limpo: o abate do logout em combate vai para quem bateu horas antes
- **Onde:** `Pvp/PvpState.cs:214-225` (grava na ZDO), `260-265` (`ForgetAttacker` não limpa a ZDO), `Pvp/PvpServer.cs:554-571`.
- **Gravidade:** médio. **Confiança:** confirmado.
- **Cenário:** B acerta A uma vez e vai embora. Uma hora depois, A ataca C, entra em combate e desloga. O servidor credita o abate (e o Ranking de Guerra) a B, que nem estava lá. Se ninguém nunca bateu em A, A leva a morte sem abate para ninguém, embora tenha sido ele o atacante.
- **Correção:** publicar o atacante junto com um carimbo, ou limpar a ZDO quando o combate acaba (`ForgetAttacker` e o fim de `InCombat`). No servidor, só creditar se o golpe for recente.

### M5. `StaffMessage` gera NullReference a cada frame no servidor
- **Onde:** `Patches.cs:77-85` (`PlayerUpdate`).
- **Gravidade:** médio (só quando `StaffMessage` não está vazio). **Confiança:** provável (o servidor dedicado instancia o Player dos jogadores remotos nas áreas ativas e o `Update` roda neles).
- **Cenário:** o admin preenche `StaffMessage`. O postfix roda para *todo* Player e chama `Player.m_localPlayer.Message(...)`. No servidor `m_localPlayer` é null, e sai uma NRE por jogador por frame, enchendo o log e pesando no servidor. No cliente, o mesmo acontece na tela de morte, e a mensagem é repetida N vezes por frame.
- **Correção:** `if (__instance != Player.m_localPlayer) return;` no começo.

### M6. Itens comprados caem como "carga" na morte por jogador
- **Onde:** `Pvp/PvpConfig.cs:304-308` (padrões `PvpCargoTypes=Material,...` e `PvpCargoKeep=PortalToken,ResetToken`), `ClonedItems.cs:29-44` (GarantiaRefino, SpawnerToken e os kits são clones de `Thunderstone`, do tipo Material).
- **Gravidade:** médio. **Confiança:** provável (não conferi o `ItemType` do Thunderstone no asset).
- **Cenário:** um jogador morre para outro com 10 Garantias de Refino (vendidas na Loja Deadcoins) e 4 Spawner Tokens. Cerca de metade cai no chão para quem matou. O PortalToken foi protegido de propósito; esses ficaram de fora.
- **Correção:** incluir `GarantiaRefino,SpawnerToken` (e os kits, se forem pagos) no padrão e **no cfg do servidor**, porque o BepInEx mantém o valor antigo. Ou excluir pelo código todos os `NativeItems`.

### M7. O RaidSystem confia no cliente em conquista, sincronização e registro
- **Onde:** `RaidSystem/RPCManager.cs:98-128` (`RPC_WardDestroyed` usa o `pid` do pacote), `19-35` e `223-251` (`FullSyncResponse` e `GrantTribute` aceitos de qualquer remetente), `37-59` (`UpdatePlayerData` com `playerId` de outro jogador).
- **Gravidade:** médio. **Confiança:** confirmado (exige cliente modificado).
- **Cenário:** na janela de raid, um cliente envia `RaidSystem_WardDestroyed` com a posição do castelo e o próprio `pid`, sem o ward ter caído. A guilda dele conquista o castelo e o servidor ainda gera *outra* RaidWard em cima da que existe (`Util.RespawnWard`). Também dá para mandar a outros clientes um `FullSyncResponse` com donos de castelo falsos (o que muda portas e regras de castelo no cliente da vítima), ou cadastrar o `playerId` de outro jogador com nick e descrição ofensivos.
- **Correção:** no servidor, conferir que a ZDO da RaidWard naquela posição não existe mais ou está com vida ≤ 0. Pegar o atacante pela conexão (como em C1). Nos clientes, só aceitar `FullSync`/`GrantTribute`/`Conquest` quando `sender == GetServerPeerID()`.

### M8. Portal sem nome (e nome que é pedaço de outro) fica só para VIP
- **Onde:** `Patches.cs:235-256` (`TeleportWorldAesir`: `Plugin.VipPortalNames.Value.Contains(portalTag)`).
- **Gravidade:** médio. **Confiança:** confirmado (vanilla `Game.cs:900-909` liga portais sem nome entre si).
- **Cenário:** um não-VIP faz dois portais sem nome. Eles se ligam, mas `"cavalinho,eguinha".Contains("")` é true e aparece "Only Aesir can access this portal.". O mesmo acontece com tags como "cava", "lin" ou ",".
- **Correção:** separar a lista por vírgula e comparar a tag inteira, de preferência sem diferenciar maiúsculas e ignorando tag vazia.

---

## Baixo

### B1. AdminWard: o raio de 150 do clone vira 50, e o AdminWardSmall não tem perfil
- `ClonedItems.cs:290-313` põe `m_radius = 150`, mas o perfil `AdminWard` (`Wards/WardProfile.cs:64-69`) tem `Radius = 50` e o `WardPatches.AwakePatch` (`Wards/WardPatches.cs:44-46`) sobrescreve ao nascer. O `AdminWardSmall` não está nos perfis, então não protege de dano, terreno nem natureza; só o vanilla (construir e abrir baú). **Confiança:** confirmado. **Correção:** decidir o raio certo e deixar um lugar só; incluir `AdminWardSmall` nos perfis.

### B2. As peças "só admin" saem do martelo do próprio admin
- `ItemService.cs:29-51` roda em `ApplyServerConfigOnce` (`Plugin.cs:75-92`), assim que a config sincroniza. Segundo `Patches.cs:106-117`, a adminlist só chega depois do `RPC_CharacterID`. Nesse momento `Admin.LocalPlayerIsAdmin()` ainda é false e as peças SH são removidas da tabela, para a sessão inteira. **Confiança:** provável. **Correção:** reaplicar quando a adminlist chegar, ou só filtrar no `PlacePiece`.

### B3. `/retreat` gasta a recarga mesmo se o teleporte falhar
- `Retreat.cs:65-66`: `MarkRetreatUsed` roda sem olhar o retorno de `TeleportTo`, que no vanilla devolve false se já estiver teleportando ou com `m_teleportCooldown < 2` (vanilla `Player.cs:5888-5911`). **Correção:** `if (TeleportTo(...)) MarkRetreatUsed(...)`.

### B4. `HuntedCanUsePortals` não vale para a pedra nem para o retreat
- A descrição (`Pvp/PvpConfig.cs:259-260`) cita "pedra de retorno", mas `PvpModule.TeleportRefusal` (`Pvp/PvpModule.cs:64-71`) bloqueia o caçado sempre. **Correção:** respeitar a config ou corrigir o texto.

### B5. `/pvpadmin`: número lido com a cultura do PC e padrão silencioso de 1 minuto
- `Pvp/PvpCommands.cs:190`: `float.TryParse(args[2], out value)` sem InvariantCulture. Em pt-BR, "1.5" vira 15. Sem número, vale 1: `/pvpadmin pk Fulano` marca **o próprio admin** como PK por 1 min, porque "Fulano" cai em `args[2]` e o alvo fica vazio. **Correção:** InvariantCulture e exigir o número.

### B6. A posição do retreat e da pedra é gravada com a cultura do PC
- `Retreat.cs:18-24,104-106` e `Hearthstone/Hearthstone.cs:110-126` usam `ToString()` e `float.Parse` sem InvariantCulture. Com o personagem no servidor (ServerCharacters), abrir em um PC com outro idioma lê "123,45" como 12345: teleporte para longe, possivelmente para a borda do mundo, ou exceção. **Confiança:** provável. **Correção:** InvariantCulture nos dois lados, aceitando o formato antigo na leitura.

### B7. Recompensa de defesa de castelo: o cooldown não sobrevive ao restart e o conluio rende moedas
- `Pvp/PvpServer.cs:281-302`: `_defenseRewardAt` só existe em memória, e a recompensa é moeda criada. Duas contas de guildas diferentes repetem "defesa" no castelo durante a raid. Mesmo sem mod, rende até 250 por par de jogadores por dia, e o contador zera a cada restart. **Correção:** gravar no PvpStore e pôr teto diário por defensor.

### B8. O logout em combate não entra no Ranking de Guerra
- `Pvp/PvpServer.cs:561-569` chama `RaiseKilled`, mas o `ScoreManager.OnPvpKill` (`RaidSystem/ScoreManager.cs:46-54`) resolve a guilda de quem saiu por `Player.GetPlayer` ou `m_players`, e ele já saiu dos dois. Resultado: "fora do Ranking de Guerra". **Correção:** guardar a guilda no `_seen` (o `RememberPeers` lê a cada segundo) e passá-la adiante.

### B9. PvpStore: linha ilegível some no save seguinte e há janela de pagamento duplo
- `Pvp/PvpStore.cs:39-68` e `Pvp/PvpData.cs:127-213`: uma linha que não é entendida é só contada. O `Save` seguinte reescreve o arquivo sem ela, sem backup. Basta um admin editar à mão, por exemplo com espaço no lugar de TAB, para o jogador sumir. Também: `OnHello` zera `pendingCoins` (`Pvp/PvpServer.cs:73-79`), mas o save só acontece a cada 60 s. Um crash nessa janela paga de novo no próximo login. **Correção:** quando `skipped > 0`, fazer backup antes do primeiro save. Salvar logo depois de mexer em moedas.

### B10. O "null guard" do `ZNetScene.RemoveObjects` deixa entradas mortas para sempre
- `Patches.cs:283-355`: uma view destruída ou sem ZDO é contada e pulada, mas nunca sai de `m_instances`. Enquanto essa entrada existir, `CreateObjects` acha que a ZDO já tem instância e não recria o objeto: ele fica invisível até relogar. **Confiança:** provável. **Correção:** remover essas chaves de `m_instances` depois do laço.

### B11. RaidSystem grava com `File.Replace`, que o PvpStore evita por causa do Mono no Linux
- `RaidSystem/DataStore.cs:47-50`. O comentário de `Pvp/PvpStore.cs:78` diz que a DatHost roda Mono em Linux e por isso usa Copy+Delete. Se `File.Replace` falhar lá, todo `Modify` loga `[RaidSystem] Save error` e territórios, tributo e pontos voltam no restart. **Confiança:** provável que *funcione* (o Mono implementa o método no Unix), mas vale procurar "Save error" no log do servidor. **Correção:** usar o mesmo padrão nos dois.

### B12. O alvo pode entregar o pote da própria bounty a um cúmplice
- `Pvp/PvpBounty.cs:247-259`: quem matou leva 75%, sem nenhuma checagem de relação com o alvo. Sem mod, o alvo morre para um amigo de outra guilda (ou de queda até 20 s depois de levar um golpe dele) e o pote pago pelo inimigo fica com o time do alvo. Com C3, nem precisa morrer. **Correção:** questão de desenho. Por exemplo, não pagar a quem já foi do grupo ou da guilda do alvo nas últimas X horas, ou pagar só por morte direta.

### B13. A faixa por chefes é burlável
- `Pvp/PvpBosses.cs:53-63,88-94`: o nível vem do perfil do personagem e é publicado pelo próprio cliente em `dh_bossTier`. Um veterano cria um personagem novo (nível 0), passa o equipamento por baú e caça novatos, enquanto fica imune aos veteranos. Um cliente modificado também pode publicar o nível que quiser. **Correção:** questão de desenho; se for importante, guardar o nível no servidor (ServerCharacters) e conferir.

### B14. Qualquer `RPC_TeleportTo` libera o teleporte do caçado ou de quem está em combate
- `Pvp/PvpPatches.cs:511-517`: `_teleportByAdmin = true` em todo `RPC_TeleportTo`, sem conferir quem chamou. Um cliente modificado (inclusive de um amigo) puxa o caçado para longe. **Correção:** só liberar se o remetente for admin, conferindo pela conexão.

### B15. `Force PvP In Zones` liga o PvP também de quem é PvE permanente ou imune
- `RaidSystem/Patches.cs:375-383` muda o resultado de `Player.IsPVPEnabled` sem olhar as flags `Pve` e `Immune`. O padrão é Off. **Correção:** pular quando `PvpFlags.Pve` ou `Immune` estiverem ligadas.

### B16. Mirar uma porta de dungeon desliga *todos* os wards para aquele cliente
- `Wards/WardPatches.cs:89-108`: o `IsEnabledPatch` devolve false para qualquer ward enquanto o jogador local mira uma porta ou baú de dungeon. Isso vale também para o `GetProtectingWard` do dano que esse cliente processa como dono das peças. **Confiança:** provável, com impacto pequeno. **Correção:** só desligar o ward que cobre a porta mirada.

### B17. Estado estático que não zera no logout
- `Plugin.PlayerWardCount` e `PlayerPortalCount` (`Plugin.cs:53-54`) e `RaidSystemPlugin.LocalPlayerInfo` (`RaidSystem/RaidSystemPlugin.cs:31`) passam de um personagem para o próximo até chegar a resposta nova: limite de ward errado e menu com dados do personagem anterior. **Correção:** zerar no `Game.Logout`.

### B18. `ResetWorldDay` muda o `m_netTime` na thread de save
- `World.cs:7-13`: o prefixo de `SaveWorldThread` roda fora da thread principal. Além disso, a volta do relógio faz o combustível do ward pular um intervalo (`Wards/WardCore.cs:141-145`). O padrão é off. **Correção:** fazer isso no `SaveWorld`, que roda na thread principal.

---

## Testes que verificam a coisa errada

### T1. `pk-sem-protecao/protegido-fere-pk` passa sem testar o caminho real
- `Testing/PvpTestDriver/Solo.cs:841-846`: `DummyStrikesMe` manda `RPC_Damage` direto e pula o filtro do vanilla em `Attack`/`Projectile`/`Aoe` que bloqueia quem tem PvP desligado (ver A2). O teste passaria mesmo com o recurso quebrado no jogo. O mesmo vale para `zona-segura/protegido-nao-ataca`, `imunidade/nao-causa` e `chefes/fraco-nao-fere-forte` (`IStrikeDummy`): eles testam só o lado da vítima. Para o atacante, use `_dummy.Damage(...)` (como no passo do agressor) ou, melhor, um `Attack` de verdade.

### T2. `retreat/bloqueado-em-luta-com-monstro` não testa a pedra
- `Testing/PvpTestDriver/Driver.cs:846-848` só confere o texto de `PvpModule.RetreatRefusal`. A pedra do Hearthstone, que é onde está o bug A1, não tem teste: falta consumir o item com o mod Hearthstone carregado e conferir que a posição e o inventário não mudaram.

---

## O que do 7.3.0 foi revisado e está certo (fora o que já foi listado)

- **DropCargo e DropCoins:** jogam no chão antes de tirar do inventário, o que é seguro (sem exceção no meio, não há duplicação). Não valem na arena e rodam antes da tumba (vanilla `Player.cs:3418`). Os problemas estão em A4, C3 e M6.
- **Timer de PK só online (`TickPkTimers`):** desconta só de quem tem personagem online, com teto de 10 s por tique e o mesmo relógio do cliente. O desvio possível é pequeno e favorece o servidor. O problema real é o relógio do cliente (A3).
- **Faixa por chefes:** `m_playerStats[0].m_enemyStats[0]` é de fato o total de mortes, e o jogo registra a morte de chefe para todos que deram dano (vanilla `Character.cs:2946-2951`, `PlayerProfile.cs:788-823`). A arena é exceção, como planejado. Os riscos estão em B13.
- **Buffs como StatusEffect:** `SE_Stats` novo é neutro (multiplicadores 1, listas vazias), o `ObjectDB.GetStatusEffect` percorre a lista (então o SE adicionado depois é encontrado) e `m_ttl = m_time + restante` dá a contagem certa. Sem bug.
- **Taxa de recursos PvE/PvP:** o postfix em `Game.UpdateWorldRates` está certo, não roda no servidor e é atualizado em todas as trocas de config e de PvE. É uma taxa absoluta: substitui o modificador de mundo, como o README diz.
- **Retreat bloqueado por luta com monstro:** o comando funciona. O bug é só na pedra (A1).
- **`/pvpadmin pk`:** a lógica no servidor está certa (0 limpa, negativo é permanente com a perda do nível mais alto). Os problemas estão em C1 e B5.
- **Arquivo `pvp-<mundo>.txt`:** InvariantCulture em tudo, ida e volta exata (testada), escrita com `.tmp` e depois Copy. Ressalvas em B9.
- A assinatura `PvpRules.AreAllies(Player, Player, Vector3)` que o NpcValheim (branch `feat/arena`) patcheia por nome não mudou.
