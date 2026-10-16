> **Briefing para agente de desenvolvimento** (leia primeiro `00-LEIA-PRIMEIRO.md` desta pasta).
> Repositório: **Deadheim** (em D:\deadheim\). Release: **R5 Deadheim**.
> Regras: responda em português; seja econômico (leia só o necessário, não imprima arquivos grandes, não crie subagentes); implemente SÓ este item; não faça commit, push, release nem deploy (o usuário decide); compile com `dotnet build -c Release` e informe o resultado; não sobrescreva alterações sem commit de outras sessões (confira `git status` antes); ao terminar, liste arquivos mudados e o que ficou sem teste no jogo.
> O texto abaixo é o PROJETO; o agente confirma nomes de métodos do jogo no assembly publicizado em D:alheim-ref antes de escrever Harmony.

## Aesir 19: vaga reservada com o servidor cheio

**Abordagem.** Quando o servidor está cheio, o VIP ainda entra. Há dois mecanismos possíveis:

| | A. Vaga reservada (VIP sempre entra, até N a mais que o limite) | B. Expulsar o último não-VIP |
|---|---|---|
| Como | Aceitar a conexão acima de `ServerMaxPlayers` até `+VipReserved` | Ao chegar o VIP com o servidor cheio, desconectar um não-VIP (o último a entrar ou o mais ocioso) |
| Justo | Sim: ninguém é expulso | Não: pune quem não paga |
| Recomendado | **Sim** | Não |

Recomendo A, com `VipReservedSlots=3` (config).

**Ganchos.** `ZNet.RPC_PeerInfo` / `ZNet.OnNewConnection`, onde o servidor recusa com `ConnectionStatus.ErrorFull` quando `GetNrOfPlayers() >= m_serverPlayerLimit`. Prefixo em `ZNet.RPC_PeerInfo` (ou no ponto exato que compara com `m_serverPlayerLimit`; ver o método no assembly) que, para um peer VIP, sobe o limite em `VipReservedSlots` para aquela decisão. **O VIP precisa ser identificado já na conexão**: o ID de plataforma está no `ISocket.GetHostName()` (`IsVip(host)`, usado em `Pets.IsVipPeer`).

**Autoridade.** Totalmente do servidor, na conexão. **PvP.** Nenhum. **Config** (`[VipSlots]`): `Enabled`, `ReservedSlots=3`. **Arquivos:** novo `VipSlots.cs` (Deadheim; roda só no servidor, `ZNet.instance.IsServer()`). A lista VIP é lida pelo `VipList`; no ponto de conexão já há um caminho existente (`DirectJoinFlow.cs`): ver se ele faz a decisão de lotação antes de criar outro.

**Riscos.** O DatHost pode impor o limite de slots **por fora do jogo** (no painel); nesse caso o mod não adianta. Verificar `ServerPlayerLimit` no cfg do servidor. O jogo fica com `limite + 3` jogadores: testar custo. **Teste:** servidor com limite baixo (2) de teste, 2 não-VIP conectados, VIP entra; um 4º não-VIP não entra; o VIP some e o limite volta. **Esforço:** 1 dia.
