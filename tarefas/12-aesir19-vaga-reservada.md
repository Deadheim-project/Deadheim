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

---

## Resultado (19/10/2026) — implementado, compila, SEM teste no jogo

**Descoberta:** o limite do jogo não é campo: `ZNet.ServerPlayerLimit = 10` é const inlinada em `ZNet.RPC_PeerInfo` (`if (GetNrOfPlayers() >= 10) Error 9`). Não existe `m_serverPlayerLimit`. Logo o servidor hoje aceita 10; o limite do DatHost/cfg não muda isso a menos que outro mod mude.

**Feito (mecanismo A):** `VipSlots.cs` novo. Patch em `ZNet.RPC_PeerInfo`: Prefix guarda o `GetHostName()` do peer (só no servidor), Transpiler troca a chamada `GetNrOfPlayers()` do teste de lotação por `VipSlots.Ocupacao(ZNet)`, que desconta `ReservedSlots` apenas se o peer está na VipList (`VipListApi.IsVip(host)`); Finalizer limpa. Não-VIP continua com exatamente o vanilla (10). Config `[VipSlots] Enabled=true, ReservedSlots=3` (sincronizada). Se o transpiler não achar exatamente 1 chamada, loga aviso.

**Arquivos:** `VipSlots.cs` (novo), `Plugin.cs` (+1 linha `VipSlotsConfig.Bind`), `Deadheim.csproj` (+1 Compile). Build Release: 0 erros.

**Sem teste no jogo:** servidor com 10 não-VIP, VIP entra (11º, 12º, 13º), 14º VIP e 11º não-VIP recusados (erro "cheio"); VIP sai e o limite volta; conferir o log sem o aviso do transpiler; confirmar que o servidor (DatHost) não tem outro limite por fora; custo com 13 jogadores. DirectJoinFlow é só cliente (não decide lotação), não foi alterado.

**Correção (19/10):** o limite do servidor é definido pelo mod MaxPlayerCount do Azumatt (`Azumatt.MaxPlayerCount.cfg` no servidor; ele troca o literal 10 do `RPC_PeerInfo`). O `VipSlots` não define limite: mantém a chamada `GetNrOfPlayers` e só desconta as vagas reservadas do VIP sobre o resultado, compatível com o transpiler do Azumatt em qualquer ordem. Para subir o limite para 40, ajustar `MaxPlayers` nesse cfg (não no Deadheim).

**Correção 2 (19/10):** sem mod do Azumatt (removido de `deadheim-installer.json`). O limite agora é do próprio Deadheim, em `VipSlots.cs`: `[VipSlots] PlayerLimit=40` troca o literal 10 em `ZNet.RPC_PeerInfo` (transpiler), o máximo anunciado ao Steam (`SteamGameServer.SetMaxPlayerCount`, alvo por reflexão) e os `MaxPlayers`/`MaxPlayerCount` 10 do PlayFab (`ZPlayFabMatchmaking.CreateLobby`/`CreateAndJoinNetwork`). VIP soma `ReservedSlots=3` por cima. Build OK, sem teste no jogo. Garantir que o MaxPlayerCount saia do servidor e do launcher (`manifest.sample.json` o lista; o `manifest.json` real não) para não haver dois patches no mesmo literal.

**Correção 3 (19/10):** removido do Deadheim todo o código que definia o limite do servidor (`PlayerLimit`, Steam, PlayFab, literal 10). O `VipSlots.cs` só desconta as vagas VIP (`ReservedSlots=3`) sobre o limite vigente. Conferido por FTP: o servidor NÃO tem MaxPlayerCount em `BepInEx/plugins`; o limite continua o do jogo (10) até alguém definir outro.

**Correção 4 (19/10, valendo):** código do limite RESTAURADO em `VipSlots.cs` (`PlayerLimit=40`, Steam, PlayFab, literal do `RPC_PeerInfo`, + `ReservedSlots=3` VIP). O MaxPlayerCount do Azumatt saiu do pack: removido de `deadheim-installer.json` e dos dois `manifest.sample.json` do launcher (principal e valheim10); o servidor já não o tinha. Build OK, sem teste no jogo.
