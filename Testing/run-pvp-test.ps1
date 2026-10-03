<#
    Teste de ponta a ponta do modulo de PvP do Deadheim, sem ninguem clicar em nada.

    Sobe um servidor dedicado local e dois clientes reais do Valheim (Alfa e Bravo), cada
    um com a sua propria arvore BepInEx (via argumentos do Doorstop, como o launcher faz),
    so com Deadheim.dll + VipList.dll (+ o PvpTestDriver.dll nos clientes). Nada e
    instalado nas pastas do jogo; personagens de teste vao para uma pasta propria.

    1. Primeira subida do servidor: gera o mundo e registra onde fica o templo inicial
       (e quanto mede a zona segura no modo Island).
    2. Grava a config de teste (zona segura pequena, arena no templo, tempos curtos) e
       sobe o servidor de novo.
    3. Sobe os dois clientes. O PvpTestDriver cria os personagens, conecta e segue o
       roteiro de Driver.cs. O resultado sai como linhas [PVPTEST] no log de cada cliente.

    As preferencias do Valheim (registro HKCU\Software\IronGate\valheim) sao salvas antes e
    restauradas no fim: os clientes de teste abrem em janela pequena.

    Uso:
      powershell -ExecutionPolicy Bypass -File Testing\run-pvp-test.ps1 -Root D:\tmp\pvptest

    Opcao Deadheim do menu do ESC (passo "ajustes", fotos em <Root>\fotos):
      ... -Solo -Steps ajustes           como jogador comum
      ... -Solo -Steps ajustes -Admin    como admin (a conta Steam logada entra na adminlist.txt)
#>
param(
    [string]$Root = (Join-Path $env:TEMP 'deadheim-pvptest'),
    [int]$Port = 2476,
    [string]$Password = 'pvptest1',
    [string]$ServerDir = 'C:\Program Files (x86)\Steam\steamapps\common\Valheim dedicated server',
    [string]$ClientDir = 'C:\Program Files (x86)\Steam\steamapps\common\Valheim',
    [string]$BepInExCore = (Join-Path $env:APPDATA 'DeadheimLauncher\profiles\Default\game\BepInEx\core'),
    # O RaidSystem exige o Guilds; vem do perfil do launcher, o mesmo que os jogadores usam.
    [string]$GuildsDll = (Join-Path $env:APPDATA 'DeadheimLauncher\profiles\Default\game\BepInEx\plugins\guilds\Guilds.dll'),
    # Terceiros do pacote PvP, do perfil do launcher (o que ele baixou do Hexium). O que nao
    # estiver la fica de fora do teste, com aviso.
    [string]$PackPlugins = (Join-Path $env:APPDATA 'DeadheimLauncher\profiles\Default\game\BepInEx\plugins'),
    [string[]]$PackMods = @('Groups.dll', 'ServerCharacters.dll', 'CreatureLevelControl.dll', 'AzuAnticheat.dll'),
    [int]$TimeoutMinutes = 25,
    [switch]$NewWorld,
    # Um cliente so, com o segundo jogador simulado pelo driver: cabe numa maquina onde
    # dois clientes + servidor estouram a memoria.
    [switch]$Solo,
    # So estes passos do roteiro solo, separados por virgula (o setup sempre roda).
    [string[]]$Steps = @(),
    # Poe a conta Steam deste PC na adminlist.txt do servidor de teste. Admin passa por cima
    # de ward, portal e teleporte em combate: use so com -Steps ajustes.
    [switch]$Admin,
    # SteamID64 do admin; sem ele, o da conta logada no Steam (HKCU\...\ActiveProcess\ActiveUser).
    [string]$AdminId = '',
    [switch]$KeepRunning
)

$ErrorActionPreference = 'Stop'
$repo = Split-Path -Parent $PSScriptRoot

if ($Admin) {
    if (-not $AdminId) {
        $activeUser = (Get-ItemProperty 'HKCU:\Software\Valve\Steam\ActiveProcess' -ErrorAction SilentlyContinue).ActiveUser
        if (-not $activeUser) { throw 'Nao achei a conta do Steam (Steam aberto?). Passe -AdminId <SteamID64>.' }
        $AdminId = ([UInt64]76561197960265728 + [UInt64]$activeUser).ToString()
    }
    if (($Steps | Where-Object { $_ -ne 'ajustes' }) -or -not $Steps) {
        Write-Warning 'Com -Admin o cliente de teste e admin e os passos de PvP que dependem de ward/teleporte podem falhar. Use -Steps ajustes.'
    }
}
$deadheimDll = Join-Path $repo 'bin\Release\Deadheim.dll'
$vipDll = Join-Path $repo 'bin\Release\VipList.dll'
$driverDll = Join-Path $PSScriptRoot 'PvpTestDriver\bin\Release\PvpTestDriver.dll'
$raidDll = Join-Path $repo 'bin\Release\RaidSystem.dll'
# A pedra do Hearthstone: o passo retreat consome a pedra de verdade (o bloqueio em luta depende dos dois mods).
$hearthDll = Join-Path $repo 'bin\Release\Hearthstone.dll'

# Terceiros do pacote PvP achados no perfil do launcher.
$packDlls = @()
foreach ($name in $PackMods) {
    $found = Get-ChildItem -Path $PackPlugins -Recurse -Filter $name -ErrorAction SilentlyContinue | Select-Object -First 1
    if ($found) { $packDlls += $found.FullName } else { Write-Warning "$name nao esta em $PackPlugins; fica fora do teste." }
}
$antiCheat = $packDlls | Where-Object { (Split-Path $_ -Leaf) -eq 'AzuAnticheat.dll' }

# Castelo de teste: zona do RaidSystem a este deslocamento do templo, com dono "Lobos".
# O PvpTestDriver (Solo.cs, CastleOffset) procura terra dentro dela com o mesmo numero.
$castleOffset = 53
$castleRadius = 30

foreach ($f in @($deadheimDll, $vipDll, $driverDll, $raidDll, $hearthDll, $GuildsDll, "$ServerDir\valheim_server.exe", "$ClientDir\valheim.exe", "$BepInExCore\BepInEx.Preloader.dll")) {
    if (-not (Test-Path $f)) { throw "Nao encontrei $f" }
}

New-Item -ItemType Directory -Force $Root | Out-Null
$processes = @()

function Write-Step($text) { Write-Host ("[{0}] {1}" -f (Get-Date -Format 'HH:mm:ss'), $text) }

function New-BepInExTree([string]$dir, [string[]]$plugins) {
    if (Test-Path $dir) { Remove-Item -Recurse -Force $dir }
    New-Item -ItemType Directory -Force "$dir\BepInEx\plugins\Deadheim", "$dir\BepInEx\config" | Out-Null
    Copy-Item -Recurse $BepInExCore "$dir\BepInEx\core"
    foreach ($p in $plugins) { Copy-Item $p "$dir\BepInEx\plugins\Deadheim\" }
}

function Write-ServerConfig([string]$mode, [string]$arena) {
    $cfg = @"
[Server config]
WardRadius = 12
SafeArea = 0

[Wards]
PlayerWardRadius = 12

[Wards - Territorio]
TerritoryWardEnabled = true
TerritoryWardRadius = 12
TerritoryWardActivationMinutes = 60
TerritoryWardSpacing = 2

[PvP]
Enabled = true
ForcePvp = true
DamageMultiplier = 0.5
WardDefenseMultiplier = 0.5
ImmunityMinutes = 2
CombatTagSeconds = 6
KillCreditSeconds = 5
KillFeed = true
# Solo: o Dummy nao e um jogador conectado, e o servidor so aceita matador conectado.
KillerMustBeOnline = $(if ($Solo) { 'false' } else { 'true' })

[PvP - PK]
PkTiers = 1:5:Skills,2:10:Skills,3:60:Unequipped,5:-1:All
PkSkillLossMultiplier = 2
PkClearsOnDeath = true
PkClearsOnPveDeath = false
PkPenaltyOnPveDeath = true
PkTimeOnlineOnly = true
PkNoSafeZone = true
AggressorSeconds = 600
AggressorPausesInCombat = true

[PvP - Chefes]
BossGapMax = 1

[PvP - Zonas]
StartIslandMode = $mode
StartIslandRadius = $(if ($mode -eq 'Island') { 1500 } else { 30 })
SafeZones =
ArenaZones = $arena
TransportsSafe = false
TransportsInvulnerable = false
ShipsSafe = false
ShipsInvulnerable = false

[PvP - Bounty]
BountyDelaySeconds = 5
# 1000 moedas = 30 s de cacada online: da para ver a bounty expirar.
BountyMinutesPer1000 = 0.5
BountyUntilDeathAt = 5000
BountyKillerSharePercent = 75
BountyBuyoutMultiplier = 1.5
BountyCooldownMinutes = 0.1
# Solo: so um jogador online de verdade (o Dummy nao e peer).
BountyMinPlayers = 1
BountyPausesInOwnWard = true
HuntedNoWardDefense = true

[PvP - Saque]
PvpCoinDropPercent = 100
PvpCargoDropPercent = 50
"@
    Set-Content -Path "$Root\server\BepInEx\config\Detalhes.Deadheim.cfg" -Value $cfg -Encoding UTF8
}

# O BepInEx do servidor nao copia o log da Unity (WriteUnityLog = false no pack), e o
# Debug.Log dos mods so aparece no -logFile. E nele que se espera.
$serverLog = "$Root\server-unity.log"

function Write-RaidSystem([int]$tx, [int]$tz) {
    $cx = $tx - $castleOffset
    $cz = $tz - $castleOffset
    $cfg = @"
[2 - Raid Rules]
Raid Hours (UTC) = 0-24
Raid Zones = CasteloTeste,$cx,$cz,$castleRadius,$castleRadius,*,1,0
"@
    Set-Content -Path "$Root\server\BepInEx\config\Detalhes.RaidSystem.cfg" -Value $cfg -Encoding UTF8

    # Territorio ja dominado, como se a guilda Lobos tivesse conquistado o castelo.
    New-Item -ItemType Directory -Force "$Root\server\BepInEx\config\RaidSystem" | Out-Null
    $data = @"
{"players":[],"scores":[],"territories":[{"Name":"CasteloTeste","X":$cx,"Y":0,"Z":$cz,"OwnerTeamId":"Lobos","LastConquestTimestamp":0,"PendingTribute":0,"LastTributeUtc":0}]}
"@
    Set-Content -Path "$Root\server\BepInEx\config\RaidSystem\RaidData.json" -Value $data -Encoding UTF8
}

function Start-Server {
    if (Test-Path $serverLog) { Remove-Item $serverLog -Force }
    $env:SteamAppId = '892970'
    $argLine = "--doorstop-enabled true --doorstop-target-assembly `"$Root\server\BepInEx\core\BepInEx.Preloader.dll`" " +
            "-nographics -batchmode -name DeadheimPvpTest -port $Port -world PvpTest -password $Password -public 0 " +
            "-savedir `"$Root\saves-server`" -logFile `"$Root\server-unity.log`""
    $p = Start-Process -FilePath "$ServerDir\valheim_server.exe" -ArgumentList $argLine -WorkingDirectory $ServerDir -WindowStyle Hidden -PassThru
    $script:processes += $p
    return $p
}

function Wait-Log([string]$path, [string]$pattern, [int]$seconds) {
    $until = (Get-Date).AddSeconds($seconds)
    while ((Get-Date) -lt $until) {
        if (Test-Path $path) {
            $hit = Select-String -Path $path -Pattern $pattern -ErrorAction SilentlyContinue | Select-Object -Last 1
            if ($hit) { return $hit.Line }
        }
        Start-Sleep -Seconds 2
    }
    return $null
}

$clients = @()

# Commit livre (RAM + pagefile) em GB. Um cliente do Valheim reserva uns 3-4 GB; o
# pagefile ainda pode crescer ate o maximo configurado, por isso o solo pede menos.
function Wait-Memory([double]$needGb) {
    $until = (Get-Date).AddMinutes(10)
    while ($true) {
        $free = (Get-CimInstance Win32_OperatingSystem).FreeVirtualMemory / 1MB
        if ($free -ge $needGb) { return }
        if ((Get-Date) -gt $until) { throw ("Memoria insuficiente: {0:N1} GB de commit livre, preciso de {1} GB." -f $free, $needGb) }
        Write-Step ("Esperando memoria: {0:N1} GB livres, preciso de {1} GB" -f $free, $needGb)
        Start-Sleep -Seconds 15
    }
}

function Stop-All {
    foreach ($p in $script:processes) {
        try { if (-not $p.HasExited) { Stop-Process -Id $p.Id -Force } } catch { }
    }
    $script:processes = @()
}

$prefsBackup = "$Root\valheim-prefs.reg"
& reg export "HKCU\Software\IronGate\valheim" $prefsBackup /y | Out-Null

try {
    # ------------------------------------------------------------ servidor: mundo e templo
    Write-Step "Montando arvores BepInEx em $Root"
    New-BepInExTree "$Root\server" (@($deadheimDll, $vipDll, $raidDll, $hearthDll, $GuildsDll) + $packDlls)
    New-BepInExTree "$Root\clientA" (@($deadheimDll, $vipDll, $raidDll, $hearthDll, $GuildsDll, $driverDll) + $packDlls)
    New-BepInExTree "$Root\clientB" (@($deadheimDll, $vipDll, $raidDll, $hearthDll, $GuildsDll, $driverDll) + $packDlls)
    New-BepInExTree "$Root\clientS" (@($deadheimDll, $vipDll, $raidDll, $hearthDll, $GuildsDll, $driverDll) + $packDlls)
    if ($antiCheat) {
        # AzuAntiCheat: a whitelist do servidor e o espelho das pastas de plugin do cliente.
        $whitelist = "$Root\server\BepInEx\config\AzuAntiCheat_Whitelist"
        New-Item -ItemType Directory -Force $whitelist | Out-Null
        Copy-Item -Recurse "$Root\clientS\BepInEx\plugins\*" $whitelist
        Write-Step "AzuAntiCheat: whitelist com $((Get-ChildItem -Recurse -File $whitelist).Count) arquivos"
    }
    Write-Step "Terceiros do pacote no teste: $(( @($GuildsDll) + $packDlls | ForEach-Object { Split-Path $_ -Leaf }) -join ', ')" 
    foreach ($d in @("$Root\sync", "$Root\chars-A", "$Root\chars-B", "$Root\chars-S")) {
        if (Test-Path $d) { Remove-Item -Recurse -Force $d }
        New-Item -ItemType Directory -Force $d | Out-Null
    }
    if ($NewWorld -and (Test-Path "$Root\saves-server")) { Remove-Item -Recurse -Force "$Root\saves-server" }
    Get-ChildItem "$Root\server\BepInEx\config" -Filter 'pvp-*.json' -ErrorAction SilentlyContinue | Remove-Item -Force

    Write-ServerConfig 'Island' ''
    Write-Step "Servidor (1a subida, modo Island) na porta $Port"
    Start-Server | Out-Null
    $line = Wait-Log $serverLog 'Templo inicial em x=' 600
    if (-not $line) { throw "O servidor nao registrou o templo inicial em 10 min. Veja $serverLog" }
    Write-Step $line
    if ($line -notmatch 'x=(-?\d+) z=(-?\d+)') { throw "Nao entendi a linha do templo: $line" }
    $tx = $Matches[1]; $tz = $Matches[2]
    Stop-All
    Start-Sleep -Seconds 5

    # -------------------------------------------------------------- servidor: config de teste
    Get-ChildItem "$Root\server\BepInEx\config" -Filter 'Deadheim' -Directory -ErrorAction SilentlyContinue | Remove-Item -Recurse -Force
    # Sempre reescrita: sem -Admin, uma rodada anterior com -Admin nao deixa o cliente admin.
    New-Item -ItemType Directory -Force "$Root\saves-server" | Out-Null
    $adminLines = @('// adminlist do teste (run-pvp-test.ps1)')
    if ($Admin) { $adminLines += @($AdminId, "Steam_$AdminId"); Write-Step "Admin no servidor de teste: $AdminId" }
    Set-Content -Path "$Root\saves-server\adminlist.txt" -Value $adminLines -Encoding ASCII
    if (Test-Path "$Root\fotos") { Remove-Item -Recurse -Force "$Root\fotos" }
    Write-ServerConfig 'Radius' "ArenaTeste,$tx,$tz,15"
    Write-RaidSystem ([int]$tx) ([int]$tz)
    Write-Step "Servidor (2a subida, config de teste; arena no templo $tx,$tz)"
    Start-Server | Out-Null
    if (-not (Wait-Log $serverLog 'Templo inicial em x=' 300)) { throw 'O servidor nao subiu de novo.' }
    # O templo aparece antes de o servidor aceitar conexao: quando o mundo nao foi salvo, ele
    # ainda regera as locations (~1 min) e so depois abre o socket. Cliente antes disso toma
    # ErrorConnectFailed e fica parado no menu, porque o +connect so tenta uma vez.
    if (-not (Wait-Log $serverLog 'Opened Steam server' 300)) { throw 'O servidor nao abriu conexoes.' }
    Start-Sleep -Seconds 5

    # ----------------------------------------------------------------------- clientes
    # Um de cada vez: os dois carregando o mundo juntos estouram memoria numa maquina
    # comum (crash nativo da Unity no segundo). O B so abre depois que o A spawnou.
    $roles = if ($Solo) { @('S') } else { @('A', 'B') }
    foreach ($role in $roles) {
        Wait-Memory $(if ($Solo) { 1.5 } else { 6 })
        $dir = "$Root\client$role"
        $argLine = "--doorstop-enabled true --doorstop-target-assembly `"$dir\BepInEx\core\BepInEx.Preloader.dll`" " +
                "+connect 127.0.0.1:$Port -password $Password " +
                "-dhtest-role $role -dhtest-sync `"$Root\sync`" -dhtest-save `"$Root\chars-$role`" " +
                $(if ($Steps) { "-dhtest-steps $($Steps -join ',') " } else { '' }) +
                "-screen-fullscreen 0 -screen-width 960 -screen-height 540 -logFile `"$Root\client$role-unity.log`""
        Write-Step "Cliente $role"
        $client = Start-Process -FilePath "$ClientDir\valheim.exe" -ArgumentList $argLine -WorkingDirectory $ClientDir -PassThru
        $script:processes += $client
        $script:clients += $client
        $until = (Get-Date).AddMinutes(8)
        while (-not (Test-Path "$Root\sync\spawned-$role")) {
            if ($client.HasExited) { throw "Cliente $role caiu antes de entrar no mundo (codigo $($client.ExitCode)). Veja $Root\client$role-unity.log" }
            if ((Get-Date) -gt $until) { throw "Cliente $role nao entrou no mundo em 8 min." }
            Start-Sleep -Seconds 3
        }
        Write-Step "Cliente $role no mundo"
    }

    Write-Step "Esperando o roteiro (ate $TimeoutMinutes min)"
    $until = (Get-Date).AddMinutes($TimeoutMinutes)
    while ((Get-Date) -lt $until) {
        if (-not ($roles | Where-Object { -not (Test-Path "$Root\sync\result-$_.txt") })) { break }
        $dead = $script:clients | Where-Object { $_.HasExited } | Select-Object -First 1
        if ($dead) { Write-Step "Cliente $($dead.Id) caiu (codigo $($dead.ExitCode)); encerrando"; break }
        Start-Sleep -Seconds 5
    }

    # ----------------------------------------------------------------------- resultado
    foreach ($role in $roles) {
        $log = "$Root\client$role\BepInEx\LogOutput.log"
        Write-Host ""
        Write-Host "===== Cliente $role ====="
        if (Test-Path $log) {
            Select-String -Path $log -Pattern '\[PVPTEST\]' | ForEach-Object { $_.Line -replace '^.*\[PVPTEST\] ', '' }
        } else { Write-Host "(sem log em $log)" }
    }
    Write-Host ""
    Write-Host "===== Servidor (PvP e Ajustes) ====="
    Select-String -Path $serverLog -Pattern 'Deadheim PvP|Deadheim\] Ajustes|Exception' | ForEach-Object { $_.Line }
    if (Test-Path "$Root\fotos") {
        Write-Host ""
        Write-Host "Fotos do passo ajustes em $Root\fotos"
    }
}
finally {
    if (-not $KeepRunning) { Stop-All }
    if (Test-Path $prefsBackup) {
        & reg delete "HKCU\Software\IronGate\valheim" /f | Out-Null
        # Via cmd: o reg escreve o "concluida com exito" no stderr, e o PowerShell 5.1
        # transforma isso em erro e o script saia com codigo 1 mesmo com tudo certo.
        & cmd /c "reg import `"$prefsBackup`" >nul 2>&1"
        Write-Step 'Preferencias do Valheim restauradas'
    }
}

# Codigo de saida = resultado do roteiro: 0 so se todo cliente terminou sem FAIL.
$results = @($roles | ForEach-Object { Get-Content "$Root\sync\result-$_.txt" -ErrorAction SilentlyContinue })
if ($results.Count -eq $roles.Count -and -not ($results | Where-Object { $_ -notmatch 'fail=0$' })) { exit 0 }
exit 1
