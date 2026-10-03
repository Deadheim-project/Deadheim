# Resultado: opcao Deadheim do menu do ESC no Valheim real

Rodado em 2026-09-30 no PC com o Valheim 1.0.16 (BepInEx 5.4.23.5, Unity 6000.0.75), servidor
dedicado local + 1 cliente (`run-pvp-test.ps1 -Solo -Steps ajustes`), tela do cliente 960x540.
Main final: `2f0d0af`.

## Resumo

| Teste | Resultado |
| --- | --- |
| AjustesSemJogo | TUDO OK (39 checagens) |
| Jogo, jogador comum | DONE pass=36 fail=0 |
| Jogo, admin (`-Admin`) | DONE pass=45 fail=0 (depois da correcao do `2f0d0af`; antes: 3 FAIL, ver abaixo) |

## O que mudou no codigo

- `766625a` Ajustes compila contra o Valheim real e o teste sem jogo roda fora da Unity
  - Erro de compilacao (unico): `Ajustes/AjustesRede.cs(281,73): error CS0104: "CompressionLevel" e uma
    referencia ambigua entre "System.IO.Compression.CompressionLevel" e "UnityEngine.CompressionLevel"`.
    Corrigido com o nome completo `System.IO.Compression.CompressionLevel.Optimal`.
    A API do Menu (`Menu.instance`, `m_menuDialog`, Show/Hide), o `CreateToggle` do `Shared/Vanilla.cs`
    e o `Testing/PvpTestDriver/Ajustes.cs` compilaram sem mudanca.
  - `AjustesSemJogo` compilava mas quebrava ao rodar: `SecurityException: Os metodos ECall devem ser
    empacotados em um modulo do sistema` em `UnityEngine.Object..cctor()`, disparado pelo
    `FieldInfo.SetValue` do `m_CachedPtr` em `Program.AddPlugin`. Com o UnityEngine real (nao stub) o
    construtor estatico chama o motor. Tirei essa gravacao: o `AjustesDados` so usa `?.` nos plugins,
    nunca o `==` da Unity. Depois disso: 39/39.
- `2f0d0af` Admin so pela adminlist.txt vanilla; servidor reenvia a lista depois do login
  - Pedido do dono: tirar o `AdminList` do cfg do Deadheim e usar so a lista vanilla. Removidos
    `Plugin.AdminList`, `Plugin.IsAdmin` e `Plugin.steamId` (nenhum codigo lia o IsAdmin; toda checagem
    ja era `Admin.LocalPlayerIsAdmin()` / `ZNet.IsAdmin`).
  - **Bug achado no teste admin**: no cliente, `ZNet.GetAdminList()` vinha vazia (`[]`), entao
    `LocalPlayerIsAdmin()` = false, a aba Servidor nao aparecia, e o admin tambem nao passaria por
    ward/portal/teleporte no cliente. O servidor reconhecia o admin (deixou mudar a config).
    Causa: o Valheim manda o RPC `AdminList` uma vez so, no `RPC_PeerInfo` do servidor; o ServerSync
    (`SendConfigsAfterLogin`) segura o `PeerInfo` do servidor num BufferingSocket ate mandar as configs,
    mas deixa o `AdminList` passar direto. O `AdminList` chega ao cliente antes do `PeerInfo`, antes de
    o cliente registrar o RPC, e se perde (confirmado: com um postfix no `ZNet.RPC_AdminList` do
    cliente, ele nunca era chamado).
    Correcao em `Patches.cs`: postfix no `ZNet.RPC_CharacterID` (servidor) chama `SendAdminList()`.
    Depois: `RPC_AdminList recebido: [76561198053330247, Steam_76561198053330247]`, `admin=True`, 45/45.
  - `Testing/PvpTestDriver/Ajustes.cs`: loga a adminlist que o cliente recebeu (diagnostico).

Atencao para o servidor de verdade: os IDs que estavam no `AdminList` do cfg
(76561198053330247 76561197961128381 76561198111650012 76561197993642177 76561198993982965) precisam
estar na `adminlist.txt` do servidor. Nao mexi no servidor.

## AjustesSemJogo

```
ok    patch no ZRpc.HandlePackage (prefix + finalizer)
ok    RPCs registrados no Game.Start
ok    jogador comum: servidor nega a lista  -- So admins do servidor (adminlist.txt) podem mexer na config do servidor.
ok    jogador comum: servidor registra a tentativa
ok    id de admin forjado no pacote nao passa  -- valor=100
ok    conexao atual volta a null depois do pacote
ok    chamada sem conexao num dedicado e negada
ok    admin: recebe a lista de mods  -- Deadheim(7), RaidSystem(2), CreatureLevelAndLootControl(400)
ok    ordem: Deadheim, proprios, resto
ok    nome amigavel tira o prefixo do GUID
ok    contagem ignora Browsable=false  -- 7
ok    mod so com opcao oculta fica fora
ok    admin: recebe as opcoes do Deadheim  -- Tecla, Local pelo ServerSync, SoAdmin, KillFeed, StartIslandMode, CartographyTableAmount, WardRadius
ok    secoes em ordem alfabetica
ok    CartographyTableAmount: texto, sincronizado, 100
ok    WardRadius: faixa 10 a 500
ok    KillFeed: liga/desliga
ok    StartIslandMode: lista com 3 opcoes
ok    Tecla: opcao local marcada como 'so no servidor'
ok    Segredo (Browsable=false) nao vai
ok    admin muda CartographyTableAmount -> 123  -- True|Detalhes.Deadheim|Server config|CartographyTableAmount|123|
ok    SettingChanged disparou no servidor (e o que o ServerSync escuta)
ok    cfg do servidor salvo em disco
ok    servidor registra quem mudou o que
ok    valor invalido e recusado e nada muda  -- Valor invalido para numero inteiro.
ok    faixa: 99999 vira 500 e a resposta traz o valor real
ok    liga/desliga: KillFeed -> false
ok    lista: StartIslandMode -> Radius
ok    opcao oculta nao aceita mudanca
ok    opcao inexistente: erro claro
ok    mod inexistente: erro claro, sem excecao
ok    mod com 400 opcoes chega inteiro (comprimido)
ok    descricao longa e cortada em 500  -- 500
ok    host de mundo nao dedicado (chamada local) e admin
ok    Meus ajustes: so mods com opcao local  -- Detalhes.Deadheim(2), Detalhes.RaidSystem(1)
ok    Meus ajustes do Deadheim: so as locais (IsAdminOnly fica de fora)  -- Tecla, Local pelo ServerSync
ok    Meus ajustes recusa opcao sincronizada  -- Essa opcao e do servidor.
ok    Meus ajustes grava opcao local
ok    nenhum erro no log

log do servidor:
  WARN [Deadheim] Ajustes: 76561198000000099 pediu 'mods' sem ser admin.
  WARN [Deadheim] Ajustes: 76561198000000099 pediu 'definir' sem ser admin.
  WARN [Deadheim] Ajustes: host pediu 'mods' sem ser admin.
  INFO [Deadheim] Ajustes: 76561198000000001 mudou Detalhes.Deadheim [Server config] CartographyTableAmount: '100' -> '123'
  WARN [Deadheim] Ajustes: 76561198000000001 tentou mudar Detalhes.Deadheim [Server config] CartographyTableAmount para 'abc': Valor invalido para numero inteiro.
  INFO [Deadheim] Ajustes: 76561198000000001 mudou Detalhes.Deadheim [Server config] WardRadius: '150' -> '500'
  INFO [Deadheim] Ajustes: 76561198000000001 mudou Detalhes.Deadheim [PvP - Geral] KillFeed: 'true' -> 'false'
  INFO [Deadheim] Ajustes: 76561198000000001 mudou Detalhes.Deadheim [PvP - Zonas] StartIslandMode: 'Island' -> 'Radius'
  WARN [Deadheim] Ajustes: 76561198000000001 tentou mudar Detalhes.Deadheim [Interno] Segredo para 'y': Essa opcao e somente leitura.
  WARN [Deadheim] Ajustes: 76561198000000001 tentou mudar Detalhes.Deadheim [Nao] Existe para '1': Opcao nao encontrada.
  WARN [Deadheim] Ajustes: 76561198000000001 tentou mudar Mod.Que.Nao.Existe [a] b para '1': Opcao nao encontrada.

TUDO OK (39 checagens)
```

## [PVPTEST] modo jogador (rodada final, com 2f0d0af)

```
[PVPTEST] driver ativo: papel=S sync=C:\Users\Werner\AppData\Local\Temp\deadheim-pvptest\sync save=C:\Users\Werner\AppData\Local\Temp\deadheim-pvptest\chars-S
[PVPTEST] criando personagem Solo
[PVPTEST] RPC_AdminList recebido: []
[PVPTEST] spawn ok (solo)
[PVPTEST] === setup
[PVPTEST] PASS setup/modulo-ativo 
[PVPTEST] PASS setup/templo templo=(4.62, 66.22, 0.66)
[PVPTEST] posicoes: templo=(4.62, 0.00, 0.66) seguro=(24.62, 0.00, 0.66) aberto=(54.62, 0.00, 0.66) wardA=(81.12, 0.00, 15.88) wardB=(46.19, 0.00, 28.44) castelo=(-48.38, 0.00, -52.34) fatorMorte=0,05 janelaSemPerda=600s
[PVPTEST] PASS setup/raidsystem-ligado 
[PVPTEST] ilha: centro=(5,1) raio=30 modo=Radius celulas=9 (~0,01 km2) biomas: Meadows 100%
[PVPTEST] plugins: AzuAntiCheat 5.1.1, VipList 1.0.0, Detalhes.Deadheim 7.1.0, PvpTestDriver 1.0.0, Guilds 1.1.14, RaidSystem 2.1.0, Creature Level & Loot Control 5.0.5, Groups 1.2.12, Server Characters 1.4.17
[PVPTEST] PASS setup/arredor-do-templo-seguro Ilha Inicial
[PVPTEST] PASS setup/aberto-nao-seguro 
[PVPTEST] PASS setup/templo-e-arena 
[PVPTEST] dummy criado em (56.62, 67.03, 0.66) id=424242 nome=Dummy
[PVPTEST] PASS setup/fora-de-cutscene eu=False dummy=False
[PVPTEST] PASS setup/pvp-ativo-no-aberto flags=None
[PVPTEST] PASS setup/hud <color=#ff5050>PvP ATIVO</color>
[PVPTEST] PASS setup/servidor-recebeu-hello -PvpTest.json: 0 jogador(es). |[Deadheim PvP] Solo (1933475744) sincronizado. |09/30/2026 11:26:37: Placed location WoodHouse6 in zone 5,-4  duration 10,1925 ms |09/30/2026 11:26:37: Placed location Dolmen02 in zone 5,-1  duration 1,0045 ms |09/30/2026 11:26:37: Placed location Dolmen01 in zone 5,0  duration 1,0082 ms |09/30/2026 11:26:38: Placed location Dolmen02 in zone 5,1  duration 1,0099 ms |
[PVPTEST] === ajustes
[PVPTEST] ajustes: admin=False tela=960x540
[PVPTEST] ajustes: adminlist no cliente=[] eu=Steam_76561198053330247
[PVPTEST] PASS ajustes/esc-volta-ao-menu (patch no Menu.Update) 
[PVPTEST] PASS ajustes/menu-abre 
[PVPTEST] menu do ESC: dialogo=Menu [Continue 'Continue' y=384..407] [ManualSave 'Save' y=361..384] [CurrentPlayerList 'Player list' y=338..361] [Settings 'Settings' y=315..338] [Deadheim 'Deadheim' y=292..315] [Logout 'Log Out' y=269..292] [Exit 'Quit' y=246..269] 
[PVPTEST] PASS ajustes/botao-no-menu 
[PVPTEST] PASS ajustes/botao-texto Deadheim
[PVPTEST] PASS ajustes/botao-sem-sobrepor 
[PVPTEST] PASS ajustes/botao-dentro-da-tela x=430..530 y=292..315 tela=960x540
[PVPTEST] foto: C:\Users\Werner\AppData\Local\Temp\deadheim-pvptest\fotos\ajustes-1-menu-esc.png
[PVPTEST] PASS ajustes/janela-abre 
[PVPTEST] PASS ajustes/menu-some-atras alfa=0
[PVPTEST] PASS ajustes/janela-dentro-da-tela x=215..745 y=95..445 tela=960x540
[PVPTEST] PASS ajustes/aba-servidor-so-admin 
[PVPTEST] foto: C:\Users\Werner\AppData\Local\Temp\deadheim-pvptest\fotos\ajustes-2-meus-ajustes.png
[PVPTEST] meus ajustes: RaidSystem(3), Creature Level & Loot Control(3), Groups(9), Guilds(5)
[PVPTEST] PASS ajustes/meus-tem-mods 
[PVPTEST] PASS ajustes/meus-sem-opcao-do-servidor 
[PVPTEST] PASS ajustes/meus-sem-config-de-servidor-do-deadheim 
[PVPTEST] PASS ajustes/meus-linha-da-tecla 
[PVPTEST] PASS ajustes/meus-edita PageUp -> PageDown
[PVPTEST] PASS ajustes/meus-salva-no-cfg 
[PVPTEST] PASS ajustes/meus-status Salvo: Menu Key = PageDown
[PVPTEST] foto: C:\Users\Werner\AppData\Local\Temp\deadheim-pvptest\fotos\ajustes-3-meus-ajustes-editado.png
[PVPTEST] PASS ajustes/meus-recusa-invalido Valor invalido para tecla.
[PVPTEST] PASS ajustes/meus-desfaz 
[PVPTEST] PASS ajustes/jogador-negado So admins do servidor (adminlist.txt) podem mexer na config do servidor.
[PVPTEST] PASS ajustes/jogador-nao-muda-o-servidor valor=100
[PVPTEST] PASS ajustes/servidor-registra-a-tentativa [Deadheim] Ajustes: 76561198053330247 pediu 'mods' sem ser admin. |[Deadheim] Ajustes: 76561198053330247 pediu 'definir' sem ser admin. |
[PVPTEST] PASS ajustes/voltar-fecha-a-janela 
[PVPTEST] PASS ajustes/voltar-devolve-o-menu alfa=1
[PVPTEST] PASS ajustes/menu-fechado-fecha-a-janela 
[PVPTEST] PASS ajustes/menu-fechado-devolve-o-menu alfa=1
[PVPTEST] DONE pass=36 fail=0
```

## [PVPTEST] modo admin (rodada final, com 2f0d0af)

```
[PVPTEST] driver ativo: papel=S sync=C:\Users\Werner\AppData\Local\Temp\deadheim-pvptest\sync save=C:\Users\Werner\AppData\Local\Temp\deadheim-pvptest\chars-S
[PVPTEST] criando personagem Solo
[PVPTEST] RPC_AdminList recebido: [76561198053330247, Steam_76561198053330247]
[PVPTEST] spawn ok (solo)
[PVPTEST] === setup
[PVPTEST] PASS setup/modulo-ativo 
[PVPTEST] PASS setup/templo templo=(4.62, 66.22, 0.66)
[PVPTEST] posicoes: templo=(4.62, 0.00, 0.66) seguro=(24.62, 0.00, 0.66) aberto=(54.62, 0.00, 0.66) wardA=(81.12, 0.00, 15.88) wardB=(46.19, 0.00, 28.44) castelo=(-48.38, 0.00, -52.34) fatorMorte=0,05 janelaSemPerda=600s
[PVPTEST] PASS setup/raidsystem-ligado 
[PVPTEST] ilha: centro=(5,1) raio=30 modo=Radius celulas=9 (~0,01 km2) biomas: Meadows 100%
[PVPTEST] plugins: AzuAntiCheat 5.1.1, VipList 1.0.0, Detalhes.Deadheim 7.1.0, PvpTestDriver 1.0.0, Guilds 1.1.14, RaidSystem 2.1.0, Creature Level & Loot Control 5.0.5, Groups 1.2.12, Server Characters 1.4.17
[PVPTEST] PASS setup/arredor-do-templo-seguro Ilha Inicial
[PVPTEST] PASS setup/aberto-nao-seguro 
[PVPTEST] PASS setup/templo-e-arena 
[PVPTEST] dummy criado em (56.62, 67.03, 0.66) id=424242 nome=Dummy
[PVPTEST] PASS setup/fora-de-cutscene eu=False dummy=False
[PVPTEST] PASS setup/pvp-ativo-no-aberto flags=None
[PVPTEST] PASS setup/hud <color=#ff5050>PvP ATIVO</color>
[PVPTEST] PASS setup/servidor-recebeu-hello s\Werner\AppData\Local\Temp\deadheim-pvptest\server\BepInEx\config\Deadheim\pvp-PvpTest.json: 0 jogador(es). |[Deadheim PvP] Solo (1933475744) sincronizado. |09/30/2026 11:23:39: Placed location WoodHouse6 in zone 5,-4  duration 11,3131 ms |09/30/2026 11:23:39: Placed location Dolmen02 in zone 5,-1  duration 0,5089 ms |09/30/2026 11:23:39: Placed location Dolmen01 in zone 5,0  duration 1,0224 ms |
[PVPTEST] === ajustes
[PVPTEST] ajustes: admin=True tela=960x540
[PVPTEST] ajustes: adminlist no cliente=[76561198053330247, Steam_76561198053330247] eu=Steam_76561198053330247
[PVPTEST] PASS ajustes/esc-volta-ao-menu (patch no Menu.Update) 
[PVPTEST] PASS ajustes/menu-abre 
[PVPTEST] menu do ESC: dialogo=Menu [Continue 'Continue' y=384..407] [ManualSave 'Save' y=361..384] [CurrentPlayerList 'Player list' y=338..361] [Settings 'Settings' y=315..338] [Deadheim 'Deadheim' y=292..315] [Logout 'Log Out' y=269..292] [Exit 'Quit' y=246..269] 
[PVPTEST] PASS ajustes/botao-no-menu 
[PVPTEST] PASS ajustes/botao-texto Deadheim
[PVPTEST] PASS ajustes/botao-sem-sobrepor 
[PVPTEST] PASS ajustes/botao-dentro-da-tela x=430..530 y=292..315 tela=960x540
[PVPTEST] foto: C:\Users\Werner\AppData\Local\Temp\deadheim-pvptest\fotos\ajustes-1-menu-esc.png
[PVPTEST] PASS ajustes/janela-abre 
[PVPTEST] PASS ajustes/menu-some-atras alfa=0
[PVPTEST] PASS ajustes/janela-dentro-da-tela x=215..745 y=95..445 tela=960x540
[PVPTEST] PASS ajustes/aba-servidor-so-admin 
[PVPTEST] foto: C:\Users\Werner\AppData\Local\Temp\deadheim-pvptest\fotos\ajustes-2-meus-ajustes.png
[PVPTEST] meus ajustes: RaidSystem(3), Creature Level & Loot Control(3), Groups(9), Guilds(5)
[PVPTEST] PASS ajustes/meus-tem-mods 
[PVPTEST] PASS ajustes/meus-sem-opcao-do-servidor 
[PVPTEST] PASS ajustes/meus-sem-config-de-servidor-do-deadheim 
[PVPTEST] PASS ajustes/meus-linha-da-tecla 
[PVPTEST] PASS ajustes/meus-edita PageUp -> PageDown
[PVPTEST] PASS ajustes/meus-salva-no-cfg 
[PVPTEST] PASS ajustes/meus-status Salvo: Menu Key = PageDown
[PVPTEST] foto: C:\Users\Werner\AppData\Local\Temp\deadheim-pvptest\fotos\ajustes-3-meus-ajustes-editado.png
[PVPTEST] PASS ajustes/meus-recusa-invalido Valor invalido para tecla.
[PVPTEST] PASS ajustes/meus-desfaz 
[PVPTEST] servidor: Deadheim(90), RaidSystem(30), VipList(1), AzuAntiCheat(2), Creature Level & Loot Control(64), Groups(12), Guilds(13), Server Characters(14)
[PVPTEST] PASS ajustes/servidor-lista-mods 
[PVPTEST] PASS ajustes/servidor-opcoes-do-deadheim 
[PVPTEST] foto: C:\Users\Werner\AppData\Local\Temp\deadheim-pvptest\fotos\ajustes-4-servidor.png
[PVPTEST] PASS ajustes/admin-muda-e-o-servidor-sincroniza 100 -> 123
[PVPTEST] PASS ajustes/admin-status Servidor: CartographyTableAmount = 123 (ja valendo)
[PVPTEST] PASS ajustes/admin-salva-no-cfg-do-servidor 
[PVPTEST] PASS ajustes/servidor-registra-quem-mudou [Deadheim] Ajustes: 76561198053330247 mudou Detalhes.Deadheim [Server config] CartographyTableAmount: '100' -> '123' |[Deadheim] Config recarregada de Detalhes.Deadheim.cfg. |
[PVPTEST] foto: C:\Users\Werner\AppData\Local\Temp\deadheim-pvptest\fotos\ajustes-5-servidor-editado.png
[PVPTEST] PASS ajustes/admin-invalido-recusado Servidor recusou: Valor invalido para numero inteiro.
[PVPTEST] PASS ajustes/admin-caixa-killfeed 
[PVPTEST] PASS ajustes/admin-caixa-muda 
[PVPTEST] PASS ajustes/admin-caixa-desfaz 
[PVPTEST] PASS ajustes/admin-botao-padrao 100
[PVPTEST] PASS ajustes/admin-desfaz 
[PVPTEST] PASS ajustes/voltar-fecha-a-janela 
[PVPTEST] PASS ajustes/voltar-devolve-o-menu alfa=1
[PVPTEST] PASS ajustes/menu-fechado-fecha-a-janela 
[PVPTEST] PASS ajustes/menu-fechado-devolve-o-menu alfa=1
[PVPTEST] DONE pass=45 fail=0
```

## [PVPTEST] modo admin, rodada que falhou (antes do 2f0d0af, so os passos do ajustes)

```
[PVPTEST] === ajustes
[PVPTEST] ajustes: admin=False tela=960x540
[PVPTEST] PASS ajustes/esc-volta-ao-menu (patch no Menu.Update) 
[PVPTEST] PASS ajustes/menu-abre 
[PVPTEST] menu do ESC: dialogo=Menu [Continue 'Continue' y=384..407] [ManualSave 'Save' y=361..384] [CurrentPlayerList 'Player list' y=338..361] [Settings 'Settings' y=315..338] [Deadheim 'Deadheim' y=292..315] [Logout 'Log Out' y=269..292] [Exit 'Quit' y=246..269] 
[PVPTEST] PASS ajustes/botao-no-menu 
[PVPTEST] PASS ajustes/botao-texto Deadheim
[PVPTEST] PASS ajustes/botao-sem-sobrepor 
[PVPTEST] PASS ajustes/botao-dentro-da-tela x=430..530 y=292..315 tela=960x540
[PVPTEST] foto: C:\Users\Werner\AppData\Local\Temp\deadheim-pvptest\fotos\ajustes-1-menu-esc.png
[PVPTEST] PASS ajustes/janela-abre 
[PVPTEST] PASS ajustes/menu-some-atras alfa=0
[PVPTEST] PASS ajustes/janela-dentro-da-tela x=215..745 y=95..445 tela=960x540
[PVPTEST] PASS ajustes/aba-servidor-so-admin 
[PVPTEST] foto: C:\Users\Werner\AppData\Local\Temp\deadheim-pvptest\fotos\ajustes-2-meus-ajustes.png
[PVPTEST] meus ajustes: RaidSystem(3), Creature Level & Loot Control(3), Groups(9), Guilds(5)
[PVPTEST] PASS ajustes/meus-tem-mods 
[PVPTEST] PASS ajustes/meus-sem-opcao-do-servidor 
[PVPTEST] PASS ajustes/meus-sem-config-de-servidor-do-deadheim 
[PVPTEST] PASS ajustes/meus-linha-da-tecla 
[PVPTEST] PASS ajustes/meus-edita PageUp -> PageDown
[PVPTEST] PASS ajustes/meus-salva-no-cfg 
[PVPTEST] PASS ajustes/meus-status Salvo: Menu Key = PageDown
[PVPTEST] foto: C:\Users\Werner\AppData\Local\Temp\deadheim-pvptest\fotos\ajustes-3-meus-ajustes-editado.png
[PVPTEST] PASS ajustes/meus-recusa-invalido Valor invalido para tecla.
[PVPTEST] PASS ajustes/meus-desfaz 
[PVPTEST] FAIL ajustes/jogador-negado Salvo: Menu Key = PageUp
[PVPTEST] FAIL ajustes/jogador-nao-muda-o-servidor valor=107
[PVPTEST] FAIL ajustes/servidor-registra-a-tentativa [Deadheim] Ajustes: 76561198053330247 mudou Detalhes.Deadheim [Server config] CartographyTableAmount: '100' -> '107' |[Deadheim] Config recarregada de Detalhes.Deadheim.cfg. |
[PVPTEST] PASS ajustes/voltar-fecha-a-janela 
[PVPTEST] PASS ajustes/voltar-devolve-o-menu alfa=1
```

## server-unity.log: linhas "[Deadheim] Ajustes" e Exceptions

Nenhuma Exception no server-unity.log nem no LogOutput.log do cliente, nos dois modos.

Jogador:
```
[Deadheim] Ajustes: 76561198053330247 pediu 'mods' sem ser admin.
[Deadheim] Ajustes: 76561198053330247 pediu 'definir' sem ser admin.
```

Admin:
```
[Deadheim] Ajustes: 76561198053330247 mudou Detalhes.Deadheim [Server config] CartographyTableAmount: '100' -> '123'
[Deadheim] Ajustes: 76561198053330247 tentou mudar Detalhes.Deadheim [Server config] CartographyTableAmount para 'abc': Valor invalido para numero inteiro.
[Deadheim] Ajustes: 76561198053330247 mudou Detalhes.Deadheim [PvP] KillFeed: 'true' -> 'false'
[Deadheim] Ajustes: 76561198053330247 mudou Detalhes.Deadheim [PvP] KillFeed: 'false' -> 'true'
[Deadheim] Ajustes: 76561198053330247 mudou Detalhes.Deadheim [Server config] CartographyTableAmount: '123' -> '100'
```

## Fotos (960x540)

- `jogador-1-menu-esc.png` / `admin-1-menu-esc.png`: menu do ESC. O botao **DEADHEIM** fica logo
  abaixo de SETTINGS e acima de LOG OUT, centralizado na mesma coluna, mesma fonte (Norse, caixa alta,
  branca) e mesmo espacamento dos vanilla. Nao sobrepoe nada nem sai da moldura. O teste confere:
  x=430..530, Deadheim y=282..305 (jogador, 1a rodada) / 292..315 (admin), sem sobrepor Settings e Log Out.
  (Os poligonos pretos no fundo da foto do jogador sao o terreno ainda carregando, nao a UI.)
- `*-2-meus-ajustes.png`: janela aberta na aba Meus ajustes. Painel de madeira do Valheim,
  titulo DEADHEIM em laranja, abas com botoes do jogo, lista de mods a esquerda (RaidSystem,
  Creature Level & Loot Control, Groups, Guilds), opcoes agrupadas por secao a direita, busca,
  botao Padrao em cada linha, Voltar embaixo. Ocupa x=215..745 y=95..445: cabe com folga na tela.
  No jogador so a aba "Meus ajustes" aparece (centralizada); no admin aparecem "Meus ajustes" e "Servidor"
  lado a lado. Parece Valheim. Nomes e valores legiveis; as descricoes curtas embaixo de cada opcao e o
  texto dos botoes Padrao ficam bem pequenos a 960x540 (legiveis com esforco; em 1080p devem ficar bons).
- `*-3-meus-ajustes-editado.png`: Menu Key do RaidSystem mudada para PageDown, status verde
  "Salvo: Menu Key = PageDown" embaixo a esquerda.
- `admin-4-servidor.png`: aba Servidor. Lista os mods do servidor (Deadheim, RaidSystem, VipList,
  AzuAntiCheat, CLLC, Groups, Guilds, Server Characters); Deadheim aberto em Montarias / Portal Mats,
  cada opcao com a marca "sincronizado" em azul. Ao passar o mouse, a descricao completa aparece
  embaixo ("[Montarias] SwimStaminaDrain (numero) ... Padrao: 1").
- `admin-5-servidor-editado.png`: mesma tela depois da mudanca; status verde
  "Servidor: CartographyTableAmount = 123 (ja valendo)". A linha editada fica mais abaixo na lista
  (secao Server config), fora da area visivel da foto.

Observacoes (nao sao falhas do teste):
- Em Meus ajustes do RaidSystem aparece "Discord Webhook URL" (descricao diz "Server-side"): o
  RaidSystem nao marca essa opcao como sincronizada, entao ela conta como local. Vale marcar no RaidSystem.
- A foto do jogador ficou com Menu Key = Scoreboard Key = PageDown no meio do teste (o teste desfaz depois).

## Arquivos

- `jogador-*.png`, `admin-*.png`: fotos da rodada final de cada modo.
- `jogador-LogOutput.log`, `admin-LogOutput.log`: BepInEx do cliente.
- `jogador-server-unity.log`, `admin-server-unity.log`: log do servidor dedicado.
