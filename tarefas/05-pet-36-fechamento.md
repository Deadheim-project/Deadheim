> **Briefing para agente de desenvolvimento** (leia primeiro `00-LEIA-PRIMEIRO.md` desta pasta).
> Repositório: **Deadheim (+ linha da ficha no npc-valheim)** (em D:\deadheim\). Release: **R3 Deadheim + NpcValheim**.
> Regras: responda em português; seja econômico (leia só o necessário, não imprima arquivos grandes, não crie subagentes); implemente SÓ este item; não faça commit, push, release nem deploy (o usuário decide); compile com `dotnet build -c Release` e informe o resultado; não sobrescreva alterações sem commit de outras sessões (confira `git status` antes); ao terminar, liste arquivos mudados e o que ficou sem teste no jogo.
> O texto abaixo é o PROJETO; o agente confirma nomes de métodos do jogo no assembly publicizado em D:alheim-ref antes de escrever Harmony.

# Item 36: pet cosmético — fechamento

## A. Conferência do que há

`Pets.cs` (810 linhas, sem commit, compila, não testado no jogo) já tem: catálogo por config (`PetCatalog`), comandos `/pet`, `/pet <id>`, `/pet off`, `/pet nome`, `/pet resgatar <id>`, RPC cliente↔servidor (`DH_Pet_ToServer/ToClient`), validação de posse no servidor (`ServerOwns`: Aesir só enquanto VIP, resto por `CosmeticosApi.Has("pet:<id>")`), `ClonedItems` já define a `FichaPet`, escolha guardada em `m_customData` e republicada no ZDO após relogar, scan 1 s de jogadores a até 40 m, limite de 12 pets visíveis, limpeza de componentes do jogo (`StripGameComponents`), seguidor local (`PetFollower`), pet some em barco e montaria, some em arena configurada por `PvpZones.IsArena`.

Do projeto original, **o que já foi decidido:** pet no cliente (opção A), nenhum colisor, sem som, 12 visíveis, Aesir escolhe 1 dos 3 básicos, ficha libera 1 da lista. **O que falta de projeto:** o que fazer com o corvo (item D5), verificação por outros clientes (D3), e a regra de zonas (D2).

**Dúvidas que a leitura do código deixa:**
1. A regra "Aesir escolhe 1 pet entre os 3 básicos" **não está implementada**: `ServerOwns` dá os **3** básicos a todo VIP. Decida: os 3 (mais simples, é o que está no código) ou só 1.
2. `Cosmeticos.cs` diz "FichaPet é tratada separadamente", e `Pets.cs` trata por `/pet resgatar`; a ficha é consumida no cliente **antes** da resposta do servidor (`inv.RemoveItem` seguido de `SendToServer("resgatar")`). Se o servidor recusar, a ficha já foi. Corrigir: só remover a ficha depois do "ok" do servidor (D1).
3. `WriteLocal` grava `lp.m_customData` e o `ZDO`; `m_customData` é do jogador salvo, ok.

## B. Parâmetros do Animator por prefab (verificado no assembly, sem abrir o jogo)

Os controladores de Animator vivem nos assetbundles do jogo, não no `assembly_valheim.dll`; **o que o assembly mostra é quais parâmetros o código do jogo escreve**. Procurei os nomes como texto no assembly original (`valheim_Data\Managed\assembly_valheim.dll`):

| Parâmetro | Tipo | Quem escreve | Presente no assembly |
|---|---|---|---|
| `forward_speed` | float, **m/s reais** | `Character.UpdateWalking` | sim |
| `sideway_speed` | float | `Character` | sim |
| `turn_speed` | float | `Character` | sim |
| `onGround` | bool | `Character` | sim |
| `inWater` | bool | `Character` | sim |
| `flying` | bool | `Character` (aves) | sim |
| `dead` | bool | `Character` | sim |
| `sleeping` / `wakeup` | bool / trigger | `MonsterAI` (criaturas que dormem) | sim |
| `alert`, `attack`, `blocking` | | IA e combate | sim |
| `Speed` | | **não é parâmetro de Animator de criatura** | só aparece como texto genérico (101 ocorrências, de outras classes) |
| `Sit` (trigger de criatura) | | **sem uso de criatura confirmado** | só 5 ocorrências, provavelmente emote do jogador |

**Consequência direta: o catálogo padrão de `Pets.cs` está errado.** Usa `Speed` e `Sit` para todos. O código filtra por parâmetros existentes (`_hasSpeed`), então não loga erro, mas **o pet desliza sem animar a caminhada**. Correções:

1. `SpeedParam` padrão = `forward_speed`, e o valor passado deve ser **em m/s reais do pet** (o código atual passa `speed01` de 0 a 1, o que mantém a animação em "quase parado"). Os limites de transição do controlador usam a velocidade de andar/correr do prefab (`Character.m_walkSpeed`, `m_runSpeed`). **Antes** de `StripGameComponents`, ler esses dois valores do `Character` do prefab original e guardar no `PetDef` (campos novos `WalkSpeed`, `RunSpeed`); movimentar o pet a esses valores e passar a velocidade real.
2. Também definir `onGround=true` (se existir) a cada frame; sem isso o controlador pode ficar em queda.
3. `SitTrigger` não deve ser tratado como trigger. Para dormir/sentar, usar a bool `sleeping` quando o prefab a tiver (detectado por `Animator.parameters`, como o código já faz); senão o pet só fica parado. Remover `Sit` do catálogo padrão; deixar o campo vazio.
4. **Registrar uma vez por prefab, no log, a lista de parâmetros do Animator** (nome e tipo). É o único jeito de confirmar o resto sem o jogo aberto; fica como comando de depuração `/pet debug` (só admin) e entra no plano de teste.

Prefabs do catálogo (existência confirmada pela lista de personagens do Jotunn, informação do usuário): `Hen`, `Chicken`, `Boar_piggy`, `Lox_Calf`, `Wolf_cub`. Os controladores deles são os que a IA do jogo usa via `ZSyncAnimation`, que escreve exatamente os parâmetros da tabela acima. Parâmetros extras de cada bicho (por exemplo, galinha ciscando) **não puderam ser lidos sem o jogo**; ficam para o log de depuração.

**Corvo.** O prefab `Raven` não está na lista de prefabs do Jotunn; o `Raven` do jogo (Hugin, do tutorial) é uma criatura de cena, não um prefab spawnável. Alternativas voadoras/no ombro encontradas: `Crow` (corvo-comum de pássaro, `RandomFlyingBird`, animações de pouso e voo) e `AshCrow` (versão das Cinzas, mesmo componente). **Proposta:** `corvo=Crow`, escala 0,6, posado **no ombro** (item D5). Se `Crow` for ruim visualmente em teste (o modelo é de ave pequena), plano B: pet voador baixo (flutua a 1,2 m do dono, com `forward_speed`/`flying`). Fica para a etapa de teste visual.

## C. O que ainda precisa de código

| # | Pendência | Onde | Esforço |
|---|---|---|---|
| D1 | Remover a ficha só depois do "ok" do servidor | `Pets.cs` (`resgatar`) | 1 h |
| D2 | Ocultar em arena/partida/raid | `Pets.ShouldHide` | 0,5 dia |
| D3 | Posse verificada pelos outros clientes | `Pets.cs` (servidor) | 1 dia |
| D4 | Nome sobre o pet | `PetFollower.SetName` | 0,5 dia |
| D5 | Corvo no ombro | `PetFollower` | 1 dia |
| D6 | Corrigir parâmetros do Animator (seção B) | `Pets.cs` | 0,5 dia |
| D7 | Ficha de Pet em `[DeadcoinShop] Items` (150 DC) | NpcValheim | 0,25 dia |
| D8 | Decidir a regra do Aesir (1 ou 3 pets) | decisão | — |

### D2. Ocultar em arena, partida e raid

Fontes de verdade (sem criar API nova):
- **Arena (zonas do Deadheim):** `Pvp.PvpZones.IsArena(posição)`, já usado.
- **Partida de arena do NpcValheim (o "battleground" real):** o ZDO do **jogador** tem `npcv_arenaMatch` (long) e `npcv_arenaPhase` (int), escritos por `ArenaClient.cs:248-250`. O pet lê no ZDO do dono: `zdo.GetLong("npcv_arenaMatch".GetStableHashCode(), 0L) != 0` esconde. Sem dependência de DLL, qualquer cliente lê (o ZDO é replicado). Esconder também o pet dos **outros** jogadores na partida, para ninguém usá-lo como referência de posição.
- **Raid:** `RaidSystem.Util.IsRaidEnabledHere(pos)` (está na zona de ward de raid) **e** `!Util.IsRaidDisabledThisTime(pos)` (a janela de horário está aberta); opcionalmente também `Util.IsInRaidPvpArea`-equivalente (o método de `ForcePvpInZones`, `Util.cs` ~l.282). Esconde só durante a janela de raid; fora dela o pet aparece. Valores do relógio vêm de `RelogioServidor`, não do relógio local.
- **Combate/PvP aberto:** manter visível (recomendação do projeto original); decisão do usuário.

Implementação: `ShouldHide(Player player)` recebendo o jogador (não só a posição), com as três checagens; a chave `PetHiddenZones` continua a controlar (`arena,battleground,raid`), com `battleground` mapeado para `npcv_arenaMatch`.

### D3. Verificação de posse pelos outros clientes

Hoje o cliente que desenha o pet de outro lê `dh_pet` do ZDO dele **sem checar nada**. Um cliente modificado escolhe qualquer pet (inclusive o de evento) só para si e os outros o veem. Dano: só cosmético, mas fecha-se barato:

1. O servidor, ao validar `set`/`off`, guarda `playerId → (petId, nome)` em memória e **transmite** a tabela aos clientes (RPC `DH_Pet_Sync`, mudança individual + tabela inteira ao entrar um jogador).
2. O cliente que desenha só mostra o pet do jogador X se a tabela tem X e o `petId` do ZDO bate. O ZDO continua útil para disparar rápido, mas a **fonte de verdade é a tabela**.
3. Aesir básico: o servidor revalida a cada 60 s (`IsVipPeer`); se o VIP caiu, remove da tabela e transmite.
4. Nome: sai pela mesma tabela, já saneado.

### D4. Nome sobre o pet

`TextMeshPro` 3D (não uma UI de tela) filho do pet, com a fonte do jogo (`TMP_FontAsset` de `Hud.instance`), billboard para a câmera em `LateUpdate`, escala fixa, visível a no máximo 15 m, **desligado nos mesmos casos do pet** (arena etc.). Texto saneado (20 caracteres). Cuidado: o pet não pode revelar posição de jogador oculto (invisibilidade/stealth): não mostrar nome se o dono está agachado e oculto.

### D5. Corvo no ombro

Se a escolha for ave (`Crow`/`AshCrow`): em vez de seguir no chão, ser filho de um osso do dono: `Player.m_visEquipment`/`Animator` do jogador, osso do ombro direito (achar por nome, por exemplo `RightShoulder`, na hierarquia do esqueleto do jogador; **confirmar o nome do osso lendo a hierarquia em runtime** no log). `localPosition` pequeno, escala 0,6 (cabe na altura do ombro, nunca na do rosto), animação parada (`onGround=true`, sem `flying`). Cada cliente anexa ao **modelo do jogador que desenha**. Esconde ao montar/barco como os outros. Ao agachar/esconder, esconder também o corvo.

### D7. Ficha de Pet na loja do NpcValheim

Uma linha em `DeadcoinShop.DefaultItems`: `prefab=FichaPet;amount=1;price=150`. O item `FichaPet` já existe em `ClonedItems.cs` (Deadheim) e o texto cita lobo, lox e corvo. O servidor de produção usa a cfg dele, então o admin acrescenta a mesma linha no `[DeadcoinShop] Items`. **O release do NpcValheim e o do Deadheim saem juntos** (a ficha precisa existir no ObjectDB, senão a entrada é pulada por `Available`).

## D. Plano de teste no jogo com dois clientes

Preparação: servidor de teste local ou o de produção em manutenção; cliente A (VIP, é o jogador "dono") e cliente B (não-VIP, observador). Configurar `PetEnabled=true`. Se em produção, **nenhum jogador comum online**. Dar ao A `FichaPet` (admin), e VIP ao A. Anotar o log do jogo de cada cliente.

| # | Passo | Esperado |
|---|---|---|
| 1 | A: `/pet` sem VIP prévio (testar antes de dar VIP) | "Você não tem nenhum pet" |
| 2 | Dar VIP a A. A: `/pet` | Lista os 3 básicos (ou 1, conforme D8) |
| 3 | A: `/pet galinha` | Pet aparece, segue a 2,5 m atrás, vira para onde anda |
| 4 | B está perto de A | B vê a galinha de A seguindo A, **animando andar** (corrigir parâmetros primeiro; sem isso, desliza) |
| 5 | A anda, corre, para | Animação acompanha, velocidade proporcional; ao parar, para; depois de 5 s pode sentar/dormir só se houver `sleeping` |
| 6 | A: `/pet nome Bolinha` | Nome aparece sobre o pet para A e B, saneado; caracteres `<`, `>` são removidos |
| 7 | B se afasta a 50 m | A galinha some em B (sem erro); volta ao se aproximar |
| 8 | A usa portal ou corre >30 m rápido | Pet reaparece ao lado de A |
| 9 | A entra em barco, depois monta uma montaria | Pet some, volta ao desembarcar |
| 10 | A entra na arena (zona do Deadheim) e depois em partida de arena do NpcValheim | Pet some para A e para B; volta ao sair |
| 11 | Durante janela de raid na zona de raid | Pet some; fora da janela volta |
| 12 | A: `/pet resgatar wolf_cub` com ficha | Ficha sai **só depois** do ok; `/pet wolf_cub` funciona |
| 13 | A: `/pet resgatar wolf_cub` de novo com outra ficha | "Você já tem este pet"; ficha não é gasta (após D1) |
| 14 | A: `/pet corvo` (sem a ficha) | Recusado: "Você não tem este pet" |
| 15 | Corvo no ombro (depois de D5) | Posado no ombro de A, visto por B, some ao montar |
| 16 | A relogar | Pet volta sozinho (republicado) e B o vê |
| 17 | Tirar o VIP de A (admin) e esperar até 60 s | Os pets Aesir somem para A e B; pet de ficha continua |
| 18 | Cliente adulterado (ou `/pet` com id de evento via ZDO, simulado por log) | B **não** vê (verificação D3) |
| 19 | 13+ jogadores com pet perto de B | Só os 12 mais próximos aparecem |
| 20 | `/pet debug` (admin) em cada prefab | Log com a lista de parâmetros do Animator: confirmar `forward_speed`, `sleeping` etc. |
| 21 | Desligar `PetEnabled` | Todos os pets somem; `/pet` recusa |
| 22 | Olhar o console do cliente e do servidor | Sem `FieldAccessException`, sem exceção por frame |

Critério de aceite: nenhum pet bloqueia porta/escada, nenhum monstro o ataca, nenhum jogador consegue acertá-lo, sem som, sem queda de FPS perceptível com 12 pets.

Antes do release: rodar a **checagem de acesso privado** (memória `acesso-privado-runtime`) no Deadheim. Os acessos novos prováveis (`Character.m_walkSpeed/m_runSpeed` lidos antes de remover o `Character`, osso do jogador, `m_customData`) devem usar `AccessTools`.

## E. Decisões que ficam com você (pet)

1. Aesir ganha **1** pet (projeto original) ou os **3** básicos (como está hoje no código)?
2. 150 DC para a ficha: confirmar.
3. Pet visível em PvP aberto e em combate (recomendado) ou esconde em combate?
4. Corvo: aceitar o `Crow` (ou `AshCrow`) no ombro, ou deixar o corvo para depois da primeira versão?
