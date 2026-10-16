# Tarefas restantes do Deadheim: leia antes de qualquer arquivo

Um arquivo por tarefa (01 a 19), cada um é o briefing de um agente. Fonte completa: ../projeto-itens-restantes.md. Um agente por repositório de cada vez (nunca dois escrevendo no mesmo repo).

## 0. Achados que mudam o plano

1. **Montarias 28 e 30 já estão quase prontos, sem commit.** O working tree de `D:\deadheim\montarias` (6 arquivos modificados + `Integration/VipProvider.cs` novo) já tem `MountNameActive/VipOnly/MaxLength`, `MountSkinActive/VipOnly`, `SkinList` por montaria (`Escura:#3A2C22|Clara:#E8DDCF|Dourada:#D4AF37`), `HandleSetCosmetic` em `RiderServer`/`RiderNet` e `Apply(rank, mounts, names, skins)`. Nada disso foi testado nem compilado por mim. O trabalho que sobra é revisar, testar e fechar, não escrever do zero. O 30 é só tinta de cor, então "precisa de arte" deixa de valer para a v1.
2. **Pet: o catálogo padrão de `Pets.cs` usa parâmetros de Animator que não existem.** Ver capítulo 36, seção B.
3. **Pet: o prefab `Raven` não existe** na lista de prefabs do Jotunn (li as páginas de 0 a 700 mil caracteres da lista; só aparecem `Crow`, `AshCrow`, `darkwood_raven` e `piece_birdnest`). Alternativa no capítulo 36.
4. **Não há API de "battleground" em lugar nenhum do código.** Só `Pets.cs` cita a palavra. O que existe é arena (`PvpZones.IsArena`, zonas do Deadheim), partida de arena do NpcValheim (chaves no ZDO do jogador `npcv_arenaMatch/Side/Phase`) e raid (`RaidSystem.Util`). O capítulo 36 resolve com isso.
5. **Aesir 23:** não achei uma constante de "anexos por carta". Só existe `MaxMailPerPlayer = 500` (caixa de entrada) e `ItemSpawner.MaxStacksPerDelivery = 32` (entrega de item). O limite de anexos precisa ser localizado em `MailboxNpc`/`MailWire`/UI antes de projetar o número.
6. **Aesir 22:** o limite é `MarketDatabase.MaxListingsPerPlayer = 50`, por jogador **por quadro de leilão** (`MarketDatabase.cs:211`).

## 1. Dependências e ordem de entrega

| Item | Repositório | Depende de | Esforço | Tokens (relativo) |
|---|---|---|---|---|
| 22 anúncios | npc-valheim | nada | 0,5 dia | baixo |
| 23 anexos | npc-valheim | achar o limite no correio | 0,5 a 1 dia | baixo |
| Barbeiro | npc-valheim | spike de persistência do cabelo | 2 dias | médio |
| 34 arena | npc-valheim | resultado da arena (`ArenaResult`) | 1 dia | médio |
| 28 nome montaria | montarias | revisar código existente | 0,5 dia | baixo |
| 30 skins montaria | montarias | idem | 0,5 dia | baixo |
| 20 estábulo | montarias | 28/30 fechados | 2 dias | alto |
| 36 pet (fechar) | Deadheim + npc-valheim | teste no jogo; ficha na loja | 2 a 3 dias | alto |
| 19 vaga reservada | Deadheim | decidir mecanismo (cap. 19) | 1 dia | médio |
| 10 puxar do baú | Deadheim | `Containers.cs`, baú Aesir | 2 dias | médio |
| 13 craft com baús | Deadheim | 10 (mesma busca de baús) | 1,5 dia | médio |
| 29 chama colorida | Deadheim | nada (é a base do Halloween) | 2 dias | médio |
| 31 efeito de abate | Deadheim | registro de cosméticos | 1,5 dia | médio |
| 33 lápide | Deadheim | registro de cosméticos | 1,5 dia | médio |
| Vela por cosmético | velas | chave `vela:*` do registro | 0,5 dia | baixo |
| H1 Halloween | vários | 29, 31, 33, vela, título | 0,5 dia (montagem) | baixo |
| 32 peças decorativas | Deadheim | arte | 2 dias + arte | médio |
| H2 passe de temporada | npc-valheim + Deadheim | cosméticos prontos, desafios semanais | 4 a 5 dias | alto |
| 24 plantas salvas | Deadheim | nada, mas é grande | 5 a 6 dias | alto |
| 35 transmog | Deadheim | nada, mas é grande | 5 dias | alto |
| 21 marcadores | Deadheim | decisão do usuário | 1 dia | baixo |

Grafo: `Cosmeticos`(registro, já existe) → 31, 33, 29, vela, título → H1 → H2. `Containers`/busca de baús → 10 → 13. 28 e 30 → 20. Nenhum item de uma linha depende de item de outra linha, fora estes.

**Ordem e releases pequenos** (cada release é um mod só, para um deploy só):

| Release | Conteúdo | Prazo sugerido |
|---|---|---|
| R1 NpcValheim 0.1.74 | 22, 23, Barbeiro (e tirar FichaBarbeiro da loja até estar pronto) | antes de 24/10 |
| R2 Montarias 0.3.x | 28, 30 (e 20 só se couber) | junto de R1 (mod diferente, deploy separado) |
| R3 Deadheim 7.7.0 | pet fechado (cap. 36) + Ficha de Pet | depois de teste com 2 clientes |
| R4 Deadheim 7.8.0 | 29, 31, 33 + título Assombrado + chave de vela | até 24/10 se quiser vender Halloween |
| R4b Velas | vela Abóbora (lê a chave de R4) | com R4 |
| R5 Deadheim | 10, 13, 19 | novembro |
| R6 NpcValheim | 34 (arena) | novembro |
| R7 | H2 passe de temporada | novembro |
| R8+ | 24, 35, 32, 20, um por release | dezembro |

**Atenção ao Halloween (24/10).** O pacote exige 29, 31, 33, vela e título. São cerca de 7 dias de trabalho de implementação mais teste, e 24/10 é daqui a 9 dias. Dá apenas se nada mais entrar antes. A alternativa honesta é mover a janela de venda da `FichaHalloween` (`from=2026-10-24` em `DeadcoinShop.DefaultItems`) para o dia em que R4 sair. Decisão sua (ver capítulo H1).

**Todo release de Deadheim**: subir `MinimumRequiredVersion`, manter Whitelist, conferir mods de admin (regras do `deploy-producao`).

---

## Barbeiro (NPC novo, gasta FichaBarbeiro)

**Abordagem.** Um NPC de serviço no NpcValheim, no molde do Treinador (`Npc/TrainerNpc.cs`, `ServiceNpcAuthority`, `NpcRequestGuard`). O jogador fala com o NPC, abre uma tela com cabelo, barba, cor do cabelo e (opcional) cor da pele, vê a prévia no próprio personagem e confirma. A confirmação gasta 1 `FichaBarbeiro` por visita (todas as mudanças de uma vez).

**Ganchos do jogo.** Nada de Harmony. A aparência do jogador é `VisEquipment`: `SetHairItem(string)`, `SetBeardItem(string)`, `SetHairColor(Vector3)`, `SetSkinColor(Vector3)`, acessíveis por `Player.m_localPlayer.m_visEquipment`. A lista de cabelos e barbas vem do `ObjectDB` (itens com nome `Hair*` e `Beard*`; confirmar a lista exata no assembly antes, porque nomes e quantidade variam). Prévia = chamar o setter e voltar ao valor antigo se cancelar.

**Persistência (spike obrigatório de 1 hora).** O ponto de risco é saber se o cabelo trocado fora da criação de personagem é salvo. A aparência é gravada junto do personagem em `PlayerProfile` (via `Player.Save`). Teste: trocar o cabelo, deslogar, logar. Se não persistir, aplicar a escolha guardada no `m_customData` do jogador a cada spawn (mesmo padrão que o pet usa com `dh_pet`).

**Onde fica a autoridade.** A ficha é um item do inventário do cliente, então o consumo é do cliente (mesmo padrão da `FichaFundador`/`FichaPet`). O efeito é só visual e de baixo valor, então uma fraude (cliente modificado que muda o cabelo sem ficha) custa apenas cosmético. Para reduzir: o cliente só aplica depois de o servidor responder "OK" a um RPC `Barber_Request` (valida distância ao NPC e `NpcRequestGuard` contra repetição), e o servidor registra a transação no log. Sem checagem de VIP: é venda avulsa.

**PvP.** Cabelo e cor de pele podem ser usados para imitar outro jogador (disfarce). Regra: cor de pele limitada a uma paleta fixa de tons naturais (config), nada de verde/azul fosforescente, e o nome sobre a cabeça continua o real. Sem mudar nome, sem mudar porte.

**Config** (`[Barber]`): `Enabled`, `TokenPrefab=FichaBarbeiro`, `SkinColors` (paleta), `HairColors` (paleta ou livre), `AllowSkin=false` por padrão.

**Arquivos.** NpcValheim: `Npc/BarberNpc.cs` (novo), `UI/BarberWindow.cs` (novo), registrar o NPC em `NpcSpawnerStub`/modelo igual ao do Treinador; `Content` para o modelo e o diálogo. Deadheim: nada, a ficha já existe em `ClonedItems.cs`.

**Riscos.** Persistência (spike). Cabelo de gênero oposto/modelo do personagem (`m_modelIndex`) fica fora. O NPC não sobrescreve NPCs já postos no servidor (lembrete do admin).

**Teste.** (1) Trocar com ficha: aplica, ficha some. (2) Cancelar: volta ao antigo, ficha fica. (3) Sem ficha: bloqueado. (4) Relogar: persiste. (5) Segundo cliente vê o novo cabelo. (6) Spam de clique: cobra uma vez.

**Esforço:** 2 dias (a UI toma metade). **Antes de pronto, tirar a FichaBarbeiro da loja** (já está em `DefaultItems`).

---

## Aesir 22: mais anúncios no leilão

**Abordagem.** O limite 50 vira `NaoVip=50` e `Vip=100` (config). A checagem está em `MarketDatabase.cs:211` e não sabe quem é VIP. Mudar `Add(...)` para receber o limite já resolvido; quem resolve é o handler do RPC em `MarketplaceNpc` (tem o `sender`), usando `VipShopShortcut.SenderIsVip(sender)`.

**Ganchos.** Nenhum Harmony. **Autoridade:** servidor, já é onde o anúncio é criado. **PvP:** nenhum efeito. **Config** (`[Market]`): `MaxListingsPerPlayer=50`, `VipMaxListings=100`. **Arquivos:** `Persistence/MarketDatabase.cs`, `Npc/MarketplaceNpc.cs`, UI do leilão (mostrar "x/100").

**Riscos.** O teto 500 por quadro (`MaxListingsPerBoard`) continua; se muitos VIPs lotarem o quadro, não-VIP não consegue anunciar. Mitigar: o teto de VIP só vale enquanto o quadro tiver menos de 400. Quem perde o VIP mantém os anúncios, só não cria novos acima de 50.

**Teste.** Não-VIP trava no 51º; VIP chega ao 100º; VIP expirado não cria mais. **Esforço:** 0,5 dia.

---

## Aesir 23: mais anexos no correio

**Abordagem.** Mesma ideia do 22: VIP envia mais anexos por carta (sugestão: o limite de hoje +50%, definido depois de achar o valor). Só aumenta o que o **VIP envia**, nunca a caixa de quem recebe, e continua restrito a correio dentro das regras atuais (a regra do projeto proíbe "correio fora de casa").

**Passo 0.** Localizar onde o limite de anexos é imposto (`MailboxNpc`, `MailWire`, `MailDatabase`, UI). Não achei constante óbvia. `MaxMailPerPlayer=500` e `MaxHouseRecipients=100` são outros limites. `ItemSpawner.MaxStacksPerDelivery=32` é a entrega de item no mundo.

**Autoridade.** Servidor valida no recebimento da carta (tem `sender`). **Config** (`[Mail]`): `VipMaxAttachments`. **Arquivos:** os do passo 0. **Teste:** não-VIP no limite antigo; VIP acima; recebimento do destinatário não-VIP funciona (receber nunca é limitado). **Esforço:** 0,5 a 1 dia.

---

## Aesir 34: comemoração na arena

**Abordagem.** Quando o VIP vence uma partida de arena, toca uma comemoração só visual (fogos/partículas, texto "Fulano venceu", pose de emote) sobre ele, visível aos presentes na arena. Sem premiação, sem efeito de dano.

**Ganchos.** `ArenaClient` já recebe `ArenaResult` (`ArenaClient.cs:158`). No servidor da arena (`ArenaServer.cs`), ao fechar a partida, o vencedor e a lista de presentes são conhecidos. O servidor envia um RPC `Arena_Celebrate(playerId)` aos participantes se o vencedor for VIP. O cliente instancia partículas (prefab do jogo já existente, por exemplo `fx_Fader_Fireball_...` ou fogos do evento; escolher um inofensivo e **sem `Aoe`/`Projectile`**, só `ParticleSystem`) e o emote por `Emote`.

**Regras de PvP.** Só depois de a partida terminar (fase `Ended`), nunca durante. Prefab sem colisor e sem som alto. Um por partida, duração de 6 s. Sem relação com quem venceu em PvP aberto.

**Config** (`[Arena]`): `VipCelebration=true`, `CelebrationSeconds=6`. **Arquivos:** `Arena/ArenaServer.cs`, `Arena/ArenaClient.cs`, `Arena/ArenaWire.cs` (novo opcode), efeito em arquivo novo `Arena/ArenaCelebration.cs`. **Riscos:** nome do prefab de efeito (verificar `ZNetScene`); efeito pesado com muita gente. **Teste:** vitória de VIP e de não-VIP; dois clientes na arena veem; fora da arena nada. **Esforço:** 1 dia.

---

## Aesir 10: animais e fogueiras puxam do baú

**Abordagem.** Dois comportamentos para o VIP, dentro de um raio configurável (sugestão 8 m) e só baús que ele pode abrir: (a) animais domesticados (`Procreation`/`Tameable` com `MonsterAI.m_consumeItems`) acham comida em baús próximos em vez de esperar no chão; (b) fogueiras e braseiros (`Fireplace`) pegam combustível de baús próximos.

**Ganchos.** Dados do jogo: `Fireplace.UpdateFireplace`/`Fireplace.Interact` (combustível), `MonsterAI.UpdateConsumeItem` (procura item no chão) e `Container` (`m_inventory`, `CheckAccess(long playerID)`). Prefixo ou postfix em `Fireplace.UpdateFireplace` quando o combustível está abaixo de um limite; achar `Container`s por `Physics.OverlapSphere` com cache de 5 s (não varrer a cada frame). Para os animais, postfix em `MonsterAI.UpdateConsumeItem`: se não achou item, procurar no baú e simular o consumo (`Container.GetInventory().RemoveItem`).

**Onde a posse é checada.** Em quem tem autoridade do objeto: o dono do `ZNetView` do animal ou da fogueira (no servidor dedicado, o servidor ou o cliente mais próximo). **O dono do objeto pode não ser o VIP.** Por isso a regra é: o *dono da peça/animal* precisa ser VIP (o jogador que construiu a fogueira, gravado em `Piece.m_creator`, ou o `Tameable` com `m_owner`/`Character.m_nview "owner"`). Checar `creator` contra a lista VIP **no servidor**, que é quem executa o consumo para objetos sem cliente por perto. Se o objeto é executado por um cliente, ele não pode confiar em si mesmo: o consumo do baú tem de passar por RPC ao dono do baú ou ao servidor. Mais simples e seguro: **executar só no servidor** (dedicado) e registrar a regra para zonas carregadas.

**Regras de PvP.** Baú só abre se o jogador teria acesso (ward, `Container.CheckAccess`). Zona de raid: fogueira de base inimiga **não** puxa de baú alheio durante raid. Sem puxar de baú de outro jogador/guilda.

**Config** (`[AesirContainers]`): `Enabled`, `Radius=8`, `OnlyOwnerVip=true`, `PullIntervalSeconds=5`, `AllowedFuel` (lista). **Arquivos:** `Containers.cs` (já existe; reaproveitar a busca), novo `AesirPull.cs`. **Riscos:** custo de varredura com muitas fogueiras (cache e intervalo); duplicação de consumo entre servidor e cliente; o baú de outro jogador. **Teste:** fogueira de VIP acende puxando madeira; fogueira de não-VIP não; baú fora do raio não; baú trancado/ward alheio não; animal come do baú. **Esforço:** 2 dias. **Faz par com o 13** (mesma busca de baús, mesma regra de posse).

---

## Aesir 13: fabricar usando os baús próximos

**Abordagem.** O VIP pode fabricar/construir usando o conteúdo de baús ao redor da bancada, em vez de só o inventário. Já existe o padrão de `ChestSnap`/mods similares (conferir o manifest do launcher: se algum opcional já fizer isso de graça, **não vender**; é o primeiro passo deste capítulo).

**Ganchos.** `Player.HaveRequirements`, `Player.ConsumeResources`, `Inventory.CountItems`/`HaveItem` quando chamados a partir de `InventoryGui.DoCrafting` e `Player.PlacePiece`. Estratégia segura: postfix em `Player.HaveRequirements` e `Player.ConsumeResources` que, se o jogador é VIP e está perto de uma bancada, soma/consome nos `Container` do raio (mesma rotina do item 10). Já existe `CraftPatches.cs` (item 11); somar lá.

**Autoridade.** O consumo de itens de baús é uma mudança do mundo: o servidor precisa conhecer o resultado. Como o `Container` é um objeto com dono, o cliente que fabrica pede posse (`ZNetView.ClaimOwnership`) como já faz ao abrir um baú. Só baús onde `Container.CheckAccess(playerId)` é verdadeiro e `PrivateArea.CheckAccess` passa. **Não consumir se o baú está aberto por outro jogador** (`m_inUse`).

**PvP.** Em zona de raid, baú de base inimiga nunca conta (CheckAccess cobre). Raio pequeno (5 m da bancada) para não virar "craft de longe".

**Config** (`[AesirCraft]`): `Enabled`, `Radius=5`, `VipOnly=true`. **Riscos:** consumo duplicado; item tirado de baú sem sincronizar; compatibilidade com `CraftPatches` (lote 99). **Teste:** receita com material parte no inventário, parte no baú; baú de outro jogador (não conta); não-VIP no vanilla. **Esforço:** 1,5 dia (depois do 10).

---

## Aesir 19: vaga reservada com o servidor cheio

**Abordagem.** Quando o servidor está cheio, o VIP ainda entra. Há dois mecanismos possíveis:

| | A. Vaga reservada (VIP sempre entra, até N a mais que o limite) | B. Expulsar o último não-VIP |
|---|---|---|
| Como | Aceitar a conexão acima de `ServerMaxPlayers` até `+VipReserved` | Ao chegar o VIP com o servidor cheio, desconectar um não-VIP (o último a entrar ou o mais ocioso) |
| Justo | Sim: ninguém é expulso | Não: pune quem não paga |
| Recomendado | **Sim** | Não |

Recomendo A, com `VipReservedSlots=3` (config).

**Ganchos.** `ZNet.RPC_PeerInfo` / `ZNet.OnNewConnection`, onde o servidor recusa com `ConnectionStatus.ErrorFull` quando `GetNrOfPlayers() >= m_serverPlayerLimit`. Prefixo em `ZNet.RPC_PeerInfo` (ou no ponto exato que compara com `m_serverPlayerLimit`; ver o método no assembly) que, para um peer VIP, sobe o limite em `VipReservedSlots` para aquela decisão. **O VIP precisa ser identificado já na conexão**: o ID de plataforma está no `ISocket.GetHostName()` (`IsVip(host)`, usado em `Pets.IsVipPeer`).

**Autoridade.** Totalmente do servidor, na conexão. **PvP.** Nenhum. **Config** (`[VipSlots]`): `Enabled`, `ReservedSlots=3`. **Arquivos:** novo `VipSlots.cs` (Deadheim; roda só no servidor, `ZNet.instance.IsServer()`). A lista VIP é lida pelo `VipList`; no ponto de conexão já há um caminho existente (`DirectJoinFlow.cs`): ver se ele faz a decisão de lotação antes de criar outro.

**Riscos.** O DatHost pode impor o limite de slots **por fora do jogo** (no painel); nesse caso o mod não adianta. Verificar `ServerPlayerLimit` no cfg do servidor. O jogo fica com `limite + 3` jogadores: testar custo. **Teste:** servidor com limite baixo (2) de teste, 2 não-VIP conectados, VIP entra; um 4º não-VIP não entra; o VIP some e o limite volta. **Esforço:** 1 dia.

---

## Aesir 31: efeito de abate

**Abordagem.** Quando um VIP (com o cosmético ativo) mata outro jogador em PvP, toca um efeito visual no corpo da vítima. Opções: `padrao` (raios), `almas` (Halloween). Pura visual, sem dano, sem recompensa extra, sem som alto.

**Ganchos.** O Deadheim já detecta a morte PvP: `PvpPatches.DeathPatch` e `PvpRules.ClassifyDeath` (distinguem PvP, PvE e pena de PK). Postfix ali no **servidor** quando a classificação é morte PvP legítima: ler o cosmético ativo do **assassino** (`CosmeticosApi.Has(killerId, "abate:<id>")` + escolha dele), e mandar um RPC `DH_AbateFx(posição, efeitoId)` aos clientes a até 60 m. O cliente instancia partículas locais (`ParticleSystem` próprio ou prefab visual do jogo **sem `Aoe`, `Projectile` ou luz de alto custo**).

**Onde a checagem ocorre.** Servidor: posse do cosmético e validade da morte. O cliente só desenha. **Regras PvP:** só em morte PvP que conta; nunca em morte PvE, em arena (`IsArena`) ou em morte por pena de PK; não mostrar o nome do assassino acima do normal (nada de "troféu" que revele posição); duração 3 s; sem bloqueio de visão (partículas pequenas, sem tela cheia).

**Config** (`[AbateFx]`): `Enabled`, `Radius=60`, `Seconds=3`. **Escolha:** `/abate <id>|off` (cliente → servidor valida posse). **Arquivos:** novo `AbateFx.cs`; registrar chaves `abate:padrao`, `abate:almas` no `Cosmeticos`; hook em `Pvp/PvpPatches.cs` (só uma chamada). **Riscos:** o efeito pode atrapalhar a visão do oponente; prefabs vanilla têm `Aoe`. Usar partículas próprias. **Teste:** VIP mata em PvP (aparece nos 2 clientes), mata mob (não), morre em arena (não), não-VIP (não). **Esforço:** 1,5 dia.

---

## Aesir 32: peças decorativas exclusivas

**Abordagem.** Peças de construção só cosméticas (estátuas, bandeira do Deadheim, vaso, tapete), liberadas por VIP/ficha. **Sem função**: sem estrutura que bloqueie raid, sem proteção, sem abrigo (`Piece.m_comfort` zero, sem `WearNTear` com força alta), sem baú. Isso evita que "decoração" vire parede.

**Ganchos.** Registrar peças novas no `PieceTable` do Hammer pelo mesmo caminho que `ClonedItems.cs` usa para itens (Jotunn). Limitar a construção por `Player.PlacePiece`/`Player.CheckCanRemovePiece`: postfix em `Player.TryPlacePiece` (servidor via RPC não existe; o cliente decide), então a **trava de VIP é no cliente e no servidor**: no servidor, `ZNetScene.CreateObject`/`ZDOMan` aceita qualquer prefab. Para impedir cliente modificado de instanciar, adicionar checagem no servidor em `WearNTear.Awake`/`Piece.Awake` da peça cosmética: se `Piece.m_creator` não é VIP/dono, destruir (`ZNetScene.Destroy`) e logar.

**PvP.** Peça com `m_noClipping`/colisor leve; não pode ser colocada dentro de wards alheias (o jogo já bloqueia por ward). Não conta como parede para o raid (sem `WearNTear` ou com vida 1). Teto de quantidade por jogador (config).

**Arte.** Esta é a dependência: modelos e texturas feitos à mão (assetbundle). Primeira versão com peças existentes do jogo recoloridas (mesh vanilla duplicado), ou adiar até ter arte.

**Config:** `[Decor] Enabled`, `MaxPerPlayer=50`, `Prefabs`. **Arquivos:** novo `DecorPieces.cs`, assetbundle em `Assets`. **Riscos:** arte; peça cosmética quebrando regra de raid; save com prefab desconhecido se o mod sair. **Teste:** VIP constrói; não-VIP não vê no martelo; cliente adulterado: peça some; construir sobre ward alheia bloqueia. **Esforço:** 2 dias + arte. Fica atrás dos outros.

---

## Aesir 33: lápide personalizada

**Abordagem.** A lápide (`Tombstone`) que o jogador deixa ao morrer ganha um visual escolhido (cruz, caveira de Halloween) e uma frase curta opcional ("Fulano, morto por ..."). **A lápide continua funcionando igual** (mesmo inventário, mesma recuperação, mesmo prazo).

**Ganchos.** `Player.CreateTombStone` (cria o prefab `Player_tombstone` e o preenche) e `TombStone.Setup(name, ownerUID)`. Postfix em `TombStone.Setup`/`Awake` no cliente que enxerga: ler o cosmético do **dono** (UID) e trocar o visual (material/modelo de filho) ou instanciar um filho decorativo. Para o jogador que morreu, `dh_lapide` vai no `ZDO` da própria lápide, escrito pelo cliente dono no momento da criação (o servidor valida `CosmeticosApi.Has(playerId, "lapide:<id>")` num RPC antes, ou revalida no ato de ler).

**Verificação.** Cliente lê `dh_lapide` do ZDO da lápide; para impedir cliente modificado de se dar "caveira" sem ter, o servidor confere ao receber o ZDO novo (`ZDOMan`/ao criar) e apaga a chave se o dono não tem o cosmético. Mais simples: aceitar o risco (puramente visual), mas o `Pets` fecha melhor com tabela do servidor; usar o mesmo padrão da seção 36-D.

**PvP.** A frase não pode mostrar posição, nem insultar: sanear como `SanitizeName` (sem `<`, `>`, controle, 40 caracteres). Não altera colisão nem loot: não pode servir de armadilha ou de esconderijo.

**Config** (`[Lapide]`): `Enabled`, `MaxTextLength=40`. **Escolha:** `/lapide <id>|off|texto <txt>`. **Arquivos:** novo `Lapide.cs`; chaves `lapide:cruz`, `lapide:caveira`. **Riscos:** variar o visual sem quebrar `TombStone.m_floater`/colisor; cliente que não tem o mod (todos têm, é o mod principal). **Teste:** morrer com e sem cosmético; recuperar itens normal; outro cliente vê o visual. **Esforço:** 1,5 dia.

---

## Chama colorida 29

**Abordagem.** Tocha, lanterna e fogueira pessoais do VIP queimam de outra cor (roxa no Halloween, azul, verde). Pura cor da luz e das partículas. Hoje "não existe sistema de chamas"; o projeto é construí-lo pequeno.

**Ganchos.** Tocha na mão: `VisEquipment.AttachItem` instancia o prefab do item na mão; o objeto filho tem um `Light` e um `ParticleSystem` (`flame`). Postfix em `VisEquipment.AttachItem` (ou `Humanoid.EquipItem`) que, se o jogador tem `chama:<id>` ativo, ajusta `Light.color` e as cores/`startColor` do `ParticleSystem` do filho. Lanterna idem (`Lantern`). Para a tocha de parede e fogueira (`Fireplace`), fora da v1: só o que o jogador **veste/segura** (menos arriscado, sem mexer em peças do mundo).

**Sincronização.** O visual do item na mão de **outro** jogador é criado em outro cliente. Cada cliente precisa saber a chama do outro: escolher `dh_chama` no ZDO do jogador (como `dh_pet`) e o cliente que renderiza lê. Verificação de posse pelo servidor como no 36-D (tabela de cosméticos transmitida pelo servidor), senão cliente modificado acende qualquer cor (só cosmético, mas fechar de graça aqui é fácil).

**PvP.** Cor não pode aumentar ou diminuir alcance de luz: **não mexer em `Light.range`/`intensity`**, só `color`. Cor também não pode ficar branca/ofuscante. Em PvP noturno uma luz diferente pode denunciar, mas igual à tocha normal.

**Config** (`[Flames]`): `Enabled`, `Palette` (`id=#RRGGBB`). **Escolha:** `/chama <id>|off`. **Arquivos:** novo `Chamas.cs`. **Riscos:** o prefab tem mais de um `ParticleSystem`/`Light`; `MaterialPropertyBlock` vs cópia de material (usar bloco para não vazar para outros jogadores). **Teste:** 2 clientes, um escolhe roxo, o outro vê roxo; tirar e pôr a tocha mantém; sem cosmético: vanilla. **Esforço:** 2 dias.

---

## Montarias 28 (nome) e 30 (skins) — estado do código existente

**O que já existe sem commit** (leia antes de mexer; não sobrescrever): `MountSettings.cs` (+74 linhas: `SkinList`, `SkinOptions()`, `TryGetSkinColor`, configs `NomeMontariaAtivo/SoVip/Tamanho`, `SkinMontariaAtivo/SoVip`), `Integration/VipProvider.cs` (VIP por reflexão no `VipList`, servidor valida cada pedido: `IsVip(sender)`), `Riding/RiderServer.cs`/`RiderNet.cs` (`HandleSetCosmetic(sender, argument, isSkin)`, `Apply(rank, mounts, names, skins)`), `Prefabs/MountPrefab.cs` e `MountVisuals.cs` (aplicam tinta). `MountClipPlayer.cs` tem alterações de outra sessão (não tocar).

**Falta (a confirmar lendo o diff completo, que não li inteiro):** (a) menu para escolher nome/skin (`MountMenu.cs` não aparece modificado); (b) persistência por jogador (onde o nome/skin escolhido fica salvo no servidor); (c) mostrar o nome em cima da montaria (`Patches/MountHudPatch.cs`); (d) comando de chat opcional; (e) ver se o nome já passa pelo sanitizador (sem `<`/`>`).

**Checagem de VIP.** Servidor, a cada pedido (já projetado assim). **PvP.** Nome com limite de 20 caracteres e sanitizado; a skin só muda a cor (mesmos atributos); skin não pode tornar a montaria invisível ou confundi-la com um mob hostil (limitar a paleta de tintas e nunca alfa < 1).

**Teste.** VIP muda nome (aparece em outro cliente), skin (idem), não-VIP é recusado; relogar mantém; trocar de montaria preserva por montaria. **Esforço:** 0,5 dia cada, para revisar, compilar e fechar. Publicar como Montarias 0.3.x. **Atenção:** não commitar o trabalho da outra sessão sem acordo; falar com ela antes (`local_257e8c66` no plano).

---

## Montarias 20: estábulo

**Abordagem.** Uma construção "estábulo" (peça) onde o jogador guarda e chama suas montarias (hoje existe `MountHub`/apito, `WhistleItem`). Estábulo VIP: guarda mais montarias e permite trocar a ativa de qualquer lugar da base, **sem teleportar a montaria**.

**Ganchos.** `Prefabs/MountHub.cs` e `MountRoster.cs` já organizam as montarias do jogador. O estábulo seria uma `Piece` com um `Interactable` que abre o menu `MountMenu`. Não precisa de Harmony novo: registrar a peça no martelo e ligar ao `MountMenu`.

**Posse.** Servidor: quantidade máxima de montarias guardadas por jogador (`MountRoster`), VIP maior (config). **PvP.** O estábulo não protege nem esconde montaria; chamar a montaria respeita `CombatLock.cs` (já existe: trava em combate). Sem voo a partir do estábulo.

**Dependência.** O que o estábulo faz além de "menu" precisa ser definido: o apito e o menu já listam as montarias. **Pergunta ao usuário:** o que o estábulo adiciona que o apito não dá? Sem isso, o item é uma peça decorativa. Proposta: guarda de montarias extras (slots VIP) + visual da montaria no estábulo. **Esforço:** 2 dias, **depois** de 28/30 fechados. Arte para a peça (pode reusar uma casa de madeira vanilla recolorida).

---

## Pacote de Halloween (H1)

**Conteúdo aprovado:** título Assombrado, vela Abóbora, chama roxa, lápide de caveira, efeito de abate almas, comprados pela `FichaHalloween` (400 DC, 24/10 a 07/11, só cosmético).

**Abordagem.** É montagem, não código novo, **depois** de 29, 31, 33, vela e título:
1. Registrar chaves: `titulo:assombrado`, `chama:roxa`, `lapide:caveira`, `abate:almas`, `vela:abobora` em `Cosmeticos` (já aceita chaves livres).
2. `FichaParaChave` hoje mapeia **uma** chave por ficha. A `FichaHalloween` concede **cinco**: mudar o dicionário para `string[]` (ou nova função `ConcederPacote`) e o `ServerGrant` para cada.
3. Velas lê `vela:abobora` pelo registro (`Velas.Integration` com `CosmeticosApi.LocalHas`), com cor e emissão laranja.
4. Título: já existe a API `CosmeticosApi` com `titulo:fundador`; somar `titulo:assombrado` (texto e cor).

**Janela de venda e regras.** `from=2026-10-24;to=2026-11-07` em `DeadcoinShop.DefaultItems` controla a venda; o cosmético **fica permanente** (a ficha é comprada na janela, o item é do jogador).

**Risco de prazo:** ver a tabela de ordem. Se 29/31/33 não fecharem até 23/10, mover o `from=` ou entregar um pacote parcial com aviso. **Decisão sua.** **Teste:** resgatar a ficha, cada chave aparece, cada cosmético funciona para o dono e é visto por outro cliente; ficha usada duas vezes não duplica. **Esforço:** 0,5 dia de montagem.

---

## Passe de temporada (H2): NPC Arauto da Temporada

**Escopo aprovado:** 10 níveis por desafios semanais, 3 gratuitos, 500 DC retroativo, só cosmético, **sem abate PvP contando ponto**, temporada ~4 semanas desde a abertura.

**Abordagem.** Um NPC "Arauto da Temporada" no NpcValheim (molde do `QuestGiverNpc`, que já tem quests e progresso: `QuestDatabase`, `QuestProgressRules`, `QuestStore`). O passe é uma trilha de 10 níveis, cada nível uma recompensa cosmética (títulos, chamas, lápides, pets de temporada, velas). Pontos vêm de **desafios semanais** de não-PvP: coletar, construir, matar chefes/mobs de PvE, explorar, pescar, usar o NPC. A `FichaTemporada` (500 DC) destrava a trilha paga **retroativamente** (as recompensas já ganhas em pontos são entregues).

**Ganchos.** Reaproveitar `Npc/QuestGiverNpc.cs` e `Persistence/QuestProgressRules.cs` para contar desafios; o servidor é autoridade (progresso no `QuestDatabase`). A contagem **exclui** abates de jogador: nas regras de progresso, ignorar mortes cuja vítima é `Player` (`QuestKillTracker` já registra por `Character.m_lastHit`: filtrar `victim is Player`). Mortes na arena e bounty não contam.

**Dados.** Novo arquivo `Persistence/SeasonDatabase.cs` (LiteDb como o resto): `playerId`, `seasonId`, `points`, `claimed[]`, `paid`. Datas em config (`SeasonStart`, `SeasonEnd`) e `Deadheim.RelogioServidor`. `WeekOf` já é compartilhado (visto em A1-A4).

**Recompensas.** São chaves do registro `Cosmeticos` (`ServerGrant`); nada de item de combate, nada de Deadcoins grátis em volume (3 níveis grátis dão cosmético simples).

**PvP.** Nenhum desafio de PvP; nenhuma recompensa dá vantagem. Sem "ranking de temporada" público (evita farm).

**Config** (`[Season]`): `Enabled`, `SeasonId`, `Start`, `End`, `PointsPerLevel`, `FreeLevels=3`, `Rewards` (nível=chave). **Arquivos:** NpcValheim `Npc/SeasonHeraldNpc.cs`, `UI/SeasonWindow.cs`, `Persistence/SeasonDatabase.cs`; Deadheim: só as chaves. **Riscos:** o maior item novo de UI; balancear pontos. **Teste:** concluir desafio, pontos sobem; abate PvP não conta; comprar a ficha libera retroativo; virar a semana reinicia desafios; fim da temporada congela. **Esforço:** 4 a 5 dias. Entregar depois do Halloween, em novembro.

---

## Plantas salvas (24)

**Abordagem.** O jogador salva uma construção (até ~300 peças) e a recoloca depois. **Cada peça passa por `Player.PlacePiece`/`TryPlacePiece`** para herdar as travas já existentes de ward, raid e combate; sem wards/portais/tokens; fora de combate. PlanBuild **não** pode entrar no launcher.

**Ganchos.** Captura: varrer peças (`Piece.GetAllPiecesInRadius`) numa área escolhida e gravar `prefab`, posição relativa, rotação, `Piece.m_creator`. Colocação: `Player.PlacePiece(Piece piece, Vector3 pos, Quaternion rot, bool doAttack)` ou `Player.TryPlacePiece`; consumir os materiais do inventário/baús (pode somar com o item 13 mais tarde).

**Autoridade.** O cliente decide a colocação (vanilla). Para as travas valerem: `PrivateArea.CheckAccess`, `Util.IsRaidEnabledHere` e checagem de combate (`PvpState`) **antes de cada peça**, no cliente **e** reavaliadas pelo servidor num RPC `DH_Planta_Place` que devolve "ok" por lote; peças sem OK são descartadas. Cliente modificado ainda pode instanciar peça solta (já acontece hoje no jogo vanilla): o objetivo é não abrir **novo** caminho.

**Limites.** 300 peças por planta, cooldown entre colocações de 10 s, 5 plantas salvas por VIP, uma colocação por vez, peça a peça com fila (para não travar o servidor), dentro do alcance de construção do jogador (se a planta é grande, o jogador anda), e **só prefabs da lista permitida** (sem `guard_stone`, portal, `Container` com conteúdo, `ItemStand` com item).

**PvP/raid.** Proibido em zona de raid durante a janela e em combate; respeita ward alheia; consome material (sem construção grátis); nada de "planta de torre" instantânea: fila de ~2 peças/segundo.

**Armazenamento.** Arquivo por jogador no servidor (`plantas-<mundo>.json`) ou no cliente? Recomendo **no servidor** (VIP com PC novo mantém) com tamanho máximo.

**Config** (`[Blueprints]`): `Enabled`, `MaxPieces=300`, `MaxSaved=5`, `PlaceIntervalSeconds`, `PrefabAllow`. **Arquivos:** novo `Plantas.cs`, `PlantasUi.cs`. **Riscos:** o maior item de código do conjunto; desempenho (muitas `ZDO`); colisão de peças; duplicar o que o PlanBuild faz (usuários já não o têm). **Teste:** salvar uma casa de 80 peças, colocar fora de ward alheia, dentro de ward alheia (bloqueia), durante combate (bloqueia), sem material (bloqueia), VIP expirado (bloqueia nova colocação). **Esforço:** 5 a 6 dias. Quase pelo fim da fila.

---

## Transmog (35)

**Abordagem.** O VIP escolhe a **aparência** de um item (capa, capuz, armadura, arma) mantendo o status real do item de verdade. Só visual: o item equipado continua sendo o que dá defesa/dano.

**Ganchos.** `VisEquipment.SetChestItem/SetLegItem/SetHelmetItem/SetShoulderItem/SetUtilityItem/SetRightItem/SetLeftItem` recebem o **nome do prefab** a mostrar. A troca é um prefixo em `VisEquipment.SetChestItem` etc. (e `Humanoid.SetupVisEquipment`) que substitui o nome do item **apenas para efeito de visual**, a partir de um mapa `item real → aparência` do jogador. O mapa (`dh_transmog`) vai no ZDO do jogador para os outros clientes aplicarem o mesmo prefixo ao desenhar esse jogador.

**Autoridade.** Posse da aparência: o jogador precisa ter **o item de aparência** ou tê-lo desbloqueado (decisão de design: desbloqueia ao ter possuído o item uma vez? ou ao gastar uma ficha?). **Recomendo:** a aparência só vale entre itens da mesma categoria (peito→peito) e só de itens que o jogador já **carregou alguma vez** (registro no servidor, `transmog-desbloqueados`), para não virar "mostrar armadura de chefe que nem matou". Servidor valida no RPC de escolha.

**PvP (o ponto mais delicado).** Transmog **não pode esconder informação de combate**: não dá para trocar o visual de uma armadura pesada por uma roupa de tecido para o inimigo julgar errado a defesa? É informação visual, não mecânica, mas **a regra do projeto proíbe vantagem de combate**; disfarçar-se pode ser vantagem (ex.: parecer um NPC/guarda, ou parecer sem armadura). Limites: (1) só categorias de armadura equivalente de peso (não finge armadura leve de pesada? decisão sua); (2) nunca aparência de itens de NPC/chefe/guarda; (3) nome sobre a cabeça e marcador de guilda/PvP continuam; (4) em arena e raid o transmog é desligado (volta ao visual real). **Decisão do usuário:** nível de liberdade (só mesma classe de peso ou livre).

**Config** (`[Transmog]`): `Enabled`, `Whitelist`, `DisabledZones=arena,raid`. **Arquivos:** novo `Transmog.cs`, UI. **Riscos:** `VisEquipment` aceita o nome do prefab e carrega os filhos de `ObjectDB`; itens com `m_attachOverride`; arma em uso muda o *ataque animado*, então **só aparência de arma da mesma classe** (espada→espada). Sincronização com 2 clientes é o teste principal. **Esforço:** 5 dias. Por último.

---

## Item 21: marcadores extras no mapa — 3 opções concretas

O usuário não soube definir. Opções, da mais barata:

| Opção | O que é | Regra de PvP | Esforço |
|---|---|---|---|
| **A. Mais pinos pessoais** | O jogador comum tem um limite de pinos (vanilla não tem teto fixo, mas o servidor pode ter configurado um); o VIP ganha mais slots | Pino é só do jogador; nada compartilhado | 0,5 dia, só se existir um limite hoje (verificar `Minimap`; se não existe, a opção A é nula) |
| **B. Pinos com ícone cosmético** | Ícones extras (caveira, coroa, abóbora, bandeira) que só o VIP pode usar nos próprios pinos | Só ícone; não revela posição de ninguém | 1 dia (`Minimap.PinType` aceita ícones novos via sprite; sincronização zero, pino é local) |
| **C. Pino compartilhado com o grupo (Aesir)** | O VIP marca um local e **só o grupo dele** vê | **Vantagem de combate** (informação em tempo real sobre inimigo). **Não recomendo; a regra do projeto proíbe mapa de guilda.** | n/a |

**Recomendação:** B (cosmético puro, barato). A só se o limite existir. C descartada. Reaproveita o padrão do `/batizar` (marcador no mapa) já existente. **Decisão sua:** B, A ou descartar de vez.



# Resumo das decisões que dependem de você

1. Halloween: mover a janela da `FichaHalloween` (24/10) para quando o pacote estiver pronto, ou cortar o pacote.
2. Item 21: opção A, B ou descartar (recomendo B).
3. Item 19: vaga reservada (recomendado) em vez de expulsar não-VIP; conferir se o DatHost impõe o limite de slots por fora.
4. Item 20 (estábulo): o que ele faz que o apito não faz.
5. Transmog (35): liberdade de aparência (só mesma classe de peso é o mais seguro).
6. Barbeiro: confirmar a paleta de cor de pele (ou nem permitir pele).
7. Retirar `FichaBarbeiro`, `FichaTemporada` e `FichaBrasao` da loja até terem efeito (já listado em `pendencias-deadheim.md`).

