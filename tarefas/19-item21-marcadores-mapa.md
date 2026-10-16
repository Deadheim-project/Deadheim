> **Briefing para agente de desenvolvimento** (leia primeiro `00-LEIA-PRIMEIRO.md` desta pasta).
> Repositório: **Deadheim** (em D:\deadheim\). Release: **so depois da decisao do usuario**.
> Regras: responda em português; seja econômico (leia só o necessário, não imprima arquivos grandes, não crie subagentes); implemente SÓ este item; não faça commit, push, release nem deploy (o usuário decide); compile com `dotnet build -c Release` e informe o resultado; não sobrescreva alterações sem commit de outras sessões (confira `git status` antes); ao terminar, liste arquivos mudados e o que ficou sem teste no jogo.
> O texto abaixo é o PROJETO; o agente confirma nomes de métodos do jogo no assembly publicizado em D:alheim-ref antes de escrever Harmony.

## Item 21: marcadores extras no mapa — 3 opções concretas

O usuário não soube definir. Opções, da mais barata:

| Opção | O que é | Regra de PvP | Esforço |
|---|---|---|---|
| **A. Mais pinos pessoais** | O jogador comum tem um limite de pinos (vanilla não tem teto fixo, mas o servidor pode ter configurado um); o VIP ganha mais slots | Pino é só do jogador; nada compartilhado | 0,5 dia, só se existir um limite hoje (verificar `Minimap`; se não existe, a opção A é nula) |
| **B. Pinos com ícone cosmético** | Ícones extras (caveira, coroa, abóbora, bandeira) que só o VIP pode usar nos próprios pinos | Só ícone; não revela posição de ninguém | 1 dia (`Minimap.PinType` aceita ícones novos via sprite; sincronização zero, pino é local) |
| **C. Pino compartilhado com o grupo (Aesir)** | O VIP marca um local e **só o grupo dele** vê | **Vantagem de combate** (informação em tempo real sobre inimigo). **Não recomendo; a regra do projeto proíbe mapa de guilda.** | n/a |

**Recomendação:** B (cosmético puro, barato). A só se o limite existir. C descartada. Reaproveita o padrão do `/batizar` (marcador no mapa) já existente. **Decisão sua:** B, A ou descartar de vez.
