> **Briefing para agente de desenvolvimento** (leia primeiro `00-LEIA-PRIMEIRO.md` desta pasta).
> Repositório: **npc-valheim** (em D:\deadheim\). Release: **R1 NpcValheim**.
> Regras: responda em português; seja econômico (leia só o necessário, não imprima arquivos grandes, não crie subagentes); implemente SÓ este item; não faça commit, push, release nem deploy (o usuário decide); compile com `dotnet build -c Release` e informe o resultado; não sobrescreva alterações sem commit de outras sessões (confira `git status` antes); ao terminar, liste arquivos mudados e o que ficou sem teste no jogo.
> O texto abaixo é o PROJETO; o agente confirma nomes de métodos do jogo no assembly publicizado em D:alheim-ref antes de escrever Harmony.

## Aesir 23: mais anexos no correio

**Abordagem.** Mesma ideia do 22: VIP envia mais anexos por carta (sugestão: o limite de hoje +50%, definido depois de achar o valor). Só aumenta o que o **VIP envia**, nunca a caixa de quem recebe, e continua restrito a correio dentro das regras atuais (a regra do projeto proíbe "correio fora de casa").

**Passo 0.** Localizar onde o limite de anexos é imposto (`MailboxNpc`, `MailWire`, `MailDatabase`, UI). Não achei constante óbvia. `MaxMailPerPlayer=500` e `MaxHouseRecipients=100` são outros limites. `ItemSpawner.MaxStacksPerDelivery=32` é a entrega de item no mundo.

**Autoridade.** Servidor valida no recebimento da carta (tem `sender`). **Config** (`[Mail]`): `VipMaxAttachments`. **Arquivos:** os do passo 0. **Teste:** não-VIP no limite antigo; VIP acima; recebimento do destinatário não-VIP funciona (receber nunca é limitado). **Esforço:** 0,5 a 1 dia.
