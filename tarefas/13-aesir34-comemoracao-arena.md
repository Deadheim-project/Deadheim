> **Briefing para agente de desenvolvimento** (leia primeiro `00-LEIA-PRIMEIRO.md` desta pasta).
> Repositório: **npc-valheim** (em D:\deadheim\). Release: **R6 NpcValheim**.
> Regras: responda em português; seja econômico (leia só o necessário, não imprima arquivos grandes, não crie subagentes); implemente SÓ este item; não faça commit, push, release nem deploy (o usuário decide); compile com `dotnet build -c Release` e informe o resultado; não sobrescreva alterações sem commit de outras sessões (confira `git status` antes); ao terminar, liste arquivos mudados e o que ficou sem teste no jogo.
> O texto abaixo é o PROJETO; o agente confirma nomes de métodos do jogo no assembly publicizado em D:alheim-ref antes de escrever Harmony.

## Aesir 34: comemoração na arena

**Abordagem.** Quando o VIP vence uma partida de arena, toca uma comemoração só visual (fogos/partículas, texto "Fulano venceu", pose de emote) sobre ele, visível aos presentes na arena. Sem premiação, sem efeito de dano.

**Ganchos.** `ArenaClient` já recebe `ArenaResult` (`ArenaClient.cs:158`). No servidor da arena (`ArenaServer.cs`), ao fechar a partida, o vencedor e a lista de presentes são conhecidos. O servidor envia um RPC `Arena_Celebrate(playerId)` aos participantes se o vencedor for VIP. O cliente instancia partículas (prefab do jogo já existente, por exemplo `fx_Fader_Fireball_...` ou fogos do evento; escolher um inofensivo e **sem `Aoe`/`Projectile`**, só `ParticleSystem`) e o emote por `Emote`.

**Regras de PvP.** Só depois de a partida terminar (fase `Ended`), nunca durante. Prefab sem colisor e sem som alto. Um por partida, duração de 6 s. Sem relação com quem venceu em PvP aberto.

**Config** (`[Arena]`): `VipCelebration=true`, `CelebrationSeconds=6`. **Arquivos:** `Arena/ArenaServer.cs`, `Arena/ArenaClient.cs`, `Arena/ArenaWire.cs` (novo opcode), efeito em arquivo novo `Arena/ArenaCelebration.cs`. **Riscos:** nome do prefab de efeito (verificar `ZNetScene`); efeito pesado com muita gente. **Teste:** vitória de VIP e de não-VIP; dois clientes na arena veem; fora da arena nada. **Esforço:** 1 dia.
