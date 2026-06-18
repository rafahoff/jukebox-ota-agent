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
[Pi]       pi_install_ota.sh  →  /opt/jukebox/ota-agent/ + systemd + config
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

- `jukebox_ota_agent.service` — oneshot `check` como utilizador **`jukebox-ota`** (não root)
- `jukebox_ota_agent.timer` — `OnBootSec=5min`, `OnUnitActiveSec=6h`

O `pi_install_ota.sh` cria o utilizador de sistema `jukebox-ota` e aplica permissões:

| Caminho | Dono | Modo | Notas |
|---------|------|------|-------|
| `/opt/jukebox/ota-agent/` | `root:jukebox-ota` | dirs `750`, ficheiros `640`, binário `750` | Agente lê/executa; não grava |
| `/etc/jukebox/` | `root:jukebox-ota` | `750` | Config e PEM legíveis pelo grupo |
| `/etc/jukebox/ota-agent.json` | `root:jukebox-ota` | `640` | |
| `/var/lib/jukebox-ota/` | `jukebox-ota:jukebox-ota` | `750` | Estado futuro (downloads) |

Teste manual no Pi (requer `sudo`):

```bash
sudo -u jukebox-ota /opt/jukebox/ota-agent/jukebox-ota-agent version
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
| `-RemotePath` | `/opt/jukebox/ota-agent` | Destino final do binário |
| `-RemoteStaging` | `/tmp/jukebox-ota-staging` | Staging temporário no Pi |
| `-SkipPublish` | — | Não chama publish se artifact ausente (falha) |
| `-SkipInstall` | — | Só envia staging; install manual no Pi |
| `-EnableTimer` | — | Passa `--enable-timer` ao install |
| `-ForceConfig` | — | Sobrescreve `/etc/jukebox/ota-agent.json` |

Fluxo interno:

1. Verifica `artifacts/linux-arm64/jukebox-ota-agent` (publica se ausente)
2. Monta staging local com artifacts + systemd + template de config + `pi_install_ota.sh`
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
- Copia `artifacts/` → `/opt/jukebox/ota-agent/` com permissões `root:jukebox-ota`
- Copia units → `/etc/systemd/system/`
- Cria `/etc/jukebox/ota-agent.json` do template se não existir (`640`)
- `systemctl daemon-reload`
- Timer só com `--enable-timer`

## Configuração

Modelo: `tools/mock/ota-agent.example.json`

```json
{
  "device_id": "machine-001",
  "channel": "beta",
  "ota_base_url": "file:///etc/jukebox/mock-manifest.json",
  "current_version": "1.4.1",
  "public_key_path": "/etc/jukebox/ota-public-key.pem"
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
| Binário | `/opt/jukebox/ota-agent/jukebox-ota-agent` executável |
| `version` | Exit 0 como `sudo -u jukebox-ota` |
| Config | `/etc/jukebox/ota-agent.json` legível por `jukebox-ota` |
| `check --config` | Exit 0 como `jukebox-ota` (se config presente) |
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
- [ ] Timer habilitado (se desejado)
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
