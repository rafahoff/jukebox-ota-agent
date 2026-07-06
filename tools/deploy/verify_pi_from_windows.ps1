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

    [string]$InstallDir = "/opt/jukeeo/ota-agent",

    [string]$ConfigPath = "/etc/jukeeo/ota-agent.json",

    [string]$KioskService = "jukeeo_kiosk_flutterpi.service",

    [string]$KioskDataDir = "/home/jukebox/.local/share/com.jukeeo.kiosk",

    [string]$KioskUser = "jukebox"
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
    $binOut = Invoke-Ssh "sudo -n -u ${OtaUser} test -x '$BinaryPath' && echo EXISTS || echo MISSING"
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

    # 5a. Config gravável pelo utilizador OTA (current_version após apply)
    if ($cfgExists -and $otaUserOk) {
        $cfgWriteOut = Invoke-Ssh "sudo -n -u ${OtaUser} test -w '$ConfigPath' && echo WRITABLE || echo READONLY"
        $cfgWritable = ($LASTEXITCODE -eq 0) -and ($cfgWriteOut -match "WRITABLE")
        Add-Check -Name "Config gravável ($OtaUser)" -Pass $cfgWritable -Detail $(if ($cfgWritable) { "660 root:jukebox-ota" } else { "esperado chmod 660 — reexecute setup_primeira_instalacao_pi ou pi_install_ota.sh" })
    } else {
        Add-Check -Name "Config gravável ($OtaUser)" -Pass $false -Detail "ignorado (config ou utilizador OTA ausente)"
    }

    # 5b. kiosk_service_name no JSON com sufixo .service (sudoers exige path literal)
    if ($cfgExists) {
        $svcNameCmd = @"
sudo -n -u ${OtaUser} python3 -c "import json; c=json.load(open('$ConfigPath')); print(c.get('kiosk_service_name',''))"
"@
        $svcNameOut = Invoke-Ssh $svcNameCmd
        $svcName = ($svcNameOut | Out-String).Trim()
        $svcNameOk = ($LASTEXITCODE -eq 0) -and ($svcName -match '\.service$')
        $svcDetail = if ($svcNameOk) { $svcName } else { "valor='$svcName' (esperado sufixo .service)" }
        Add-Check -Name "kiosk_service_name (.service)" -Pass $svcNameOk -Detail $svcDetail
    } else {
        Add-Check -Name "kiosk_service_name (.service)" -Pass $false -Detail "config ausente"
    }

    # 5c. ACL traverse + leitura em kiosk_data_dir (backup SQLite no apply)
    if ($cfgExists -and $otaUserOk) {
        $aclCmd = @"
DATA='$KioskDataDir'
for p in /home/${KioskUser} /home/${KioskUser}/.local /home/${KioskUser}/.local/share; do
  if [ -d "`$p" ]; then
    sudo -n -u ${OtaUser} test -x "`$p" || { echo "TRAVERSE_FAIL:`$p"; exit 1; }
  fi
done
if [ ! -d "`$DATA" ]; then
  echo "NO_DATA_DIR"
  exit 0
fi
sudo -n -u ${OtaUser} test -r "`$DATA" && echo READ_OK || { echo "READ_FAIL"; exit 1; }
"@
        $aclOut = Invoke-Ssh $aclCmd
        $aclText = ($aclOut | Out-String).Trim()
        if ($aclText -match "NO_DATA_DIR") {
            Add-Check -Name "ACL backup kiosk_data_dir" -Pass $true -Detail "dados do kiosk ausentes ($KioskDataDir) — reexecute install após primeiro run do kiosk"
        } elseif ($LASTEXITCODE -eq 0 -and $aclText -match "READ_OK") {
            Add-Check -Name "ACL backup kiosk_data_dir" -Pass $true -Detail "traverse + leitura OK em $KioskDataDir"
        } else {
            Add-Check -Name "ACL backup kiosk_data_dir" -Pass $false -Detail $aclText
        }
    } else {
        Add-Check -Name "ACL backup kiosk_data_dir" -Pass $false -Detail "ignorado (config ou utilizador OTA ausente)"
    }

    # 6. check --config (como jukebox-ota)
    if ($cfgExists -and $binOk -and $otaUserOk) {
        $checkOut = Invoke-SshAsOta "$BinaryPath check --config '$ConfigPath'"
        $checkOk = $LASTEXITCODE -eq 0 -or $LASTEXITCODE -eq 2
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
  "public_key_path": "/etc/jukeeo/ota-public-key.pem"
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

    # 9. sudoers — systemctl do kiosk sem password
    $sudoersPath = "/etc/sudoers.d/99-jukebox-ota-systemctl"
    $sudoersOut = Invoke-Ssh "test -f '$sudoersPath' && echo EXISTS || echo MISSING"
    $sudoersExists = ($LASTEXITCODE -eq 0) -and ($sudoersOut -match "EXISTS")
    Add-Check -Name "sudoers ($sudoersPath)" -Pass $sudoersExists -Detail $(if ($sudoersExists) { "presente" } else { "ausente — reexecute pi_install_ota.sh" })

    if ($sudoersExists -and $otaUserOk) {
        $sysctlOut = Invoke-Ssh "sudo -n -u ${OtaUser} sudo -n /bin/systemctl is-active '$KioskService' 2>&1; echo exit=`$?"
        $sysctlText = ($sysctlOut | Out-String).Trim()
        $sysctlOk = $sysctlText -match 'exit=0' -or $sysctlText -match 'exit=3'
        $sysctlDetail = if ($sysctlOk) { $sysctlText } else { "$sysctlText (esperado active ou inactive, não not-found)" }
        Add-Check -Name "systemctl is-active kiosk (sudo -n como $OtaUser)" -Pass $sysctlOk -Detail $sysctlDetail

        $catOut = Invoke-Ssh "sudo -n -u ${OtaUser} sudo -n /bin/systemctl cat '$KioskService' 2>&1 | head -1"
        $catOk = $LASTEXITCODE -eq 0
        Add-Check -Name "systemctl cat kiosk (sudo -n como $OtaUser)" -Pass $catOk -Detail $(if ($catOk) { ($catOut | Out-String).Trim() } else { "ausente no sudoers — reexecute pi_install_ota.sh ou setup_primeira_instalacao_pi" })
    } else {
        Add-Check -Name "systemctl is-active kiosk (sudo -n como $OtaUser)" -Pass $false -Detail "ignorado (sudoers ou utilizador OTA ausente)"
        Add-Check -Name "systemctl cat kiosk (sudo -n como $OtaUser)" -Pass $false -Detail "ignorado (sudoers ou utilizador OTA ausente)"
    }

    # 10. journalctl (se service instalada)
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
