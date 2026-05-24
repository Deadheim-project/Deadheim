Add-Type -AssemblyName System.Windows.Forms
Add-Type -AssemblyName System.Drawing

$ErrorActionPreference = "Stop"

$scriptDir = Split-Path -Parent $MyInvocation.MyCommand.Path
$installerScript = Join-Path $scriptDir "Install-Deadheim.ps1"
$manifestPath = Join-Path $scriptDir "deadheim-installer.json"

function Get-DefaultValheimPath {
    $candidates = @(
        "C:\Program Files (x86)\Steam\steamapps\common\Valheim",
        "C:\Program Files\Steam\steamapps\common\Valheim"
    )

    foreach ($candidate in $candidates) {
        if (Test-Path (Join-Path $candidate "valheim.exe")) {
            return $candidate
        }
    }

    return ""
}

function New-Label($Text, $X, $Y, $Width, $Height) {
    $label = New-Object System.Windows.Forms.Label
    $label.Text = $Text
    $label.Location = New-Object System.Drawing.Point($X, $Y)
    $label.Size = New-Object System.Drawing.Size($Width, $Height)
    return $label
}

function New-TextBox($X, $Y, $Width, $Text) {
    $box = New-Object System.Windows.Forms.TextBox
    $box.Location = New-Object System.Drawing.Point($X, $Y)
    $box.Size = New-Object System.Drawing.Size($Width, 24)
    $box.Text = $Text
    return $box
}

function Append-Log($Text) {
    $logBox.AppendText($Text + [Environment]::NewLine)
    $logBox.SelectionStart = $logBox.TextLength
    $logBox.ScrollToCaret()
}

if (-not (Test-Path $installerScript)) {
    [System.Windows.Forms.MessageBox]::Show("Install-Deadheim.ps1 nao foi encontrado na mesma pasta.", "Deadheim Installer", "OK", "Error") | Out-Null
    exit 1
}

if (-not (Test-Path $manifestPath)) {
    [System.Windows.Forms.MessageBox]::Show("deadheim-installer.json nao foi encontrado na mesma pasta.", "Deadheim Installer", "OK", "Error") | Out-Null
    exit 1
}

$manifest = Get-Content -LiteralPath $manifestPath -Raw | ConvertFrom-Json

$form = New-Object System.Windows.Forms.Form
$form.Text = "Deadheim Installer"
$form.StartPosition = "CenterScreen"
$form.Size = New-Object System.Drawing.Size(760, 560)
$form.MinimumSize = New-Object System.Drawing.Size(760, 560)
$form.Font = New-Object System.Drawing.Font("Segoe UI", 9)

$title = New-Object System.Windows.Forms.Label
$title.Text = "Deadheim $($manifest.version)"
$title.Font = New-Object System.Drawing.Font("Segoe UI", 16, [System.Drawing.FontStyle]::Bold)
$title.Location = New-Object System.Drawing.Point(20, 18)
$title.Size = New-Object System.Drawing.Size(500, 34)
$form.Controls.Add($title)

$subtitle = New-Label "Instala BepInEx, dependencias e o pacote Deadheim no Valheim." 20 56 680 22
$form.Controls.Add($subtitle)

$gameLabel = New-Label "Pasta do Valheim" 20 94 200 20
$form.Controls.Add($gameLabel)

$gamePathBox = New-TextBox 20 118 610 (Get-DefaultValheimPath)
$form.Controls.Add($gamePathBox)

$browseButton = New-Object System.Windows.Forms.Button
$browseButton.Text = "Procurar"
$browseButton.Location = New-Object System.Drawing.Point(640, 116)
$browseButton.Size = New-Object System.Drawing.Size(80, 28)
$browseButton.Add_Click({
    $dialog = New-Object System.Windows.Forms.FolderBrowserDialog
    $dialog.Description = "Selecione a pasta onde o Valheim esta instalado"
    if ($gamePathBox.Text -and (Test-Path $gamePathBox.Text)) {
        $dialog.SelectedPath = $gamePathBox.Text
    }

    if ($dialog.ShowDialog() -eq [System.Windows.Forms.DialogResult]::OK) {
        $gamePathBox.Text = $dialog.SelectedPath
    }
})
$form.Controls.Add($browseButton)

$zipLabel = New-Label "Link do pacote proprio Deadheim" 20 158 260 20
$form.Controls.Add($zipLabel)

$ownUrl = ""
if ($manifest.ownPackage -and $manifest.ownPackage.url) {
    $ownUrl = $manifest.ownPackage.url
}

$zipUrlBox = New-TextBox 20 182 700 $ownUrl
$form.Controls.Add($zipUrlBox)

$backupCheck = New-Object System.Windows.Forms.CheckBox
$backupCheck.Text = "Criar backup da pasta BepInEx atual antes de instalar"
$backupCheck.Checked = $true
$backupCheck.Location = New-Object System.Drawing.Point(20, 218)
$backupCheck.Size = New-Object System.Drawing.Size(430, 24)
$form.Controls.Add($backupCheck)

$depsCheck = New-Object System.Windows.Forms.CheckBox
$depsCheck.Text = "Instalar dependencias oficiais"
$depsCheck.Checked = $true
$depsCheck.Location = New-Object System.Drawing.Point(20, 246)
$depsCheck.Size = New-Object System.Drawing.Size(300, 24)
$form.Controls.Add($depsCheck)

$installButton = New-Object System.Windows.Forms.Button
$installButton.Text = "Instalar"
$installButton.Font = New-Object System.Drawing.Font("Segoe UI", 10, [System.Drawing.FontStyle]::Bold)
$installButton.Location = New-Object System.Drawing.Point(20, 286)
$installButton.Size = New-Object System.Drawing.Size(120, 34)
$form.Controls.Add($installButton)

$statusLabel = New-Label "Pronto para instalar." 156 294 560 24
$form.Controls.Add($statusLabel)

$progress = New-Object System.Windows.Forms.ProgressBar
$progress.Location = New-Object System.Drawing.Point(20, 334)
$progress.Size = New-Object System.Drawing.Size(700, 18)
$progress.Style = "Continuous"
$progress.Minimum = 0
$progress.Maximum = 100
$progress.Value = 0
$form.Controls.Add($progress)

$logBox = New-Object System.Windows.Forms.TextBox
$logBox.Location = New-Object System.Drawing.Point(20, 370)
$logBox.Size = New-Object System.Drawing.Size(700, 130)
$logBox.Multiline = $true
$logBox.ReadOnly = $true
$logBox.ScrollBars = "Vertical"
$logBox.Font = New-Object System.Drawing.Font("Consolas", 9)
$form.Controls.Add($logBox)

$state = @{
    Process = $null
    LogPath = $null
    LastLength = 0
}

$timer = New-Object System.Windows.Forms.Timer
$timer.Interval = 700
$timer.Add_Tick({
    if (-not $state.Process) {
        return
    }

    if ($state.LogPath -and (Test-Path $state.LogPath)) {
        $content = Get-Content -LiteralPath $state.LogPath -Raw -ErrorAction SilentlyContinue
        if ($content.Length -gt $state.LastLength) {
            $newText = $content.Substring($state.LastLength)
            $state.LastLength = $content.Length
            $newText -split "\r?\n" | Where-Object { $_ } | ForEach-Object { Append-Log $_ }
        }
    }

    if ($progress.Value -lt 95) {
        $progress.Value = [Math]::Min(95, $progress.Value + 1)
    }

    if ($state.Process.HasExited) {
        $timer.Stop()
        $exitCode = $state.Process.ExitCode
        $state.Process.Dispose()
        $state.Process = $null
        $installButton.Enabled = $true

        if ($exitCode -eq 0) {
            $progress.Value = 100
            $statusLabel.Text = "Instalacao concluida."
            [System.Windows.Forms.MessageBox]::Show("Instalacao concluida. Abra o Valheim e confira se tudo carregou.", "Deadheim Installer", "OK", "Information") | Out-Null
        } else {
            $statusLabel.Text = "Instalacao falhou. Veja o log."
            [System.Windows.Forms.MessageBox]::Show("A instalacao falhou. Veja o log na janela do instalador.", "Deadheim Installer", "OK", "Error") | Out-Null
        }
    }
})

$installButton.Add_Click({
    try {
        $gamePath = $gamePathBox.Text.Trim()
        if ([string]::IsNullOrWhiteSpace($gamePath) -or -not (Test-Path (Join-Path $gamePath "valheim.exe"))) {
            [System.Windows.Forms.MessageBox]::Show("Selecione uma pasta valida do Valheim.", "Deadheim Installer", "OK", "Warning") | Out-Null
            return
        }

        $zipUrl = $zipUrlBox.Text.Trim()
        if ([string]::IsNullOrWhiteSpace($zipUrl)) {
            $answer = [System.Windows.Forms.MessageBox]::Show(
                "Nenhum link do pacote proprio Deadheim foi informado. Continuar instalando apenas as dependencias oficiais?",
                "Deadheim Installer",
                "YesNo",
                "Question"
            )

            if ($answer -ne [System.Windows.Forms.DialogResult]::Yes) {
                return
            }
        }

        $logBox.Clear()
        $progress.Value = 3
        $statusLabel.Text = "Instalando..."
        $installButton.Enabled = $false

        $logPath = Join-Path $env:TEMP ("deadheim-installer-" + [Guid]::NewGuid().ToString("N") + ".log")
        $state.LogPath = $logPath
        $state.LastLength = 0

        $arguments = @(
            "-NoProfile",
            "-ExecutionPolicy", "Bypass",
            "-File", "`"$installerScript`"",
            "-ManifestPath", "`"$manifestPath`"",
            "-GamePath", "`"$gamePath`""
        )

        if (-not [string]::IsNullOrWhiteSpace($zipUrl)) {
            $arguments += @("-DeadheimZipUrl", "`"$zipUrl`"")
        }

        if (-not $backupCheck.Checked) {
            $arguments += "-NoBackup"
        }

        if (-not $depsCheck.Checked) {
            $arguments += "-SkipOfficialDependencies"
        }

        $process = New-Object System.Diagnostics.Process
        $process.StartInfo.FileName = "powershell.exe"
        $process.StartInfo.Arguments = ($arguments -join " ")
        $process.StartInfo.UseShellExecute = $false
        $process.StartInfo.RedirectStandardOutput = $true
        $process.StartInfo.RedirectStandardError = $true
        $process.StartInfo.CreateNoWindow = $true
        $process.StartInfo.WindowStyle = [System.Diagnostics.ProcessWindowStyle]::Hidden

        Register-ObjectEvent -InputObject $process -EventName OutputDataReceived -Action {
            if ($EventArgs.Data) {
                Add-Content -LiteralPath $Event.MessageData -Value $EventArgs.Data
            }
        } -MessageData $logPath | Out-Null

        Register-ObjectEvent -InputObject $process -EventName ErrorDataReceived -Action {
            if ($EventArgs.Data) {
                Add-Content -LiteralPath $Event.MessageData -Value $EventArgs.Data
            }
        } -MessageData $logPath | Out-Null

        [void]$process.Start()
        $process.BeginOutputReadLine()
        $process.BeginErrorReadLine()
        $state.Process = $process

        Append-Log "Instalador iniciado."
        $timer.Start()
    } catch {
        $installButton.Enabled = $true
        $statusLabel.Text = "Erro antes de iniciar."
        [System.Windows.Forms.MessageBox]::Show($_.Exception.Message, "Deadheim Installer", "OK", "Error") | Out-Null
    }
})

$form.Add_FormClosing({
    if ($state.Process -and -not $state.Process.HasExited) {
        $answer = [System.Windows.Forms.MessageBox]::Show(
            "A instalacao ainda esta rodando. Deseja cancelar?",
            "Deadheim Installer",
            "YesNo",
            "Warning"
        )

        if ($answer -ne [System.Windows.Forms.DialogResult]::Yes) {
            $_.Cancel = $true
            return
        }

        try {
            $state.Process.Kill()
        } catch {
        }
    }
})

[void]$form.ShowDialog()
