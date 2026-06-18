# Validação orquestrada do agente OTA no Raspberry Pi (executar no Windows).
# Executa checks via SSH e imprime checklist pass/fail.
#
# Uso:
#   .\tools\deploy\verify_pi_from_windows.ps1
#   .\tools\deploy\verify_pi_from_windows.ps1 -PiHost 192.168.15.100
#   .\tools\deploy\verify_pi_from_windows.ps1 -MockBaseUrl "192.168.15.50:8080"

param(
    [string]$PiHost = "192.168.15.100",

    [string]$PiUser = "jukebox",

    [string]$MockBaseUrl = "",

    [string]$InstallDir = "/opt/jukebox/ota-agent",

    [string]$ConfigPath = "/etc/jukebox/ota-agent.json"
)

$ErrorActionPreference = "Continue"
$BinaryPath = "$InstallDir/jukebox-ota-agent"
$OtaUser = "jukebox-ota"
$ServiceUnit = "jukebox_ota_agent.service"
$TimerUnit = "jukebox_ota_agent.timer"

$results = [System.Collections.Generic.List[object]]::new()

function Add-Check {
    param(
        [string]$Name,
        [bool]$Pass,
        [string]$Detail = ""
    )
    $script:results.Add([pscustomobject]@{
        Name   = $Name
        Pass   = $Pass
        Detail = $Detail
    }) | Out-Null
}

function Invoke-Ssh {
    param([string]$Command)
    ssh -o ConnectTimeout=10 -o BatchMode=yes "${PiUser}@${PiHost}" $Command 2>&1
}

function Invoke-SshAsOta {
    param([string]$Command)
    Invoke-Ssh "sudo -n -u ${OtaUser} bash -lc '$($Command -replace "'", "'\''")'"
}

Write-Host "============================================" -ForegroundColor Cyan
Write-Host "  Verificação OTA Agent — Pi" -ForegroundColor Cyan
Write-Host "============================================" -ForegroundColor Cyan
Write-Host "Alvo: ${PiUser}@${PiHost}"
Write-Host ""

# 1. Conectividade SSH
$sshOut = Invoke-Ssh "echo OK"
$sshOk = ($LASTEXITCODE -eq 0) -and ($sshOut -match "OK")
Add-Check -Name "SSH conectividade" -Pass $sshOk -Detail $(if ($sshOk) { "OK" } else { ($sshOut | Out-String).Trim() })

if (-not $sshOk) {
    Write-Host "Falha SSH — demais checks ignorados." -ForegroundColor Red
} else {
    # 2. Utilizador de sistema dedicado
    $otaUserOut = Invoke-Ssh "getent passwd ${OtaUser} >/dev/null && echo EXISTS || echo MISSING"
    $otaUserOk = ($LASTEXITCODE -eq 0) -and ($otaUserOut -match "EXISTS")
    Add-Check -Name "Utilizador de sistema ($OtaUser)" -Pass $otaUserOk -Detail $(if ($otaUserOk) { "presente" } else { "ausente — reexecute pi_install_ota.sh" })

    # 3. Binário existe e é executável pelo utilizador OTA
    $binOut = Invoke-Ssh "test -x '$BinaryPath' && echo EXISTS || echo MISSING"
    $binOk = ($LASTEXITCODE -eq 0) -and ($binOut -match "EXISTS")
    Add-Check -Name "Binário instalado ($BinaryPath)" -Pass $binOk -Detail $(($binOut | Out-String).Trim())

    # 4. version (como jukebox-ota, não root)
    if ($binOk -and $otaUserOk) {
        $verOut = Invoke-SshAsOta "$BinaryPath version"
        $verOk = $LASTEXITCODE -eq 0
        $verDetail = ($verOut | Out-String).Trim()
        Add-Check -Name "jukebox-ota-agent version (user=$OtaUser)" -Pass $verOk -Detail $verDetail
    } else {
        Add-Check -Name "jukebox-ota-agent version (user=$OtaUser)" -Pass $false -Detail "binário ou utilizador OTA ausente"
    }

    # 5. Config existe e é legível pelo utilizador OTA
    $cfgOut = Invoke-Ssh "sudo -n -u ${OtaUser} test -r '$ConfigPath' && echo EXISTS || echo MISSING"
    $cfgExists = ($LASTEXITCODE -eq 0) -and ($cfgOut -match "EXISTS")
    Add-Check -Name "Config ($ConfigPath)" -Pass $cfgExists -Detail $(if ($cfgExists) { "legível por $OtaUser" } else { "ausente ou sem permissão para $OtaUser" })

    # 6. check --config (como jukebox-ota)
    if ($cfgExists -and $binOk -and $otaUserOk) {
        $checkOut = Invoke-SshAsOta "$BinaryPath check --config '$ConfigPath'"
        $checkOk = $LASTEXITCODE -eq 0
        $checkDetail = ($checkOut | Out-String).Trim()
        if ($checkDetail.Length -gt 200) {
            $checkDetail = $checkDetail.Substring(0, 200) + "..."
        }
        Add-Check -Name "check --config" -Pass $checkOk -Detail $checkDetail
    } else {
        Add-Check -Name "check --config" -Pass $false -Detail "ignorado (config ou binário ausente)"
    }

    # 7. MockBaseUrl opcional — check com config temporária apontando para o PC
    if ($MockBaseUrl -and $binOk -and $otaUserOk) {
        $mockUrl = $MockBaseUrl.Trim()
        if ($mockUrl -notmatch "^https?://") {
            $mockUrl = "http://$mockUrl"
        }
        $mockUrlEscaped = $mockUrl -replace "'", "'\''"
        $mockCmd = @"
cat > /tmp/ota-agent-mock-verify.json <<'EOCFG'
{
  "device_id": "verify-pi",
  "channel": "beta",
  "ota_base_url": "$mockUrl",
  "current_version": "0.0.0",
  "public_key_path": "/etc/jukebox/ota-public-key.pem"
}
EOCFG
chmod 644 /tmp/ota-agent-mock-verify.json
sudo -n -u ${OtaUser} '$BinaryPath' check --config /tmp/ota-agent-mock-verify.json; ec=`$?; rm -f /tmp/ota-agent-mock-verify.json; exit `$ec
"@
        $mockOut = Invoke-Ssh $mockCmd
        $mockOk = $LASTEXITCODE -eq 0
        $mockDetail = ($mockOut | Out-String).Trim()
        if ($mockDetail.Length -gt 200) {
            $mockDetail = $mockDetail.Substring(0, 200) + "..."
        }
        Add-Check -Name "check contra mock ($mockUrl)" -Pass $mockOk -Detail $mockDetail
    }

    # 8. systemd units e utilizador da service
    $svcOut = Invoke-Ssh "systemctl cat '$ServiceUnit' 2>/dev/null | head -1"
    $svcOk = $LASTEXITCODE -eq 0
    Add-Check -Name "Unit systemd ($ServiceUnit)" -Pass $svcOk -Detail $(if ($svcOk) { "instalada" } else { "não encontrada" })

    if ($svcOk) {
        $svcUserOut = Invoke-Ssh "systemctl show -p User --value '$ServiceUnit' 2>/dev/null"
        $svcUser = ($svcUserOut | Out-String).Trim()
        $svcUserOk = $svcUser -eq $OtaUser
        Add-Check -Name "Service User=$OtaUser" -Pass $svcUserOk -Detail $(if ($svcUserOk) { "User=$svcUser" } else { "User=$svcUser (esperado: $OtaUser)" })
    } else {
        Add-Check -Name "Service User=$OtaUser" -Pass $false -Detail "unit não instalada"
    }

    $timerOut = Invoke-Ssh "systemctl is-enabled '$TimerUnit' 2>/dev/null || echo disabled"
    $timerEnabled = ($timerOut | Out-String).Trim() -match "enabled"
    Add-Check -Name "Timer ($TimerUnit)" -Pass $true -Detail $(if ($timerEnabled) { "habilitado" } else { "não habilitado (esperado na POC sem --enable-timer)" })

    # 9. journalctl (se service instalada)
    if ($svcOk) {
        $journalOut = Invoke-Ssh "journalctl -t jukebox-ota -n 20 --no-pager 2>/dev/null || echo '(sem entradas)'"
        $journalOk = $LASTEXITCODE -eq 0
        $journalDetail = ($journalOut | Out-String).Trim()
        if ($journalDetail.Length -gt 300) {
            $journalDetail = $journalDetail.Substring(0, 300) + "..."
        }
        Add-Check -Name "journalctl -t jukebox-ota (últimas 20)" -Pass $journalOk -Detail $journalDetail
    } else {
        Add-Check -Name "journalctl -t jukebox-ota" -Pass $false -Detail "ignorado (unit não instalada)"
    }
}

# --- Relatório ---
Write-Host ""
Write-Host "Checklist" -ForegroundColor Cyan
Write-Host "---------"

$passCount = 0
$failCount = 0

foreach ($r in $results) {
    $icon = if ($r.Pass) { "PASS" } else { "FAIL" }
    $color = if ($r.Pass) { "Green" } else { "Red" }
    if ($r.Pass) { $passCount++ } else { $failCount++ }

    Write-Host ("[{0}] {1}" -f $icon, $r.Name) -ForegroundColor $color
    if ($r.Detail) {
        Write-Host "      $($r.Detail)" -ForegroundColor Gray
    }
}

Write-Host ""
Write-Host ("Resumo: {0} pass, {1} fail" -f $passCount, $failCount) -ForegroundColor $(if ($failCount -eq 0) { "Green" } else { "Yellow" })

if ($failCount -gt 0) {
    exit 1
}
exit 0
