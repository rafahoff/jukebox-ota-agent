# Deploy no Raspberry Pi — Agente OTA (Agente B)

Guia operacional para Fases 2–3 da POC: publish self-contained, deploy Windows → Pi, systemd e validação.

**Pi de referência:** `jukebox@192.168.15.100`

## Visão geral

```text
[Windows]  publish-linux-arm64.ps1  →  artifacts/linux-arm64/
     │
     ▼
[Windows]  deploy_to_pi.ps1  →  rsync/scp  →  /tmp/jukebox-ota-staging/ (Pi)
     │
     ▼
[Pi]       pi_install_ota.sh  →  /opt/jukeeo/ota-agent/ + systemd + sudoers + config
     │
     ▼
[Windows]  verify_pi_from_windows.ps1  →  checklist pass/fail
```

## Fase 2 — Publish `linux-arm64`

No Windows (PowerShell) ou WSL/Linux:

```powershell
.\tools\deploy\publish-linux-arm64.ps1
```

```bash
./tools/deploy/publish-linux-arm64.sh
```

Saída: `artifacts/linux-arm64/jukebox-ota-agent` (+ runtime .NET self-contained).

Critério de saída F2: binário executa `version` no Pi.

## Fase 3 — systemd + journald

Units em `packaging/systemd/`:

- `jukebox_ota_agent.service` — oneshot **`upgrade`** como utilizador **`jukebox-ota`** (check com política SQLite → download + apply se houver update)
- `jukebox_ota_agent.timer` — `OnBootSec=5min`, `OnUnitActiveSec=10min` (tick barato; intervalo/janela na política SQLite)

O `pi_install_ota.sh` cria o utilizador de sistema `jukebox-ota` e aplica permissões:

| Caminho | Dono | Modo | Notas |
|---------|------|------|-------|
| `/opt/jukeeo/ota-agent/` | `root:jukebox-ota` | dirs `750`, ficheiros `640`, binário `750` | Agente lê/executa; não grava |
| `/home/jukebox/.local/share/com.jukeeo.kiosk/ota_update_status.json` | `jukebox` + ACL | `664` + ACL | Estado OTA para UI (`pi_install` aplica ACL `jukebox-ota:rw`) |
| `/etc/jukeeo/` | `root:jukebox-ota` | `750` | Config e PEM legíveis pelo grupo |
| `/etc/jukeeo/ota-agent.json` | `root:jukebox-ota` | `640` | |
| `/opt/jukeeo/releases`, `backups`, `ota/*` | `root:jukebox-ota` | `2775` (setgid) | Apply grava releases/backups; builder em `ota/out` |
| Utilizador builder | `jukebox` ∈ grupo `jukebox-ota` | — | `prepare_ota_release` / `package_flutterpi_bundle.sh` |
| `/etc/sudoers.d/99-jukebox-ota-systemctl` | `root` | `440` | `systemctl` do kiosk sem password |
| `/var/lib/jukebox-ota/` | `jukebox-ota:jukebox-ota` | `750` | Estado do agente (`last_check_at_ms`) |

Teste manual no Pi (requer `sudo`):

```bash
sudo -u jukebox-ota /opt/jukeeo/ota-agent/jukebox-ota-agent version
sudo -u jukebox-ota sudo -n /bin/systemctl is-active jukeeo_kiosk_flutterpi.service
sudo systemctl start jukebox_ota_agent.service
```

O utilizador SSH `jukebox` **não** executa o binário directamente após o install — usar `sudo -u jukebox-ota` ou o timer systemd.

O timer **não** é habilitado automaticamente; use `--enable-timer` no install ou `deploy_to_pi.ps1 -EnableTimer`.

Identificador journald: `jukebox-ota` (`SyslogIdentifier` na unit).

## Deploy orquestrado (Windows)

### `deploy_to_pi.ps1`

| Parâmetro | Padrão | Descrição |
|-----------|--------|-----------|
| `-PiHost` | `192.168.15.100` | IP do Raspberry Pi |
| `-PiUser` | `jukebox` | Utilizador SSH |
| `-RemotePath` | `/opt/jukeeo/ota-agent` | Destino final do binário |
| `-RemoteStaging` | `/tmp/jukebox-ota-staging` | Staging temporário no Pi |
| `-SkipPublish` | — | Pula o publish; usa `artifacts/` existente (falha se ausente) |
| `-SkipInstall` | — | Só envia staging; install manual no Pi |
| `-EnableTimer` | — | Passa `--enable-timer` ao install |
| `-ForceConfig` | — | Sobrescreve `/etc/jukeeo/ota-agent.json` |

Fluxo interno:

1. Executa `publish-linux-arm64.ps1` (rebuild) — salvo `-SkipPublish`
2. Monta staging local com artifacts + systemd + sudoers + template de config + `pi_install_ota.sh`
3. Envia via **WSL rsync** (preferido) ou **scp** (fallback)
4. SSH: `sudo bash pi_install_ota.sh`

Exemplo completo para `192.168.15.100`:

```powershell
.\tools\deploy\deploy_to_pi.ps1 -PiHost 192.168.15.100 -EnableTimer
```

## Instalação no Pi

### `pi_install_ota.sh`

Executado automaticamente pelo deploy ou manualmente no Pi:

```bash
sudo bash /tmp/jukebox-ota-staging/pi_install_ota.sh
sudo bash /tmp/jukebox-ota-staging/pi_install_ota.sh --enable-timer
sudo bash /tmp/jukebox-ota-staging/pi_install_ota.sh --force-config
```

Ações:

- Cria utilizador de sistema `jukebox-ota` (sem login)
- Copia `artifacts/` → `/opt/jukeeo/ota-agent/` com permissões `root:jukebox-ota`
- Cria `/opt/jukeeo/{releases,backups,ota/*}` graváveis pelo grupo `jukebox-ota`
- Adiciona o utilizador kiosk/build (`jukebox`) ao grupo `jukebox-ota` (escrita em `ota/out`)
- Instala `/etc/sudoers.d/99-jukebox-ota-systemctl` (systemctl do kiosk sem password)
- ACL de leitura nos dados do kiosk (`/home/jukebox/.local/share/com.jukeeo.kiosk`)
- Traverse ACL em `/home/jukebox`, `.local` e `.local/share` (pacote `acl` instalado se ausente)
- Normaliza `kiosk_service_name` (sufixo `.service`) e `kiosk_data_dir` (`~` → path absoluto do utilizador kiosk)
- Copia units → `/etc/systemd/system/`
- Cria `/etc/jukeeo/ota-agent.json` do template se não existir (`640`)
- `systemctl daemon-reload`
- Timer só com `--enable-timer`

### sudoers (apply OTA)

O `apply` para/inicia o kiosk via `systemctl`. Como o agente corre como `jukebox-ota` (não root), o install grava um fragmento sudoers restrito:

- Ficheiro: `/etc/sudoers.d/99-jukebox-ota-systemctl` (template em `packaging/sudoers/`)
- Comandos permitidos: `start`, `stop`, `restart`, `is-active` **apenas** em `jukeeo_kiosk_flutterpi.service`
- O binário invoca `sudo -n /bin/systemctl …` quando o euid ≠ 0

Validar após install:

```bash
sudo -u jukebox-ota sudo -n /bin/systemctl is-active jukeeo_kiosk_flutterpi.service
```

**Sandbox systemd:** `ProtectHome=read-only` + `ReadWritePaths` em `kiosk_data_dir`, `/opt/jukeeo/{releases,backups}` e `/opt/jukeeo` (symlinks `current`/`previous`). Sem isto o timer ignora intervalo/janela da UI (defaults 30 min). `NoNewPrivileges=no` — necessário para `sudo -n systemctl` no apply.

**Nota:** disparo manual continua disponível (`check`, `upgrade --force` na CLI).

## Configuração

Modelo: `tools/mock/ota-agent.example.json`

```json
{
  "device_id": "machine-001",
  "channel": "beta",
  "ota_base_url": "file:///etc/jukeeo/mock-manifest.json",
  "current_version": "1.4.1",
  "public_key_path": "/etc/jukeeo/ota-public-key.pem",
  "kiosk_service_name": "jukeeo_kiosk_flutterpi.service",
  "kiosk_data_dir": "/home/jukebox/.local/share/com.jukeeo.kiosk"
}
```

Para Fase 4 (mock HTTPS no PC), ajuste `ota_base_url` para `http://<IP_DO_PC>:8080/...`.

## Validação (Windows)

### `verify_pi_from_windows.ps1`

```powershell
.\tools\deploy\verify_pi_from_windows.ps1 -PiHost 192.168.15.100
.\tools\deploy\verify_pi_from_windows.ps1 -MockBaseUrl "192.168.15.50:8080"
```

Checks executados via SSH:

| Check | Critério |
|-------|----------|
| SSH | Conectividade |
| Utilizador OTA | `jukebox-ota` existe (`getent passwd`) |
| Binário | `/opt/jukeeo/ota-agent/jukebox-ota-agent` executável |
| `version` | Exit 0 como `sudo -u jukebox-ota` |
| Config | `/etc/jukeeo/ota-agent.json` legível por `jukebox-ota` |
| `kiosk_service_name` | Valor no JSON termina em `.service` (alinha com sudoers) |
| ACL backup | Traverse em `/home/jukebox` + leitura em `kiosk_data_dir` como `jukebox-ota` |
| sudoers | `/etc/sudoers.d/99-jukebox-ota-systemctl` + `systemctl is-active` como `jukebox-ota` |
| `check --config` | Exit `0` (sem update) ou `2` (update disponível) como `jukebox-ota` |
| systemd service | `SuccessExitStatus=0 2` — `systemctl start` não marca failed com update pendente |
| Mock opcional | `check` com URL temporária (`-MockBaseUrl`) |
| systemd | Unit instalada; `User=jukebox-ota` na service |
| journald | `journalctl -t jukebox-ota -n 20` |

Exit code: `0` se todos passarem; `1` se algum falhar.

## Checklist Agente B (Pi)

- [ ] Publish F2 OK
- [ ] Deploy staging OK
- [ ] Install F3 OK (`version` no Pi)
- [ ] Config JSON presente
- [ ] `check` contra mock/API (F4)
- [x] Timer habilitado (`-EnableTimer` / `--enable-timer`) — validado 2026-06-18 em `192.168.15.100` (JUK-69)
- [ ] Logs em journald

## WSL vs scp

| Método | Vantagem | Requisito |
|--------|----------|-----------|
| **WSL + rsync** | Transferência incremental; padrão `jukebox_tv` | WSL, `rsync`, chave SSH em `~/.ssh` do WSL |
| **scp nativo** | Sem WSL | OpenSSH Client no Windows |

O script tenta rsync primeiro; em falha, usa scp.

## Referências

- `packaging/pi/README.md` — resumo e copy-paste
- `tools/README.md` — índice de scripts
- [[PLANO_POC_DOTNET_PI]]
