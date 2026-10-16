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
