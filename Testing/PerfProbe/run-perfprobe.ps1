<#
.SYNOPSIS
    Abre o Valheim com os mods do perfil do launcher mais o PerfProbe, para medir quanto
    cada mod custa por frame.

.DESCRIPTION
    Copia bin\Release\PerfProbe.dll para plugins\perfprobe do perfil e abre o valheim.exe
    com o BepInEx do perfil, como o launcher faz, mas SEM conectar no servidor: o
    AzuAntiCheat do servidor recusaria a DLL, que nao esta na whitelist. Entre num mundo
    local, fique parado no spawn uns 60 s e feche o jogo. A cada 20 s o PerfProbe grava
    um relatorio no LogOutput.log do perfil; -Relatorio mostra os relatorios.

.EXAMPLE
    .\run-perfprobe.ps1               # instala e abre o jogo
    .\run-perfprobe.ps1 -Relatorio    # depois de jogar: mostra as medicoes
    .\run-perfprobe.ps1 -Remover      # tira o PerfProbe do perfil
#>
param(
    [string]$Perfil = 'Default',
    [switch]$IncluirAzu,
    [switch]$Relatorio,
    [switch]$Remover
)

$ErrorActionPreference = 'Stop'
$game = Join-Path $env:APPDATA "DeadheimLauncher\profiles\$Perfil\game"
$bepinex = Join-Path $game 'BepInEx'
$destino = Join-Path $bepinex 'plugins\perfprobe'
$cfg = Join-Path $bepinex 'config\Detalhes.PerfProbe.cfg'
$log = Join-Path $bepinex 'LogOutput.log'

if ($Remover) {
    Remove-Item $destino -Recurse -Force -ErrorAction SilentlyContinue
    Remove-Item $cfg -Force -ErrorAction SilentlyContinue
    Write-Host "PerfProbe removido do perfil $Perfil."
    return
}

if ($Relatorio) {
    if (-not (Test-Path $log)) { throw "Sem log em $log." }
    Select-String -Path $log -Pattern 'PerfProbe' -Context 0, 27 | ForEach-Object {
        $_.Line
        $_.Context.PostContext | Where-Object { $_ -match '^\s{3}' }
        ''
    }
    return
}

$dll = Join-Path $PSScriptRoot 'bin\Release\PerfProbe.dll'
if (-not (Test-Path $dll)) {
    throw "Compile antes: MSBuild Testing\PerfProbe\PerfProbe.csproj /p:Configuration=Release"
}
if (-not (Test-Path (Join-Path $bepinex 'core\BepInEx.Preloader.dll'))) {
    throw "O perfil $Perfil nao tem BepInEx. Abra o jogo uma vez pelo launcher."
}

New-Item -ItemType Directory -Force $destino | Out-Null
Copy-Item $dll $destino -Force
Set-Content -Path $cfg -Encoding UTF8 -Value @(
    '[Probe]',
    '',
    "IncludeAzuAntiCheat = $(if ($IncluirAzu) { 'true' } else { 'false' })"
)

$settings = Get-Content (Join-Path $env:APPDATA 'DeadheimLauncher\settings.json') -Raw | ConvertFrom-Json
$valheim = $settings.ValheimPath
if (-not $valheim) { $valheim = 'C:\Program Files (x86)\Steam\steamapps\common\Valheim' }
if (-not (Test-Path (Join-Path $valheim 'winhttp.dll'))) {
    throw "Sem winhttp.dll em $valheim. Abra o jogo uma vez pelo launcher."
}

$preloader = Join-Path $bepinex 'core\BepInEx.Preloader.dll'
Start-Process -FilePath (Join-Path $valheim 'valheim.exe') -WorkingDirectory $valheim `
    -ArgumentList "--doorstop-enabled true --doorstop-target-assembly `"$preloader`""
Write-Host "Valheim aberto com o PerfProbe. Entre num mundo local, fique no spawn ~60 s e feche."
Write-Host "Depois: .\run-perfprobe.ps1 -Relatorio"
