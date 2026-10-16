> **Briefing para agente de desenvolvimento** (leia primeiro `00-LEIA-PRIMEIRO.md` desta pasta).
> Repositório: **Deadheim** (em D:\deadheim\). Release: **R5 Deadheim**.
> Regras: responda em português; seja econômico (leia só o necessário, não imprima arquivos grandes, não crie subagentes); implemente SÓ este item; não faça commit, push, release nem deploy (o usuário decide); compile com `dotnet build -c Release` e informe o resultado; não sobrescreva alterações sem commit de outras sessões (confira `git status` antes); ao terminar, liste arquivos mudados e o que ficou sem teste no jogo.
> O texto abaixo é o PROJETO; o agente confirma nomes de métodos do jogo no assembly publicizado em D:alheim-ref antes de escrever Harmony.

## Aesir 10: animais e fogueiras puxam do baú

**Abordagem.** Dois comportamentos para o VIP, dentro de um raio configurável (sugestão 8 m) e só baús que ele pode abrir: (a) animais domesticados (`Procreation`/`Tameable` com `MonsterAI.m_consumeItems`) acham comida em baús próximos em vez de esperar no chão; (b) fogueiras e braseiros (`Fireplace`) pegam combustível de baús próximos.

**Ganchos.** Dados do jogo: `Fireplace.UpdateFireplace`/`Fireplace.Interact` (combustível), `MonsterAI.UpdateConsumeItem` (procura item no chão) e `Container` (`m_inventory`, `CheckAccess(long playerID)`). Prefixo ou postfix em `Fireplace.UpdateFireplace` quando o combustível está abaixo de um limite; achar `Container`s por `Physics.OverlapSphere` com cache de 5 s (não varrer a cada frame). Para os animais, postfix em `MonsterAI.UpdateConsumeItem`: se não achou item, procurar no baú e simular o consumo (`Container.GetInventory().RemoveItem`).

**Onde a posse é checada.** Em quem tem autoridade do objeto: o dono do `ZNetView` do animal ou da fogueira (no servidor dedicado, o servidor ou o cliente mais próximo). **O dono do objeto pode não ser o VIP.** Por isso a regra é: o *dono da peça/animal* precisa ser VIP (o jogador que construiu a fogueira, gravado em `Piece.m_creator`, ou o `Tameable` com `m_owner`/`Character.m_nview "owner"`). Checar `creator` contra a lista VIP **no servidor**, que é quem executa o consumo para objetos sem cliente por perto. Se o objeto é executado por um cliente, ele não pode confiar em si mesmo: o consumo do baú tem de passar por RPC ao dono do baú ou ao servidor. Mais simples e seguro: **executar só no servidor** (dedicado) e registrar a regra para zonas carregadas.

**Regras de PvP.** Baú só abre se o jogador teria acesso (ward, `Container.CheckAccess`). Zona de raid: fogueira de base inimiga **não** puxa de baú alheio durante raid. Sem puxar de baú de outro jogador/guilda.

**Config** (`[AesirContainers]`): `Enabled`, `Radius=8`, `OnlyOwnerVip=true`, `PullIntervalSeconds=5`, `AllowedFuel` (lista). **Arquivos:** `Containers.cs` (já existe; reaproveitar a busca), novo `AesirPull.cs`. **Riscos:** custo de varredura com muitas fogueiras (cache e intervalo); duplicação de consumo entre servidor e cliente; o baú de outro jogador. **Teste:** fogueira de VIP acende puxando madeira; fogueira de não-VIP não; baú fora do raio não; baú trancado/ward alheio não; animal come do baú. **Esforço:** 2 dias. **Faz par com o 13** (mesma busca de baús, mesma regra de posse).

---

# FEITO (16/10/2026) — implementado na opção A (cliente do VIP)

**Status: código escrito e compilando; NADA testado no jogo.**

## Desvio de projeto: a autoridade mudou de lado

O briefing acima manda **executar só no servidor** e checar `Piece.m_creator` contra a lista VIP
no servidor. Isso **não é possível** no Deadheim como ele está hoje:

- A lista VIP (`VipListApi`) só responde com confiança para o **jogador local**
  (`IsLocalPlayerVip()`). `IsVip(string)` compara o id contra a lista sincronizada, mas nenhum
  código do repo resolve `peer → conta → VIP` no servidor; o único lugar que tenta é o
  `Pets.cs` (`IsVipPeer`), que é justamente o código não testado do item 36.
- Todo Aesir existente do repo roda no **cliente do próprio VIP**: 6, 7, 8, 9, 12, 14, 15, 16
  usam `VipList.VipListApi.IsLocalPlayerVip()`.
- Construir a resolução de identidade no servidor seria duplicar o que o
  `ValheimMontarias` já tem em `RiderServer.AccountIdOf` / `Integration/VipProvider`.

**Decisão do usuário: opção A** — rodar no cliente do VIP, como os outros Aesir.

O consumo continua seguro sem servidor próprio porque o `Container` é **objeto de rede**: o
cliente reivindica a posse (`ZNetView.ClaimOwnership`, o que o `Fireplace.Interact` do jogo já
faz), tira o item e o ZDO propaga. Quem pode puxar é decidido por `PrivateArea.CheckAccess`
(ward) e `Container.CheckAccess` (baú privado), igual ao `/depositar` do item Aesir 14.

**Consequência aceita:** um cliente modificado pode puxar do baú sem ser VIP. O dano é
limitado (só o próprio baú; ward e `CheckAccess` barram o alheio), e é a mesma exposição que os
itens 6/7/12/14 já têm.

## Arquivos mudados

| Arquivo | O que |
|---|---|
| `Deadheim/AesirPull.cs` | **novo** — o item 10 inteiro (~300 linhas) |
| `Deadheim/Plugin.cs` | uma linha: `AesirPullConfig.Bind(Config);` depois do `AesirConfig.Bind` |
| `Deadheim/Deadheim.csproj` | `<Compile Include="AesirPull.cs" />` — o projeto lista os arquivos um a um |

## O que ficou implementado

**`AesirPullConfig`** — seção `[AesirContainers]`, todos `Plugin.Synced`:
`Enabled=true`, `Radius=8` (1..30), `OnlyOwnerVip=true`, `PullIntervalSeconds=5` (1..60),
`FireplaceThreshold=0.5` (0.05..0.95), `AllowedFuel=""` (vazio = usa o `m_fuelItem` do prefab).

**`AesirChests`** — a busca compartilhada de baús, **é o que o item 13 vai reaproveitar**:
- `FindWith(origem, raio, playerId, sharedName)`: `Physics.OverlapSphereNonAlloc` com buffer
  fixo de 64 (sem alocar por chamada), filtrando por `Container.CheckAccess(playerId)`,
  `PrivateArea.CheckAccess` (ward), `!IsInUse()` (baú aberto por outro jogador) e
  `CountItems(sharedName) > 0`. Devolve o mais próximo.
- `Take(container, sharedName, n)`: **`ClaimOwnership` no `ZNetView` antes do `RemoveItem`** —
  sem isso o consumo roda só no cliente e o item não sai do ZDO para os outros.
- `OwnerIsVip(piece)`: responde "sim" **apenas** quando o criador é o jogador local VIP
  (ver limitação abaixo).

**(a) Fogueiras** — postfix em `Fireplace.UpdateFireplace` (o jogo chama a cada 2 s por
`InvokeRepeating`). Só age quando `m_nview.IsOwner()`, `!m_infiniteFuel`, `m_canRefill`, o
combustível está abaixo de `m_maxFuel × FireplaceThreshold` e o jogador local é VIP. Repõe no
máximo 50 unidades de uma vez e chama `InvokeRPC("RPC_AddFuelAmount", n)` — o **mesmo caminho
que o `Interact` do jogo usa**, então o efeito visual e a sincronia vêm de graça. Cache por
`GetInstanceID()` respeita o `PullIntervalSeconds`. Pula durante a janela de raid.

**(b) Animais** — postfix em `MonsterAI.UpdateConsumeItem`, agindo **só quando o retorno é
`false`** (o vanilla não achou comida no chão). Exige `Tameable` + `IsTamed()` + `IsHungry()`,
confere o dono pelo campo `owner` da ZDO, pega 1 unidade do baú e chama
`tameable.ResetFeedingTimer()` + o trigger `consume` + o `m_consumeItemEffects`.

## Correções aplicadas depois da revisão (16/10/2026)

Três defeitos relatados na revisão, todos corrigidos em `AesirPull.cs`:

| Defeito | Causa | Correção |
|---|---|---|
| **O item (b) nunca funcionava com a config padrão** | `OwnerIsVip(ai.GetComponent<Piece>())` — animal **não tem** componente `Piece`, a peça vinha nula e a função devolvia `false` com `OnlyOwnerVip=true` | A linha foi **removida**. Para animal quem responde pelo dono é `IsOwnerLocal` (o campo `owner` da ZDO). Documentado no XML doc do `OwnerIsVip` para ninguém repetir o erro |
| **A busca podia não achar baú nenhum em base densa** | `OverlapSphereNonAlloc` com buffer (64, depois 256) e camadas `piece, Default, static_solid, Default_small, item`: uma base densa tem centenas de colisores de parede/chão/escada em 8 m e o `OverlapSphere` **não tem ordem nenhuma** — o baú podia simplesmente não caber, e `FindWith` devolvia `null` sem erro | **Trocado por varredura da lista de peças do jogo**: `Piece.GetAllPiecesInRadius` sobre `Piece.s_allPieces` (o mesmo que o próprio `Piece` usa), filtrada por distância e pela camada `ghost`. Não há teto de colisor: por mais apertada que a base esteja, o baú é visitado. Buffer de colisores maior só empurrava o problema |
| **Brilho do escudo da ward alheia** | `PrivateArea.CheckAccess(pos)` com o `flash` padrão (`true`), chamado a cada varredura de 5 s | Passou a **`flash: false`** |

O `ZDOVars.s_owner` também foi confirmado no assembly publicizado — é
`"owner".GetStableHashCode()`, então as duas formas davam a mesma chave; a ordem foi invertida
para a constante vir primeiro e o literal ficar só como fallback.

Recompilado depois das correções: `dotnet build Deadheim.csproj -c Release` → **0 erros**.

### Fechamento dos riscos levantados na segunda revisão

| Risco | Situação |
|---|---|
| Busca de baús pode encher o buffer numa base densa | **Resolvido** — a varredura por colisores saiu; virou lista de peças, sem teto |
| Animal distante de todo jogador (dono é o servidor) | **Limitação aceita** — falha em silêncio longe; cobrir exigiria o lado servidor |
| Campo `owner` zerado passa o filtro | **Limitação aceita** — permissivo de propósito; o nome da chave está confirmado, o caso é o zero |
| Dois VIPs na mesma fogueira | **Não é defeito** — o `Rpc_AddFuelAmount` do jogo faz `Mathf.Clamp(0, m_maxFuel)`; o pior caso é combustível a mais |
| Acesso privado antes do release | **Pendente** — rodar `acesso-privado-runtime` cobrindo `AesirPull.cs` e o `Pets.cs` |

### O que continua sem teste no jogo

1. **`Piece.s_allPieces` só contém peças carregadas** — é o mesmo escopo do `OverlapSphere`, que
   também só enxerga o que está instanciado, então não é uma regressão. Mas se a busca voltar
   sempre vazia, o primeiro suspeito passa a ser o `Container` **não estar no mesmo GameObject
   da `Piece`** (o `GetComponentInParent`/`GetComponentInChildren` cobre o caso comum de filho,
   não um baú montado de forma exótica).
2. **Se o `Container` de fato acompanha a `Piece`** — `Container.Awake` faz
   `GetComponent<Piece>()` no próprio objeto, o que indica que sim, mas não foi verificado no jogo.
3. **A posse do `ZNetView`** da fogueira e do animal (ver abaixo).
4. **Acesso privado** — rodar a checagem (`acesso-privado-runtime`) antes do release. Os acessos
   novos aqui são `Piece.s_allPieces` / `Piece.s_ghostLayer` / `Piece.GetAllPiecesInRadius` e os
   campos do `MonsterAI`/`Fireplace` já listados adiante.

## Limitações declaradas (aceitas)

- **Animal distante de todo jogador:** em servidor dedicado, o `ZNetView` do bicho pertence ao
  servidor quando não há ninguém por perto, e aí **nenhum cliente executa a alimentação** — o
  `IsOwner()` barra. O item (b) funciona onde ele importa, que é o animal perto de um jogador
  (a fazenda de quem está na base), e falha **em silêncio** longe. Cobrir o caso distante exige
  o lado servidor, que é justamente a opção (B) descartada por falta de lista VIP confiável lá.
- **Dois VIPs na mesma base:** o segundo não puxa do baú do primeiro — `OwnerIsVip` só
  reconhece o próprio jogador, porque a lista VIP do cliente não cobre os outros. É o lado
  seguro, igual ao `/depositar`.
- **Dono do animal zerado passa o filtro.** Se o campo `owner` da ZDO estiver em 0 (bicho que
  ainda não gravou a chave), `IsOwnerLocal` devolve `true` e o animal pode ser alimentado com o
  baú de outro jogador VIP presente. É **permissivo de propósito**: recusar no zero deixaria o
  dono sem o recurso nos bichos que não têm a chave. O nome da chave está confirmado no
  assembly (`ZDOVars.s_owner`), então o caso "nome errado" não existe — o que existe é o zero.
- **Cliente modificado** pode puxar de um baú sem ser VIP. O dano fica no próprio baú e na
  própria ward.
- **Raid:** durante a janela de raid a fogueira não puxa de **nenhum** baú, nem do próprio —
  o `WardBridge.Governed` só diz "aqui manda o raid", sem separar baú próprio de alheio.
- **Baú de guilda ou grupo:** `Container.CheckAccess` devolve `false` para
  `PrivacySetting.Group` (é o comportamento do jogo, não escolha nossa), então esses baús
  nunca são usados.

## Pontos menores conhecidos

- O cache `NextTry` é por instância e **cresce sem limite** (uma entrada por fogueira/animal
  já tentado). É pequeno na prática; se virar problema, limpar por `Time` no `Tick`.
- **Se o dono do `ZNetView` da fogueira for outro jogador** (o cliente mais próximo), a
  fogueira do VIP não é abastecida, mesmo com ele ao lado — o `IsOwner()` barra. Vale medir no
  teste com dois clientes.
- **Dois VIPs na mesma fogueira: o pior caso é combustível a mais, não corrupção.** Se os dois
  chamarem `RPC_AddFuelAmount` no mesmo instante, o `Rpc_AddFuelAmount` do jogo faz
  `Mathf.Clamp(num + amount, 0, m_maxFuel)` antes de gravar, então o valor nunca passa do
  máximo. Não é defeito — é o mesmo comportamento de dois jogadores enchendo a fogueira à mão.
- A busca de baús é **compartilhada de propósito** com o item 13 (`FindWith` / `Take`).

## Nomes do jogo confirmados no assembly publicizado

Verificados em `D:/valheim-1.0.17-ref/valheim_Data/Managed/publicized_assemblies/assembly_valheim_publicized.dll`
(por decompilação com `ilspycmd`, não por leitura do briefing):

| Membro | Assinatura real | Observação |
|---|---|---|
| `MonsterAI.UpdateConsumeItem` | `public bool (Humanoid, float)` | chamado dentro do `UpdateAI`, não do `Update` |
| `MonsterAI.m_consumeItems` | `List<ItemDrop>` | `CanConsume(ItemData)` compara `m_shared.m_name` |
| `Fireplace.UpdateFireplace` | `public void ()` | `InvokeRepeating(..., 0f, 2f)` no `Awake` |
| `Fireplace.RPC_AddFuelAmount` | `public void (long, float)` | registrado como `nview.Register<float>` |
| `Fireplace.Interact` | `public bool (Humanoid, bool, bool)` | é ele quem chama `m_nview.ClaimOwnership()` |
| `Container.CheckAccess` | `public bool (long playerID)` | `PrivacySetting.Group` devolve **false** sempre |
| `Container.IsInUse` | `public bool ()` | |
| `Inventory.RemoveItem` | `public bool (ItemDrop.ItemData, int)` | |
| `Tameable` | **não tem `m_owner`** | o dono do animal é o campo `owner` da ZDO do `Character` |

## Decisões que você precisa confirmar

1. **`OnlyOwnerVip` responde "sim" só quando o criador é o jogador local.** A lista VIP do
   cliente não cobre outros jogadores, então baú de **outro** VIP cai no caso desconhecido e é
   recusado. Numa base compartilhada entre dois VIPs, o segundo não puxa do baú do primeiro.
   É o lado seguro, e é o mesmo comportamento do `/depositar`. Se isso incomodar, a saída é
   desligar `OnlyOwnerVip` (aí vale `CheckAccess` + ward, sem olhar o dono).
2. **Durante a janela de raid a fogueira não puxa de baú nenhum**, não só do alheio — porque o
   `WardBridge.Governed` (que o RaidSystem registra) só diz "aqui manda o raid", sem separar
   baú próprio de alheio. Se você quiser "puxa do próprio baú mesmo em raid", precisa de um
   hook mais fino que o WardBridge não oferece hoje.

## Compilação

`dotnet build Deadheim.csproj -c Release` → **0 erros**, 58 avisos (todos pré-existentes, do
`ThirdParty/ConfigSync.cs` em conflito com o `ConfigSync` do VipList).

## SEM TESTE NO JOGO — o que conferir primeiro

1. **`LayerMask` da busca de baús.** Usei `"piece", "Default", "static_solid", "Default_small",
   "item"`. Se o colisor do baú não estiver em nenhuma dessas camadas, `FindWith` volta `null`
   e **nada acontece, sem erro no log**. É o primeiro ponto a verificar.
2. **A chave `owner` da ZDO do animal.** Usei `"owner".GetStableHashCode()` com fallback para
   `ZDOVars.s_owner`. Se o nome real for outro, o `owner` lê 0 e o filtro **libera**
   (permissivo — não quebra, mas não protege).
3. **`InvokeRPC("RPC_AddFuelAmount", take)`** chegando com o `float` correto no dono da fogueira.
4. **Posse do `ZNetView` do animal.** Em servidor dedicado o bicho pode pertencer ao servidor, e
   aí nenhum cliente executa o postfix — o item (b) não dispara para animais distantes. Animais
   de uma base ativa devem estar com posse do cliente próximo.
5. **Duplicação entre dois VIPs** perto da mesma fogueira. O cache `NextTry` é por instância e
   **por cliente**, então não protege entre clientes; o `m_nview.IsOwner()` limita a um só
   executor, mas vale confirmar.
6. **Acessos privados** (`m_consumeItems`, `m_nview`, `m_fuelItem`, `m_maxFuel`, `m_infiniteFuel`,
   `m_canRefill`, `m_animator`, `m_consumeItemEffects`, `ZDOVars.s_fuel`) — todos compilam contra
   o publicized, mas rodar a **checagem de acesso privado** (`acesso-privado-runtime`) antes do
   release, como o item 36 manda.

## Próximo passo natural

O **item 13** (fabricar usando baús) deve chamar `AesirChests.FindWith` e `AesirChests.Take` em
vez de escrever a busca de novo — é o motivo de ela estar em classe separada.
