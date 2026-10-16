> **Briefing para agente de desenvolvimento** (leia primeiro `00-LEIA-PRIMEIRO.md` desta pasta).
> Repositório: **Deadheim + velas** (em D:\deadheim\). Release: **R4/R4b (depende de 06,07,08)**.
> Regras: responda em português; seja econômico (leia só o necessário, não imprima arquivos grandes, não crie subagentes); implemente SÓ este item; não faça commit, push, release nem deploy (o usuário decide); compile com `dotnet build -c Release` e informe o resultado; não sobrescreva alterações sem commit de outras sessões (confira `git status` antes); ao terminar, liste arquivos mudados e o que ficou sem teste no jogo.
> O texto abaixo é o PROJETO; o agente confirma nomes de métodos do jogo no assembly publicizado em D:alheim-ref antes de escrever Harmony.

## Pacote de Halloween (H1)

**Conteúdo aprovado:** título Assombrado, vela Abóbora, chama roxa, lápide de caveira, efeito de abate almas, comprados pela `FichaHalloween` (400 DC, 24/10 a 07/11, só cosmético).

**Abordagem.** É montagem, não código novo, **depois** de 29, 31, 33, vela e título:
1. Registrar chaves: `titulo:assombrado`, `chama:roxa`, `lapide:caveira`, `abate:almas`, `vela:abobora` em `Cosmeticos` (já aceita chaves livres).
2. `FichaParaChave` hoje mapeia **uma** chave por ficha. A `FichaHalloween` concede **cinco**: mudar o dicionário para `string[]` (ou nova função `ConcederPacote`) e o `ServerGrant` para cada.
3. Velas lê `vela:abobora` pelo registro (`Velas.Integration` com `CosmeticosApi.LocalHas`), com cor e emissão laranja.
4. Título: já existe a API `CosmeticosApi` com `titulo:fundador`; somar `titulo:assombrado` (texto e cor).

**Janela de venda e regras.** `from=2026-10-24;to=2026-11-07` em `DeadcoinShop.DefaultItems` controla a venda; o cosmético **fica permanente** (a ficha é comprada na janela, o item é do jogador).

**Risco de prazo:** ver a tabela de ordem. Se 29/31/33 não fecharem até 23/10, mover o `from=` ou entregar um pacote parcial com aviso. **Decisão sua.** **Teste:** resgatar a ficha, cada chave aparece, cada cosmético funciona para o dono e é visto por outro cliente; ficha usada duas vezes não duplica. **Esforço:** 0,5 dia de montagem.
