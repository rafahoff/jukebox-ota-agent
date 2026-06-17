# Deploy no Raspberry Pi (POC) — Agente B

**Agente B** = `jukebox-ota-agent` (.NET self-contained), processo separado do kiosk Flutter (`jukebox_tv`).

Pi alvo de referência: `jukebox@192.168.15.100` (mesmo utilizador padrão do `jukebox_tv`).

## Pré-requisitos

| Onde | Requisito |
|------|-----------|
| **Windows (build)** | .NET SDK 8+ |
| **Windows (deploy)** | WSL com `rsync` **ou** OpenSSH Client (`ssh`/`scp`) |
| **Pi** | Raspberry Pi OS 64-bit (aarch64); .NET **não** precisa estar instalado |
| **Rede** | SSH sem senha para `jukebox@<IP>` |

## Divisão Windows vs Pi

| Fase | Onde | O quê |
|------|------|-------|
| Publish | Windows | `publish-linux-arm64.ps1` → `artifacts/linux-arm64/` |
| Deploy | Windows | `deploy_to_pi.ps1` — rsync/scp staging + SSH install |
| Instalação | Pi (via SSH) | `pi_install_ota.sh` — binário, systemd, config |
| Validação | Windows | `verify_pi_from_windows.ps1` — checklist pass/fail |

## Checklist — Agente B (Pi)

Copiar e marcar após cada passo:

- [ ] **F2** — Publish `linux-arm64` self-contained (`artifacts/linux-arm64/jukebox-ota-agent` existe)
- [ ] **Deploy** — Staging enviado para `/tmp/jukebox-ota-staging/` no Pi
- [ ] **F3** — Binário em `/opt/jukebox/ota-agent/` com `chmod +x`
- [ ] **F3** — Units em `/etc/systemd/system/jukebox_ota_agent.{service,timer}`
- [ ] **Config** — `/etc/jukebox/ota-agent.json` criado (template em `tools/mock/ota-agent.example.json`)
- [ ] **Validação** — `jukebox-ota-agent version` retorna versão
- [ ] **Validação** — `check --config /etc/jukebox/ota-agent.json` (quando mock/API disponível)
- [ ] **Opcional** — `systemctl enable --now jukebox_ota_agent.timer` (só com `--enable-timer` no install)
- [ ] **Observabilidade** — `journalctl -t jukebox-ota -n 50` mostra execuções

## Comandos copy-paste (192.168.15.100)

### No Windows (PowerShell, raiz do repo)

```powershell
# 1. Build do artefato
.\tools\deploy\publish-linux-arm64.ps1

# 2. Deploy + instalação (timer desligado por padrão)
.\tools\deploy\deploy_to_pi.ps1 -PiHost 192.168.15.100

# 3. Com timer periódico habilitado
.\tools\deploy\deploy_to_pi.ps1 -PiHost 192.168.15.100 -EnableTimer

# 4. Validação orquestrada
.\tools\deploy\verify_pi_from_windows.ps1 -PiHost 192.168.15.100

# 5. Validação com mock HTTP no PC (ex.: servidor em 192.168.15.50:8080)
.\tools\deploy\verify_pi_from_windows.ps1 -PiHost 192.168.15.100 -MockBaseUrl "192.168.15.50:8080"
```

### No Pi (manual, se usar `-SkipInstall`)

```bash
ssh jukebox@192.168.15.100
sudo bash /tmp/jukebox-ota-staging/pi_install_ota.sh --enable-timer
/opt/jukebox/ota-agent/jukebox-ota-agent version
sudo journalctl -t jukebox-ota -n 50 --no-pager
```

## Dependência WSL / rsync

O `deploy_to_pi.ps1` **prefere** WSL + `rsync` (incremental, mesmo padrão do `jukebox_tv` em `tools/flutterpi/`).

Se WSL não estiver disponível ou rsync falhar, usa **fallback `scp`** nativo do Windows OpenSSH.

Configurar chave SSH no WSL (uma vez):

```bash
wsl
mkdir -p ~/.ssh && chmod 700 ~/.ssh
cp /mnt/c/Users/$USER/.ssh/id_ed25519* ~/.ssh/ 2>/dev/null || cp /mnt/c/Users/$USER/.ssh/id_rsa* ~/.ssh/
chmod 600 ~/.ssh/id_*
ssh-keyscan -H 192.168.15.100 >> ~/.ssh/known_hosts
sudo apt install -y rsync
```

## Referências

- How-to detalhado: `docs/howto/DEPLOY_PI.md`
- Plano POC: [[PLANO_POC_DOTNET_PI]] · brainstorm [[PLANO_JUKEBOX_OTA_AGENT_DOTNET_POC]]
