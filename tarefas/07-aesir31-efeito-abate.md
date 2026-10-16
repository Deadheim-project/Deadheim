> **Briefing para agente de desenvolvimento** (leia primeiro `00-LEIA-PRIMEIRO.md` desta pasta).
> Repositório: **Deadheim** (em D:\deadheim\). Release: **R4 Deadheim (Halloween)**.
> Regras: responda em português; seja econômico (leia só o necessário, não imprima arquivos grandes, não crie subagentes); implemente SÓ este item; não faça commit, push, release nem deploy (o usuário decide); compile com `dotnet build -c Release` e informe o resultado; não sobrescreva alterações sem commit de outras sessões (confira `git status` antes); ao terminar, liste arquivos mudados e o que ficou sem teste no jogo.
> O texto abaixo é o PROJETO; o agente confirma nomes de métodos do jogo no assembly publicizado em D:alheim-ref antes de escrever Harmony.

## Aesir 31: efeito de abate

**Abordagem.** Quando um VIP (com o cosmético ativo) mata outro jogador em PvP, toca um efeito visual no corpo da vítima. Opções: `padrao` (raios), `almas` (Halloween). Pura visual, sem dano, sem recompensa extra, sem som alto.

**Ganchos.** O Deadheim já detecta a morte PvP: `PvpPatches.DeathPatch` e `PvpRules.ClassifyDeath` (distinguem PvP, PvE e pena de PK). Postfix ali no **servidor** quando a classificação é morte PvP legítima: ler o cosmético ativo do **assassino** (`CosmeticosApi.Has(killerId, "abate:<id>")` + escolha dele), e mandar um RPC `DH_AbateFx(posição, efeitoId)` aos clientes a até 60 m. O cliente instancia partículas locais (`ParticleSystem` próprio ou prefab visual do jogo **sem `Aoe`, `Projectile` ou luz de alto custo**).

**Onde a checagem ocorre.** Servidor: posse do cosmético e validade da morte. O cliente só desenha. **Regras PvP:** só em morte PvP que conta; nunca em morte PvE, em arena (`IsArena`) ou em morte por pena de PK; não mostrar o nome do assassino acima do normal (nada de "troféu" que revele posição); duração 3 s; sem bloqueio de visão (partículas pequenas, sem tela cheia).

**Config** (`[AbateFx]`): `Enabled`, `Radius=60`, `Seconds=3`. **Escolha:** `/abate <id>|off` (cliente → servidor valida posse). **Arquivos:** novo `AbateFx.cs`; registrar chaves `abate:padrao`, `abate:almas` no `Cosmeticos`; hook em `Pvp/PvpPatches.cs` (só uma chamada). **Riscos:** o efeito pode atrapalhar a visão do oponente; prefabs vanilla têm `Aoe`. Usar partículas próprias. **Teste:** VIP mata em PvP (aparece nos 2 clientes), mata mob (não), morre em arena (não), não-VIP (não). **Esforço:** 1,5 dia.
