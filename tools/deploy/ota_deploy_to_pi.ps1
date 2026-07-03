# Deploy do jukebox-ota-agent: Windows → Raspberry Pi (artefatos + systemd + install).
# Pré-requisito: SSH sem senha para ${PiUser}@${PiHost} (chave no WSL se usar rsync).
#
# Uso:
#   .\tools\deploy\ota_deploy_to_pi.ps1
#   .\tools\deploy\ota_deploy_to_pi.ps1 -PiHost 192.168.15.100 -EnableTimer
#   .\tools\deploy\ota_deploy_to_pi.ps1 -SkipInstall          # só envia staging
#   .\tools\deploy\ota_deploy_to_pi.ps1 -SkipPublish          # usa artifacts existentes (sem rebuild)

param(
    [string]$PiHost = "192.168.15.100",

    [string]$PiUser = "jukebox",

    [string]$RemotePath = "/opt/jukeeo/ota-agent",

    [string]$RemoteStaging = "/tmp/jukebox-ota-staging",

    [switch]$SkipPublish,

    [switch]$SkipInstall,

    [switch]$EnableTimer,

    [switch]$ForceConfig
)

$ErrorActionPreference = "Stop"
$DeployDir = $PSScriptRoot
$Root = Split-Path (Split-Path $DeployDir -Parent) -Parent
$ArtifactsDir = Join-Path $Root "artifacts\linux-arm64"
$StagingDir = Join-Path $Root "artifacts\pi-deploy-staging"
$PublishScript = Join-Path $DeployDir "publish-linux-arm64.ps1"
$RsyncScript = Join-Path $DeployDir "deploy_to_pi_rsync.sh"
$InstallScript = Join-Path $DeployDir "pi_install_ota.sh"
$BinaryName = "jukebox-ota-agent"

function Set-UnixLineEndings {
    param([string[]]$Paths)

    foreach ($path in $Paths) {
        if (-not (Test-Path $path)) { continue }
        $text = [System.IO.File]::ReadAllText($path)
        $normalized = $text -replace "`r`n", "`n" -replace "`r", "`n"
        if ($normalized -ne $text) {
            $utf8NoBom = New-Object System.Text.UTF8Encoding $false
            [System.IO.File]::WriteAllText($path, $normalized, $utf8NoBom)
        }
    }
}

function ToWslPath {
    param([string]$WinPath)
    if (-not $WinPath) { return "" }
    $q = (Split-Path -Path $WinPath -Qualifier).TrimEnd(':')
    $nq = Split-Path -Path $WinPath -NoQualifier
    return "/mnt/$($q.ToLower())$($nq -replace '\\', '/')"
}

function Test-WslAvailable {
    try {
        $null = Get-Command wsl -ErrorAction Stop
        wsl -e true 2>$null
        return $LASTEXITCODE -eq 0
    } catch {
        return $false
    }
}

function Invoke-ScpRecursive {
    param(
        [string]$LocalPath,
        [string]$RemoteDest
    )
    $scp = Get-Command scp -ErrorAction SilentlyContinue
    if (-not $scp) {
        throw "scp não encontrado. Instale OpenSSH Client ou use WSL com rsync."
    }
    & scp -r $LocalPath "${PiUser}@${PiHost}:$RemoteDest"
    if ($LASTEXITCODE -ne 0) {
        throw "scp falhou (código $LASTEXITCODE)"
    }
}

Write-Host "============================================" -ForegroundColor Cyan
Write-Host "  Deploy OTA Agent → Raspberry Pi" -ForegroundColor Cyan
Write-Host "============================================" -ForegroundColor Cyan
Write-Host ""
Write-Host "Projeto:  $Root"
Write-Host "Destino:  ${PiUser}@${PiHost}:$RemotePath"
Write-Host "Staging:  ${PiUser}@${PiHost}:$RemoteStaging"
Write-Host ""

# --- Publish (rebuild antes do deploy) ---
$binaryPath = Join-Path $ArtifactsDir $BinaryName
if (-not $SkipPublish) {
    Write-Host "Executando publish (linux-arm64)..." -ForegroundColor Yellow
    & $PublishScript
    if ($LASTEXITCODE -ne 0) {
        Write-Host "ERRO: publish falhou." -ForegroundColor Red
        exit 1
    }
    if (-not (Test-Path $binaryPath)) {
        Write-Host "ERRO: publish concluiu mas $BinaryName não foi gerado." -ForegroundColor Red
        exit 1
    }
} elseif (-not (Test-Path $binaryPath)) {
    Write-Host "ERRO: $binaryPath não existe. Execute:" -ForegroundColor Red
    Write-Host "  .\tools\deploy\publish-linux-arm64.ps1" -ForegroundColor Gray
    exit 1
}

# --- Montar staging local ---
Write-Host "Montando staging local em $StagingDir ..." -ForegroundColor Gray

if (Test-Path $StagingDir) {
    Remove-Item -Recurse -Force $StagingDir
}
New-Item -ItemType Directory -Path $StagingDir -Force | Out-Null

$stagingArtifacts = Join-Path $StagingDir "artifacts"
$stagingSystemd = Join-Path $StagingDir "systemd"
$stagingConfig = Join-Path $StagingDir "config"
$stagingSudoers = Join-Path $StagingDir "sudoers"

New-Item -ItemType Directory -Path $stagingArtifacts -Force | Out-Null
New-Item -ItemType Directory -Path $stagingSystemd -Force | Out-Null
New-Item -ItemType Directory -Path $stagingSudoers -Force | Out-Null

Copy-Item -Recurse -Force "$ArtifactsDir\*" $stagingArtifacts
Copy-Item -Force (Join-Path $Root "packaging\systemd\jukebox_ota_agent.service") $stagingSystemd
Copy-Item -Force (Join-Path $Root "packaging\systemd\jukebox_ota_agent.timer") $stagingSystemd
New-Item -ItemType Directory -Path $stagingConfig -Force | Out-Null
Copy-Item -Force (Join-Path $Root "tools\mock\ota-agent.example.json") (Join-Path $stagingConfig "ota-agent.example.json")
Copy-Item -Force (Join-Path $Root "packaging\sudoers\jukebox-ota-systemctl.template") (Join-Path $stagingSudoers "jukebox-ota-systemctl.template")
Copy-Item -Force (Join-Path $Root "packaging\sudoers\jukebox-kiosk-ota-check.template") (Join-Path $stagingSudoers "jukebox-kiosk-ota-check.template")
Copy-Item -Force $InstallScript (Join-Path $StagingDir "pi_install_ota.sh")

Set-UnixLineEndings @(
    $RsyncScript
    $InstallScript
    (Join-Path $StagingDir "pi_install_ota.sh")
    (Join-Path $stagingSudoers "jukebox-ota-systemctl.template")
    (Join-Path $stagingSudoers "jukebox-kiosk-ota-check.template")
    (Join-Path $stagingSystemd "jukebox_ota_agent.service")
    (Join-Path $stagingSystemd "jukebox_ota_agent.timer")
)

# --- Enviar para o Pi ---
$useWsl = Test-WslAvailable
$transferOk = $false

if ($useWsl) {
    Write-Host "Transferindo via WSL + rsync..." -ForegroundColor Cyan
    $wslStaging = ToWslPath $StagingDir
    $wslRsyncScript = ToWslPath $RsyncScript
    $bashCmd = "bash '$wslRsyncScript' '$PiHost' '$PiUser' '$wslStaging' '$RemoteStaging'"
    wsl -e bash -c $bashCmd
    if ($LASTEXITCODE -eq 0) {
        $transferOk = $true
    } else {
        Write-Host "AVISO: rsync via WSL falhou — tentando scp nativo..." -ForegroundColor Yellow
    }
}

if (-not $transferOk) {
    Write-Host "Transferindo via scp nativo..." -ForegroundColor Cyan
    ssh "${PiUser}@${PiHost}" "rm -rf '$RemoteStaging' && mkdir -p '$RemoteStaging'"
    if ($LASTEXITCODE -ne 0) {
        Write-Host "ERRO: SSH falhou. Verifique chave e conectividade." -ForegroundColor Red
        exit 1
    }
    try {
        Invoke-ScpRecursive -LocalPath "$StagingDir\*" -RemoteDest "$RemoteStaging/"
        $transferOk = $true
    } catch {
        Write-Host "ERRO: $($_.Exception.Message)" -ForegroundColor Red
        Write-Host "Dicas:" -ForegroundColor Gray
        Write-Host "  - WSL: apt install rsync; copiar chave SSH para ~/.ssh no WSL" -ForegroundColor Gray
        Write-Host "  - Windows: instalar OpenSSH Client (scp/ssh)" -ForegroundColor Gray
        exit 1
    }
}

Write-Host "Staging enviado para ${PiUser}@${PiHost}:$RemoteStaging" -ForegroundColor Green

if ($SkipInstall) {
    Write-Host ""
    Write-Host "SkipInstall: execute no Pi:" -ForegroundColor Yellow
    Write-Host "  ssh ${PiUser}@${PiHost}" -ForegroundColor Gray
    Write-Host "  sudo bash $RemoteStaging/pi_install_ota.sh --install-dir $RemotePath" -ForegroundColor Gray
    exit 0
}

# --- Instalação remota ---
Write-Host ""
Write-Host "Executando instalação no Pi (sudo)..." -ForegroundColor Cyan

$installFlags = @("--staging-dir", $RemoteStaging, "--install-dir", $RemotePath)
if ($EnableTimer) { $installFlags += "--enable-timer" }
if ($ForceConfig) { $installFlags += "--force-config" }

$remoteCmd = "chmod +x '$RemoteStaging/pi_install_ota.sh' && sudo bash '$RemoteStaging/pi_install_ota.sh' $($installFlags -join ' ')"
ssh "${PiUser}@${PiHost}" $remoteCmd

if ($LASTEXITCODE -ne 0) {
    Write-Host "ERRO: instalação remota falhou." -ForegroundColor Red
    exit 1
}

Write-Host ""
Write-Host "Deploy concluído." -ForegroundColor Green
Write-Host "Validar: .\tools\deploy\verify_pi_from_windows.ps1 -PiHost $PiHost" -ForegroundColor Gray
