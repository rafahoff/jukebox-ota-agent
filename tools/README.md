# Tools — scripts auxiliares

Scripts operacionais do **jukebox-ota-agent** (publish, deploy no Pi, mock OTA). Guias detalhados em `docs/howto/`.

## Organização

| Local | Uso |
|-------|-----|
| `tools/deploy/` | Publish `linux-arm64`, deploy Windows → Pi, install e validação |
| `tools/mock/` | Fixtures JSON, servidor HTTP mock OTA e configs de exemplo |
| `packaging/systemd/` | Units `jukebox_ota_agent.{service,timer}` (empacotadas no deploy) |
| `packaging/sudoers/` | Template `jukebox-ota-systemctl` (systemctl do kiosk sem password) |

Scripts de **deploy** não ficam na raiz do repositório.

## Fluxo deploy (visão geral)

```text
[Windows]  publish-linux-arm64.ps1  →  artifacts/linux-arm64/
     │
     ▼
[Windows]  ota_deploy_to_pi.ps1  →  rsync/scp  →  /tmp/jukebox-ota-staging/ (Pi)
     │
     ▼
[Pi]       pi_install_ota.sh  →  /opt/jukeeo/ota-agent/ + systemd + sudoers + config
     │
     ▼
[Windows]  verify_pi_from_windows.ps1  →  checklist pass/fail
```

Pi de referência: `jukebox@192.168.15.100`. How-to completo: [`docs/howto/DEPLOY_PI.md`](../docs/howto/DEPLOY_PI.md) · checklist copy-paste: [`packaging/pi/README.md`](../packaging/pi/README.md).

## Scripts de deploy

| Script | Descrição |
|--------|-----------|
| `deploy/publish-linux-arm64.ps1` | Publish self-contained `linux-arm64` → `artifacts/linux-arm64/` |
| `deploy/publish-linux-arm64.sh` | Equivalente bash (WSL/Linux) |
| `deploy/ota_deploy_to_pi.ps1` | Deploy Windows → Pi (staging + install via SSH) |
| `deploy/deploy_to_pi_rsync.sh` | Rsync WSL (auxiliar interno do `ota_deploy_to_pi`) |
| `deploy/pi_install_ota.sh` | Instalação no Pi: binário, systemd, sudoers, permissões `/opt/jukeeo` |
| `deploy/verify_pi_from_windows.ps1` | Validação orquestrada via SSH (checklist pass/fail) |

### `ota_deploy_to_pi.ps1`

Orquestra o deploy completo **Windows → Raspberry Pi**: publish (opcional), montagem de staging, transferência SSH e instalação remota via `pi_install_ota.sh`.

**Pré-requisitos**

| Onde | Requisito |
|------|-----------|
| Windows | PowerShell; .NET SDK 8+ (se não usar `-SkipPublish`) |
| Rede | SSH sem senha para `jukebox@<IP>` |
| Transferência (preferido) | WSL com `rsync` e chave SSH em `~/.ssh` do WSL |
| Transferência (fallback) | OpenSSH Client no Windows (`ssh` + `scp`) |

Executar na **raiz do repositório** (ou a partir de qualquer pasta — o script resolve paths relativamente a si).

**Parâmetros**

| Parâmetro | Padrão | Descrição |
|-----------|--------|-----------|
| `-PiHost` | `192.168.15.100` | IP do Raspberry Pi |
| `-PiUser` | `jukebox` | Utilizador SSH |
| `-RemotePath` | `/opt/jukeeo/ota-agent` | Destino final do binário no Pi |
| `-RemoteStaging` | `/tmp/jukebox-ota-staging` | Staging temporário no Pi (envio + install) |
| `-SkipPublish` | — | Não executa rebuild; exige `artifacts/linux-arm64/jukebox-ota-agent` |
| `-SkipInstall` | — | Só envia staging; install manual no Pi |
| `-EnableTimer` | — | Passa `--enable-timer` ao `pi_install_ota.sh` |
| `-ForceConfig` | — | Sobrescreve `/etc/jukeeo/ota-agent.json` no Pi |

**O que o script faz (por etapa)**

1. **Publish** — chama `publish-linux-arm64.ps1` (salvo `-SkipPublish`) e valida que `artifacts/linux-arm64/jukebox-ota-agent` existe.
2. **Staging local** — monta `artifacts/pi-deploy-staging/` com:
   - `artifacts/` — runtime self-contained publicado
   - `systemd/` — `jukebox_ota_agent.service` + `.timer`
   - `config/ota-agent.example.json` — template de config
   - `sudoers/jukebox-ota-systemctl.template`
   - `pi_install_ota.sh`
3. **Transferência** — envia staging para `$RemoteStaging` no Pi:
   - **Preferido:** WSL + `deploy_to_pi_rsync.sh` (rsync incremental, `--delete`)
   - **Fallback:** `scp` nativo se WSL/rsync falhar
4. **Instalação** (salvo `-SkipInstall`) — SSH remoto: `sudo bash pi_install_ota.sh` com `--staging-dir` e `--install-dir`; repassa `--enable-timer` / `--force-config` quando pedido.
5. **Saída** — sugere `verify_pi_from_windows.ps1` ao concluir.

**Exemplos**

```powershell
# Deploy completo (rebuild + install; timer desligado)
.\tools\deploy\ota_deploy_to_pi.ps1

# Pi específico + habilitar timer periódico
.\tools\deploy\ota_deploy_to_pi.ps1 -PiHost 192.168.15.100 -EnableTimer

# Só reenviar artefato já publicado (sem dotnet publish)
.\tools\deploy\ota_deploy_to_pi.ps1 -SkipPublish

# Só enviar staging — install manual no Pi
.\tools\deploy\ota_deploy_to_pi.ps1 -SkipInstall
# No Pi depois:
#   sudo bash /tmp/jukebox-ota-staging/pi_install_ota.sh --enable-timer

# Reinstalar e sobrescrever config existente
.\tools\deploy\ota_deploy_to_pi.ps1 -ForceConfig

# Destino customizado (lab)
.\tools\deploy\ota_deploy_to_pi.ps1 -PiHost 192.168.15.50 -RemotePath /opt/jukeeo/ota-agent -RemoteStaging /tmp/jukebox-ota-staging
```

**Códigos de saída**

| Código | Significado |
|--------|-------------|
| `0` | Staging enviado (e install OK, se não `-SkipInstall`) |
| `1` | Falha em publish, artefato ausente, SSH, transferência ou install remoto |

**Pós-deploy**

```powershell
.\tools\deploy\verify_pi_from_windows.ps1 -PiHost 192.168.15.100
```

**WSL + rsync (recomendado)**

O script tenta rsync primeiro. Configurar chave no WSL (uma vez):

```bash
wsl
mkdir -p ~/.ssh && chmod 700 ~/.ssh
cp /mnt/c/Users/$USER/.ssh/id_ed25519* ~/.ssh/ 2>/dev/null || cp /mnt/c/Users/$USER/.ssh/id_rsa* ~/.ssh/
chmod 600 ~/.ssh/id_*
ssh-keyscan -H 192.168.15.100 >> ~/.ssh/known_hosts
sudo apt install -y rsync
```

Se rsync falhar, o script tenta **scp** automaticamente. O timer systemd **não** é habilitado por padrão (validar com `verify` antes de `-EnableTimer` em fleet — JUK-69).
### `pi_install_ota.sh`

Executado automaticamente pelo deploy ou manualmente no Pi (`sudo bash …/pi_install_ota.sh`).

| Opção | Descrição |
|-------|-----------|
| `--enable-timer` | Habilita e inicia `jukebox_ota_agent.timer` |
| `--force-config` | Sobrescreve `/etc/jukeeo/ota-agent.json` |
| `--staging-dir <dir>` | Staging (padrão: `/tmp/jukebox-ota-staging`) |
| `--install-dir <dir>` | Binário (padrão: `/opt/jukeeo/ota-agent`) |
| `--kiosk-service <unit>` | Unit do kiosk (padrão: `jukeeo_kiosk_flutterpi.service`) |
| `--kiosk-user <user>` | Utilizador kiosk para ACL de backup (padrão: `jukebox`) |

O timer **não** é habilitado por padrão (JUK-69: validar antes de fleet).

### `verify_pi_from_windows.ps1`

| Parâmetro | Padrão | Descrição |
|-----------|--------|-----------|
| `-PiHost` | `192.168.15.100` | IP do Pi |
| `-PiUser` | `jukebox` | Utilizador SSH |
| `-MockBaseUrl` | — | Ex.: `192.168.15.50:8080` — testa `check` contra mock HTTP no PC |
| `-InstallDir` | `/opt/jukeeo/ota-agent` | Caminho do binário |
| `-ConfigPath` | `/etc/jukeeo/ota-agent.json` | Config do agente |

Exit code `0` se todos os checks passarem; `1` se algum falhar.

## Mock e fixtures (`tools/mock/`)

| Ficheiro | Uso |
|----------|-----|
| `ota-agent.example.json` | Template completo de config (`/etc/jukeeo/ota-agent.json` no Pi) |
| `ota-agent.pi-lan.example.json` | Config LAN — substituir `192.168.15.XXX` pelo IP do PC com mock |
| `ota-agent.pi-100.json` | Exemplo real do Pi `192.168.15.100` (lab) |
| `manifest.example.json` | Manifesto OTA assinado (modo `has-update` / `auto`) |
| `manifest-rollback-test.json` | Manifesto para cenários de rollback/apply |
| `ota_mock_server.py` | Servidor HTTP mock da API OTA (Fase 4, stdlib Python 3) |

Contrato HTTP: [`docs/API.md`](../docs/API.md).

### Mock local (`file://`)

1. Copiar `tools/mock/ota-agent.example.json` e apontar `ota_base_url` para `file://` + caminho absoluto do manifesto.
2. Executar:

```powershell
dotnet run --project src/Jukebox.Ota.Agent -- check --config tools/mock/ota-agent.example.json
```

Outros comandos CLI úteis em desenvolvimento:

```powershell
dotnet run --project src/Jukebox.Ota.Agent -- version
dotnet run --project src/Jukebox.Ota.Agent -- verify --manifest tools/mock/manifest.example.json --package <pacote.tar.zst>
```

### Mock HTTP em LAN (Windows)

Servidor stdlib na porta **8080** (bind `0.0.0.0`):

```powershell
python tools/mock/ota_mock_server.py --mode auto
```

| Modo | Resposta |
|------|----------|
| `no-update` | HTTP 204 |
| `has-update` | HTTP 200 + `manifest.example.json` |
| `auto` | 204 se `version` == manifesto; senão 200 |

Teste rápido:

```powershell
curl -i "http://127.0.0.1:8080/v1/updates/check?device_id=pi-001&channel=beta&version=1.4.1"
```

No Pi, usar `ota-agent.pi-lan.example.json` como base e executar `check` como utilizador `jukebox-ota`:

```bash
sudo -u jukebox-ota /opt/jukeeo/ota-agent/jukebox-ota-agent check --config /etc/jukeeo/ota-agent.json
```

Guia completo (firewall, IP do PC, journald): [`docs/howto/FASE4_MOCK_LAN.md`](../docs/howto/FASE4_MOCK_LAN.md).

## Documentação relacionada

| Nota | Tema |
|------|------|
| [`docs/index.md`](../docs/index.md) | MOC do repositório |
| [`docs/howto/DEPLOY_PI.md`](../docs/howto/DEPLOY_PI.md) | Deploy, systemd, permissões, validação |
| [`docs/howto/FASE4_MOCK_LAN.md`](../docs/howto/FASE4_MOCK_LAN.md) | Mock OTA HTTP Windows + Pi cliente |
| [`packaging/pi/README.md`](../packaging/pi/README.md) | Checklist e comandos copy-paste |
| [`docs/API.md`](../docs/API.md) | Contrato `/v1/updates/check` e `/v1/updates/ack` |
