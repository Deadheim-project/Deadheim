# Opcao "Deadheim" no menu do ESC

Dentro do jogo, o ESC ganha a opcao **Deadheim**, logo abaixo de Configuracoes. Ela abre uma
janela no visual do Valheim (painel de madeira, fonte e botoes do jogo) com duas abas:

- **Meus ajustes** (todo jogador): a config deste PC de todos os mods carregados, menos o que o
  servidor controla. Grava direto no `BepInEx/config` do jogador, na hora.
- **Servidor** (so admin): a config que o servidor esta usando, de todos os mods que o servidor
  tem, pedida a ele pela rede. Cada mudanca e aplicada e salva no cfg do servidor na hora. O que e
  sincronizado (ServerSync) chega a todos os jogadores conectados sem reiniciar nada.

Cobre qualquer mod do launcher, sem lista fixa: Deadheim (PvP, Wards, Montarias...), RaidSystem,
Hearthstone, NPCs, Velas, AdaptiveNet, Guilds, Groups, ServerCharacters, CLLC, AzuAntiCheat e os
opcionais. O que conta e o `ConfigFile` que o BepInEx ja tem aberto para cada mod.

## Quem ve o que

- "Sincronizado" = o ServerSync marca a opcao com um `SyncedConfigEntry` nas Tags (ou o Jotunn
  com `IsAdminOnly`). Essas opcoes nunca aparecem em "Meus ajustes": no cliente elas valem o que
  o servidor mandou, e a regra de PvP roda no cliente de quem leva o golpe.
- Opcao com `Browsable = false` nao aparece; com `ReadOnly = true` aparece travada.
- Opcao que so o servidor usa e nao e sincronizada (segredo, como o webhook do RaidSystem):
  o mod marca `Browsable = Application.isBatchMode`. No cliente ela some de "Meus ajustes" (e do
  F1); no servidor dedicado continua na aba Servidor.
- Admin e quem esta na `adminlist.txt` do servidor, a mesma lista que o ServerSync e os
  devcommands usam. A aba Servidor so aparece para admin, e o servidor confere de novo a cada
  pedido pela conexao de verdade (`ZRpc`), nao pelo id que o cliente escreve no pacote.

## Como funciona

| Arquivo | O que faz |
| --- | --- |
| `AjustesMenu.cs` | Copia o botao Configuracoes do menu do ESC, troca o texto e o clique. Com a janela aberta, o ESC fecha so a janela. |
| `AjustesJanela.cs` | A janela: lista de mods, opcoes agrupadas por secao, busca, botao Padrao, descricao completa ao passar o mouse. |
| `AjustesDados.cs` | Le e grava os `ConfigFile` do BepInEx. Valores em texto, no formato do .cfg (`TomlTypeConverter`). |
| `AjustesRede.cs` | RPCs `DH_Ajustes_ToServer` / `DH_Ajustes_ToClient`: lista de mods, opcoes de um mod (comprimidas) e troca de valor. |

Tipos de editor: liga/desliga vira caixa de marcar; enum e lista de valores aceitos viram `< valor >`;
o resto (numero, texto, tecla, atalho, cor) vira campo de texto, aplicado com Enter ou ao clicar fora.
Valor invalido e recusado com a mensagem embaixo.

O servidor registra cada mudanca no log: `Ajustes: <id> mudou <mod> [secao] chave: 'antes' -> 'depois'`.

Alguns mods so leem certas opcoes quando iniciam; essas passam a valer no proximo restart.

## Testes

- **Sem o jogo** (`Testing/AjustesSemJogo`): o `AjustesRede.cs` e o `AjustesDados.cs` de verdade,
  com o Harmony aplicando os patches, um servidor e clientes simulados trocando pacotes e o
  `ConfigFile` do BepInEx gravando em disco. 40 checagens: jogador comum negado (inclusive com
  id de admin forjado no pacote), admin lista/le/muda, valor invalido, faixa, liga/desliga,
  lista, opcao oculta, mod com 400 opcoes, host de mundo local, e o que entra em "Meus ajustes".

      dotnet build Testing\AjustesSemJogo -c Release
      Testing\AjustesSemJogo\bin\Release\net48\AjustesSemJogo.exe

- **No jogo** (passo `ajustes` do `Testing/run-pvp-test.ps1`): abre o ESC, confere o botao
  (texto, sem sobrepor os outros, dentro da tela), abre a janela, edita pelos controles dela e
  testa a aba Servidor contra o servidor dedicado. Tira fotos de cada tela em `<Root>\fotos`.

      Testing\run-pvp-test.ps1 -Solo -Steps ajustes           como jogador comum
      Testing\run-pvp-test.ps1 -Solo -Steps ajustes -Admin    como admin
