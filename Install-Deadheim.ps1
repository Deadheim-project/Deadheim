param(
    [string]$GamePath,
    [string]$ManifestPath = (Join-Path $PSScriptRoot "deadheim-installer.json"),
    [string]$DeadheimZipUrl,
    [switch]$NoBackup,
    [switch]$SkipOfficialDependencies
)

$ErrorActionPreference = "Stop"

function Write-Step($Message) {
    Write-Host ""
    Write-Host "==> $Message" -ForegroundColor Cyan
}

function Get-ValheimPath {
    $candidates = @(
        "C:\Program Files (x86)\Steam\steamapps\common\Valheim",
        "C:\Program Files\Steam\steamapps\common\Valheim"
    )

    foreach ($candidate in $candidates) {
        if (Test-Path (Join-Path $candidate "valheim.exe")) {
            return $candidate
        }
    }

    throw "Nao achei o Valheim automaticamente. Rode com -GamePath `"C:\...\Valheim`"."
}

function New-Directory($Path) {
    if (-not (Test-Path $Path)) {
        New-Item -ItemType Directory -Path $Path | Out-Null
    }
}

function Copy-DirectoryContents($Source, $Destination) {
    if (-not (Test-Path $Source)) {
        return
    }

    New-Directory $Destination
    Get-ChildItem -LiteralPath $Source -Force | ForEach-Object {
        Copy-Item -LiteralPath $_.FullName -Destination $Destination -Recurse -Force
    }
}

function Save-Url($Url, $Destination) {
    Write-Host "Baixando: $Url"
    Invoke-WebRequest -Uri $Url -OutFile $Destination -UseBasicParsing
}

function Expand-Zip($ZipPath, $Destination) {
    if (Test-Path $Destination) {
        Remove-Item -LiteralPath $Destination -Recurse -Force
    }

    New-Directory $Destination
    Expand-Archive -LiteralPath $ZipPath -DestinationPath $Destination -Force
}

function Install-BepInExPack($ExtractedPath, $GamePath) {
    $packRoot = Get-ChildItem -LiteralPath $ExtractedPath -Recurse -Directory |
        Where-Object {
            (Test-Path (Join-Path $_.FullName "BepInEx")) -and
            (Test-Path (Join-Path $_.FullName "doorstop_config.ini"))
        } |
        Select-Object -First 1

    if (-not $packRoot) {
        $packRoot = Get-Item -LiteralPath $ExtractedPath
    }

    Copy-DirectoryContents $packRoot.FullName $GamePath
}

function Install-GenericPackage($ExtractedPath, $GamePath, $PluginFolderName) {
    $bepInExPath = Join-Path $GamePath "BepInEx"
    $pluginsPath = Join-Path $bepInExPath "plugins"
    $configPath = Join-Path $bepInExPath "config"
    $patchersPath = Join-Path $bepInExPath "patchers"

    New-Directory $pluginsPath
    New-Directory $configPath

    Copy-DirectoryContents (Join-Path $ExtractedPath "BepInEx") $bepInExPath
    Copy-DirectoryContents (Join-Path $ExtractedPath "plugins") $pluginsPath
    Copy-DirectoryContents (Join-Path $ExtractedPath "config") $configPath
    Copy-DirectoryContents (Join-Path $ExtractedPath "patchers") $patchersPath

    $rootDlls = Get-ChildItem -LiteralPath $ExtractedPath -File -Filter "*.dll" -ErrorAction SilentlyContinue
    if ($rootDlls.Count -gt 0) {
        $target = Join-Path $pluginsPath $PluginFolderName
        New-Directory $target
        foreach ($dll in $rootDlls) {
            Copy-Item -LiteralPath $dll.FullName -Destination $target -Force
        }
    }
}

function Install-PackageZip($ZipPath, $GamePath, $PluginFolderName, [switch]$BepInExPack) {
    $extractPath = Join-Path ([IO.Path]::GetTempPath()) ("deadheim_extract_" + [Guid]::NewGuid().ToString("N"))
    Expand-Zip $ZipPath $extractPath

    try {
        if ($BepInExPack) {
            Install-BepInExPack $extractPath $GamePath
        } else {
            Install-GenericPackage $extractPath $GamePath $PluginFolderName
        }
    } finally {
        Remove-Item -LiteralPath $extractPath -Recurse -Force -ErrorAction SilentlyContinue
    }
}

if (-not (Test-Path $ManifestPath)) {
    throw "Manifest nao encontrado: $ManifestPath"
}

$manifest = Get-Content -LiteralPath $ManifestPath -Raw | ConvertFrom-Json

if ([string]::IsNullOrWhiteSpace($GamePath)) {
    $GamePath = Get-ValheimPath
}

if (-not (Test-Path (Join-Path $GamePath "valheim.exe"))) {
    throw "Pasta invalida do Valheim: $GamePath"
}

$bepInExPath = Join-Path $GamePath "BepInEx"
$downloadPath = Join-Path $env:TEMP ("deadheim_downloads_" + [Guid]::NewGuid().ToString("N"))
New-Directory $downloadPath

Write-Step "Instalando $($manifest.name) $($manifest.version) em $GamePath"

if (-not $NoBackup -and (Test-Path $bepInExPath)) {
    $stamp = Get-Date -Format "yyyyMMdd-HHmmss"
    $backupPath = Join-Path $GamePath "BepInEx.backup.$stamp"
    Write-Step "Criando backup: $backupPath"
    Copy-Item -LiteralPath $bepInExPath -Destination $backupPath -Recurse -Force
}

try {
    if (-not $SkipOfficialDependencies) {
        Write-Step "Baixando e instalando dependencias oficiais"
        foreach ($dep in $manifest.dependencies) {
            if ($dep.source -ne "thunderstore") {
                throw "Fonte nao suportada no momento: $($dep.source)"
            }

            $url = "https://thunderstore.io/package/download/$($dep.namespace)/$($dep.name)/$($dep.version)/"
            $zipPath = Join-Path $downloadPath "$($dep.namespace)-$($dep.name)-$($dep.version).zip"
            $folderName = "$($dep.namespace)-$($dep.name)"

            Write-Host "Instalando $folderName $($dep.version)"
            Save-Url $url $zipPath
            Install-PackageZip $zipPath $GamePath $folderName -BepInExPack:($dep.name -eq "BepInExPack_Valheim")
        }
    }

    $ownUrl = $DeadheimZipUrl
    if ([string]::IsNullOrWhiteSpace($ownUrl)) {
        $ownUrl = $manifest.ownPackage.url
    }

    if ([string]::IsNullOrWhiteSpace($ownUrl)) {
        Write-Host ""
        Write-Warning "Pulei o pacote proprio Deadheim porque nenhum link foi informado. Use -DeadheimZipUrl ou preencha ownPackage.url no JSON."
    } else {
        Write-Step "Baixando e instalando pacote proprio Deadheim"
        $ownZip = Join-Path $downloadPath "$($manifest.ownPackage.name)-$($manifest.ownPackage.version).zip"
        Save-Url $ownUrl $ownZip
        Install-PackageZip $ownZip $GamePath $manifest.ownPackage.pluginFolder
    }

    Write-Step "Concluido"
    Write-Host "Abra o Valheim uma vez e confira BepInEx\LogOutput.log se algum plugin falhar."
} finally {
    Remove-Item -LiteralPath $downloadPath -Recurse -Force -ErrorAction SilentlyContinue
}
