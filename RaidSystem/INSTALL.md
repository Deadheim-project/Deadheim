# Como compilar e instalar o RaidSystem no Windows 11

## Pré-requisitos

### 1. .NET SDK 8+
- Baixe em: https://dotnet.microsoft.com/download
- Instale normalmente, next next finish
- Teste: abra o Terminal (Win+X → Terminal) e digite:
  ```
  dotnet --version
  ```
  Se aparecer um número (ex: 8.0.x), tá certo.

### 2. Valheim com BepInEx
- Instale o BepInEx via Thunderstore Mod Manager ou manualmente:
  https://valheim.thunderstore.io/package/denikson/BepInExPack_Valheim/
- Rode o Valheim uma vez com BepInEx pra ele criar as pastas

### 3. Publicized assemblies
- Instale o mod **Assembly Publicizer** via Thunderstore:
  https://valheim.thunderstore.io/package/iDeathHD/Assembly_Publicizer/
- Rode o jogo uma vez — ele cria a pasta:
  `Valheim\valheim_Data\Managed\publicized_assemblies\`
- Confirme que existem arquivos como `assembly_valheim_publicized.dll` lá dentro

### 4. Guilds mod (opcional mas recomendado)
- Instale via Thunderstore:
  https://thunderstore.io/c/valheim/p/Smoothbrain/Guilds/

---

## Passo a passo

### Passo 1: Baixar as dependências

1. **ServerSync.dll** — baixe a última release:
   https://github.com/blaxxun-boop/ServerSync/releases
   → Baixe `ServerSync.dll`

2. **GuildsAPI.dll** — baixe da release do Guilds:
   https://github.com/blaxxun-boop/Guilds/releases
   → Baixe `GuildsAPI.dll`

3. Coloque os dois DLLs na pasta `libs/` dentro do projeto:
   ```
   RaidSystem/
   ├── libs/
   │   ├── ServerSync.dll
   │   └── GuildsAPI.dll
   ├── Models.cs
   ├── DataStore.cs
   ├── ... (todos os .cs)
   ├── RaidSystem.csproj
   └── ILRepack.targets
   ```

### Passo 2: Ajustar o caminho do Valheim

Abra `RaidSystem.csproj` num editor de texto e edite esta linha:
```xml
<ValheimPath>C:\Program Files (x86)\Steam\steamapps\common\Valheim</ValheimPath>
```
Mude para onde seu Valheim está instalado. Para descobrir:
- Steam → Biblioteca → Valheim → botão direito → Propriedades → Arquivos Instalados → Procurar

### Passo 3: Compilar

Abra o Terminal na pasta do projeto e rode:
```
dotnet build --configuration Release
```

Se tudo der certo, a saída mostra:
```
Build succeeded.
    0 Warning(s)
    0 Error(s)
```

O DLL final vai estar em:
```
bin/Release/net472/RaidSystem.dll
```

E se a config de `CopyToPlugins` estiver certa, ele já copia automaticamente pra:
```
Valheim/BepInEx/plugins/RaidSystem/RaidSystem.dll
```

### Passo 4: Instalar manualmente (se o auto-copy não funcionou)

Copie o `RaidSystem.dll` para:
```
C:\...\Valheim\BepInEx\plugins\RaidSystem\RaidSystem.dll
```
Crie a pasta `RaidSystem` se não existir.

### Passo 5: Rodar o jogo

1. Abra o Valheim
2. No console do BepInEx (janela preta que abre junto), procure:
   ```
   [Info   : RaidSystem] RaidSystem v2.0.0 loaded.
   [Info   : RaidSystem] Guilds mod active: True
   [Info   : RaidSystem] RaidWard created: scale=3, HP=10000
   ```
3. Se aparecer, tá funcionando!

### Passo 6: Configurar

O config é gerado automaticamente na primeira execução em:
```
Valheim\BepInEx\config\Detalhes.RaidSystem.cfg
```

Exemplo de zonas de raid:
```ini
[2 - Raid Rules]
Raid Zones = Castelo Norte,500,300,150,300|Porto Sul,1200,800,100,250|Arena Central,0,0,200,400
```
Formato: `nome,x,z,raioWard,raioPvP`
- raioWard = área onde a ward é protegida e build é bloqueado
- raioPvP = área onde PvP é forçado (pode ser maior que raioWard)

---

## Estrutura do projeto

```
RaidSystem/
├── libs/
│   ├── ServerSync.dll        ← baixar do GitHub
│   └── GuildsAPI.dll         ← baixar do GitHub
├── Models.cs                 ← classes de dados
├── DataStore.cs              ← persistência JSON thread-safe
├── GuildsIntegration.cs      ← integração com Guilds (API direta)
├── CooldownManager.cs        ← cooldown por território
├── ScoreManager.cs           ← kills, conquistas, ranking
├── RPCManager.cs             ← comunicação server↔client
├── Patches.cs                ← Harmony patches (dano, PvP, build)
├── Util.cs                   ← zonas, helpers
├── WardSetup.cs              ← clona guard_stone em runtime
├── GUI.cs                    ← stub de UI (adaptar)
├── dWebHook.cs               ← Discord webhook
├── RaidSystemPlugin.cs       ← entry point + configs
├── RaidSystem.csproj         ← projeto com referências
└── ILRepack.targets          ← merge ServerSync no DLL final
```

## Dependências finais

| Dependência | Tipo | Obrigatória |
|-------------|------|-------------|
| BepInEx 5.4+ | Framework | Sim |
| ServerSync | Embutida (ILRepack) | Sim |
| GuildsAPI | Referência (não embutida) | Não (soft dep) |
| Guilds mod | Plugin do jogador | Não (recomendada) |

O jogador só precisa instalar o `RaidSystem.dll` — o ServerSync já está dentro.
Se quiser guilds como facções, instala o Guilds mod separado.

---

## Troubleshooting

**"assembly_valheim_publicized.dll not found"**
→ Rode o jogo com Assembly Publicizer instalado pelo menos uma vez.

**"ServerSync.dll not found"**
→ Confirme que está na pasta `libs/` e que o .csproj aponta pra lá.

**"Type 'Guilds.API' not found"**
→ Baixe o `GuildsAPI.dll` e coloque em `libs/`.

**Build falha com "Splatform.Valheim not found"**
→ Procure `Splatform.Valheim.dll` em `Valheim\valheim_Data\Managed\` — se não existir
na sua versão, comente a referência no .csproj e ajuste o `PlayerReference` em `GuildsIntegration.cs`.

**Ward não aparece no martelo**
→ Verifique no console se aparece "[RaidSystem] RaidWard created" e "RaidWard added to hammer".
