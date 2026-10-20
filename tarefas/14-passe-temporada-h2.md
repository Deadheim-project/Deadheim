> **Briefing para agente de desenvolvimento** (leia primeiro `00-LEIA-PRIMEIRO.md` desta pasta).
> Repositório: **npc-valheim + Deadheim** (em D:\deadheim\). Release: **R7**.
> Regras: responda em português; seja econômico (leia só o necessário, não imprima arquivos grandes, não crie subagentes); implemente SÓ este item; não faça commit, push, release nem deploy (o usuário decide); compile com `dotnet build -c Release` e informe o resultado; não sobrescreva alterações sem commit de outras sessões (confira `git status` antes); ao terminar, liste arquivos mudados e o que ficou sem teste no jogo.
> O texto abaixo é o PROJETO; o agente confirma nomes de métodos do jogo no assembly publicizado em D:alheim-ref antes de escrever Harmony.

## Passe de temporada (H2): NPC Arauto da Temporada

**Escopo aprovado:** 10 níveis por desafios semanais, 3 gratuitos, 500 DC retroativo, só cosmético, **sem abate PvP contando ponto**, temporada ~4 semanas desde a abertura.

**Abordagem.** Um NPC "Arauto da Temporada" no NpcValheim (molde do `QuestGiverNpc`, que já tem quests e progresso: `QuestDatabase`, `QuestProgressRules`, `QuestStore`). O passe é uma trilha de 10 níveis, cada nível uma recompensa cosmética (títulos, chamas, lápides, pets de temporada, velas). Pontos vêm de **desafios semanais** de não-PvP: coletar, construir, matar chefes/mobs de PvE, explorar, pescar, usar o NPC. A `FichaTemporada` (500 DC) destrava a trilha paga **retroativamente** (as recompensas já ganhas em pontos são entregues).

**Ganchos.** Reaproveitar `Npc/QuestGiverNpc.cs` e `Persistence/QuestProgressRules.cs` para contar desafios; o servidor é autoridade (progresso no `QuestDatabase`). A contagem **exclui** abates de jogador: nas regras de progresso, ignorar mortes cuja vítima é `Player` (`QuestKillTracker` já registra por `Character.m_lastHit`: filtrar `victim is Player`). Mortes na arena e bounty não contam.

**Dados.** Novo arquivo `Persistence/SeasonDatabase.cs` (LiteDb como o resto): `playerId`, `seasonId`, `points`, `claimed[]`, `paid`. Datas em config (`SeasonStart`, `SeasonEnd`) e `Deadheim.RelogioServidor`. `WeekOf` já é compartilhado (visto em A1-A4).

**Recompensas.** São chaves do registro `Cosmeticos` (`ServerGrant`); nada de item de combate, nada de Deadcoins grátis em volume (3 níveis grátis dão cosmético simples).

**PvP.** Nenhum desafio de PvP; nenhuma recompensa dá vantagem. Sem "ranking de temporada" público (evita farm).

**Config** (`[Season]`): `Enabled`, `SeasonId`, `Start`, `End`, `PointsPerLevel`, `FreeLevels=3`, `Rewards` (nível=chave). **Arquivos:** NpcValheim `Npc/SeasonHeraldNpc.cs`, `UI/SeasonWindow.cs`, `Persistence/SeasonDatabase.cs`; Deadheim: só as chaves. **Riscos:** o maior item novo de UI; balancear pontos. **Teste:** concluir desafio, pontos sobem; abate PvP não conta; comprar a ficha libera retroativo; virar a semana reinicia desafios; fim da temporada congela. **Esforço:** 4 a 5 dias. Entregar depois do Halloween, em novembro.

---

## Resultado da implementação (2026-10-19, sessão npc-valheim, sem commit)

**Feito (NpcValheim, compila: 0 avisos, 0 erros):** Arauto da Temporada (`NpcValheim_Season`), 10 níveis (3 grátis), passe de 500 DC cobrado no próprio Arauto (retroativo), 3 desafios semanais sorteados de forma determinística (Kill/Gather/Talk, nada de PvP), recompensas por chave de `CosmeticosApi.Grant` (por reflexão), progresso em `season.db`.
**Arquivos novos:** `Npc/Season.cs`, `Npc/SeasonHeraldNpc.cs`, `Persistence/SeasonDatabase.cs`, `Integration/DeadheimCosmeticsApi.cs`, `UI/SeasonView.cs`.
**Arquivos alterados:** `Plugin.cs` (config `[Season]`, abrir/fechar `season.db`), `Npc/QuestProgressNetwork.cs` (2 chamadas a `Season.OnEvent`), `Npc/ServiceNpcAuthority.cs`, `Prefabs/NpcPrefabFactory.cs`, `UI/NpcWindow.cs`. Deadheim: nenhum código (só este rodapé).
**Não testado no jogo:** tudo (concluir desafio, abate PvP não contar, compra retroativa, virada de semana, fim da temporada, 2 clientes).
**Pendências:** (1) `Enabled=false` por padrão; ligar e conferir `Start`/`End`. (2) Chaves `vela:`, `chama:`, `lapide:`, `abate:` e `pet:temporada1/2` são só registradas; o efeito depende dos itens 29/31/33/vela/pet. Os dois pets são placeholders. (3) O `Titulos` do Deadheim só exibe `titulo:fundador`; `titulo:temporada1`/`campeao_t1` ainda não aparecem no chat. (4) Falta o ícone `Assets/Icons/season.png` (usa o da caixa postal). (5) Desafio Explore ficou de fora; Gather é farmável soltando e pegando o item (mesmo risco dos quests). (6) Para compilar neste worktree criei a junção `.claude/worktrees/Deadheim` -> `D:\deadheim\Deadheim` (o csproj usa `..\..\Deadheim`); ela não entra no git.
