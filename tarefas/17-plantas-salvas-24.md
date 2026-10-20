> **Briefing para agente de desenvolvimento** (leia primeiro `00-LEIA-PRIMEIRO.md` desta pasta).
> Repositório: **Deadheim** (em D:\deadheim\). Release: **release proprio**.
> Regras: responda em português; seja econômico (leia só o necessário, não imprima arquivos grandes, não crie subagentes); implemente SÓ este item; não faça commit, push, release nem deploy (o usuário decide); compile com `dotnet build -c Release` e informe o resultado; não sobrescreva alterações sem commit de outras sessões (confira `git status` antes); ao terminar, liste arquivos mudados e o que ficou sem teste no jogo.
> O texto abaixo é o PROJETO; o agente confirma nomes de métodos do jogo no assembly publicizado em D:alheim-ref antes de escrever Harmony.

## Plantas salvas (24)

**Abordagem.** O jogador salva uma construção (até ~300 peças) e a recoloca depois. **Cada peça passa por `Player.PlacePiece`/`TryPlacePiece`** para herdar as travas já existentes de ward, raid e combate; sem wards/portais/tokens; fora de combate. PlanBuild **não** pode entrar no launcher.

**Ganchos.** Captura: varrer peças (`Piece.GetAllPiecesInRadius`) numa área escolhida e gravar `prefab`, posição relativa, rotação, `Piece.m_creator`. Colocação: `Player.PlacePiece(Piece piece, Vector3 pos, Quaternion rot, bool doAttack)` ou `Player.TryPlacePiece`; consumir os materiais do inventário/baús (pode somar com o item 13 mais tarde).

**Autoridade.** O cliente decide a colocação (vanilla). Para as travas valerem: `PrivateArea.CheckAccess`, `Util.IsRaidEnabledHere` e checagem de combate (`PvpState`) **antes de cada peça**, no cliente **e** reavaliadas pelo servidor num RPC `DH_Planta_Place` que devolve "ok" por lote; peças sem OK são descartadas. Cliente modificado ainda pode instanciar peça solta (já acontece hoje no jogo vanilla): o objetivo é não abrir **novo** caminho.

**Limites.** 300 peças por planta, cooldown entre colocações de 10 s, 5 plantas salvas por VIP, uma colocação por vez, peça a peça com fila (para não travar o servidor), dentro do alcance de construção do jogador (se a planta é grande, o jogador anda), e **só prefabs da lista permitida** (sem `guard_stone`, portal, `Container` com conteúdo, `ItemStand` com item).

**PvP/raid.** Proibido em zona de raid durante a janela e em combate; respeita ward alheia; consome material (sem construção grátis); nada de "planta de torre" instantânea: fila de ~2 peças/segundo.

**Armazenamento.** Arquivo por jogador no servidor (`plantas-<mundo>.json`) ou no cliente? Recomendo **no servidor** (VIP com PC novo mantém) com tamanho máximo.

**Config** (`[Blueprints]`): `Enabled`, `MaxPieces=300`, `MaxSaved=5`, `PlaceIntervalSeconds`, `PrefabAllow`. **Arquivos:** novo `Plantas.cs`, `PlantasUi.cs`. **Riscos:** o maior item de código do conjunto; desempenho (muitas `ZDO`); colisão de peças; duplicar o que o PlanBuild faz (usuários já não o têm). **Teste:** salvar uma casa de 80 peças, colocar fora de ward alheia, dentro de ward alheia (bloqueia), durante combate (bloqueia), sem material (bloqueia), VIP expirado (bloqueia nova colocação). **Esforço:** 5 a 6 dias. Quase pelo fim da fila.

---
## Resultado (2026-10-19, sessao youthful-feistel) — implementado, compila (0 erros), SEM teste no jogo
- Arquivos: `Plantas.cs` (novo), `PlantasUi.cs` (novo), `Deadheim.csproj` (2 Compile + referencia UnityEngine.IMGUIModule), `Plugin.cs` (1 linha: `Plantas.Bind(Config);` apos `Lugares.Bind`).
- Janela IMGUI com `/planta` (e `/planta cancelar`). So VIP (sem ficha). So pecas criadas pelo proprio jogador. Armazenamento no servidor em `config/Deadheim/plantas-<mundo>.txt` (texto, nao JSON).
- Achado: `Player.PlacePiece` NAO tem trava de ward/material/combate. As travas sao feitas antes de cada chamada: ward (`PrivateArea.CheckAccess` no cliente; `WardCore.IsPermittedIn` no servidor), raid (`WardBridge.Governed`), NoBuild, bioma, estacao, combate (`PvpState`), material (`HaveRequirements`/`ConsumeResources`) e OK do servidor (RPC `DH_Planta_ToServer`: begin/check/end; o servidor recalcula posicao da peca a partir da planta salva).
- Sem teste no jogo: tudo (captura, fila, RPC com 2 clientes, janela e bloqueio de input/cursor, VIP expirado, ward alheio, raid, combate, sem material, bancada, desempenho com 300 pecas). Risco conhecido: o servidor nao ve o combate (so o cliente) e nao confere o criador na captura.
- PlanBuild em `deadheim-installer.json:44` nao chega ao jogador (tudo vem do manifest do launcher): nada a fazer.
