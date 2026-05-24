param(
    [string]$OutputPath = (Join-Path $PSScriptRoot "DeadheimInstaller.zip"),
    [string]$DeadheimZipUrl
)

$ErrorActionPreference = "Stop"

$packageRoot = Join-Path $env:TEMP ("DeadheimInstaller_" + [Guid]::NewGuid().ToString("N"))
New-Item -ItemType Directory -Path $packageRoot | Out-Null

try {
    $files = @(
        "Install-Deadheim.vbs",
        "Install-Deadheim-GUI.ps1",
        "Install-Deadheim.ps1",
        "deadheim-installer.json"
    )

    foreach ($file in $files) {
        Copy-Item -LiteralPath (Join-Path $PSScriptRoot $file) -Destination $packageRoot -Force
    }

    if (-not [string]::IsNullOrWhiteSpace($DeadheimZipUrl)) {
        $manifestPath = Join-Path $packageRoot "deadheim-installer.json"
        $manifest = Get-Content -LiteralPath $manifestPath -Raw | ConvertFrom-Json
        $manifest.ownPackage.url = $DeadheimZipUrl
        $manifest | ConvertTo-Json -Depth 10 | Set-Content -LiteralPath $manifestPath -Encoding UTF8
    }

    $readme = @"
Deadheim Installer

1. Extraia este ZIP em uma pasta.
2. Dê dois cliques em Install-Deadheim.vbs.
3. Confirme a pasta do Valheim.
4. Clique em Instalar.

O instalador cria backup da pasta BepInEx atual antes de alterar os arquivos.
"@

    Set-Content -LiteralPath (Join-Path $packageRoot "LEIA-ME.txt") -Value $readme -Encoding UTF8

    if (Test-Path $OutputPath) {
        Remove-Item -LiteralPath $OutputPath -Force
    }

    Compress-Archive -LiteralPath (Join-Path $packageRoot "*") -DestinationPath $OutputPath -Force
    Write-Host "Pacote criado: $OutputPath"
} finally {
    Remove-Item -LiteralPath $packageRoot -Recurse -Force -ErrorAction SilentlyContinue
}
