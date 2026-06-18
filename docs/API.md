# API OTA — contrato consumido pelo agente

Referência mínima para a POC. Servidor completo: repo `jukebox-ota-server` (futuro).

Plano de execução (paths, apply, ack): `jukebox_tv/docs/plans/PLANO_OTA_EXECUCAO_PI.md`.

## GET `/v1/updates/check`

**Query:** `device_id`, `channel`, `version` (versão actual reportada pelo dispositivo), `arch`.

**Respostas:**

| Código | Corpo | Significado |
|--------|-------|-------------|
| `204` | vazio | Sem actualização |
| `200` | JSON manifesto | Actualização disponível |

**v2 (raro):** repetir `check` → apply (kiosk parado) até `204` antes de `systemctl start`. Operacionalmente preferir pacote `full` único.

### Manifesto (200)

```json
{
  "app": "jukeeo",
  "version": "1.4.2",
  "arch": "aarch64",
  "package_type": "full",
  "sha256": "...",
  "signature_b64": "...",
  "signature_algorithm": "rsa-pss-sha256",
  "flutter_pi_min_version": "0.0.0",
  "released_at": "2026-06-12T12:00:00Z"
}
```

| Campo | v1 | Notas |
|-------|-----|-------|
| `app` | `jukeeo` | Produto OTA (não `jukebox_tv`) |
| `package_type` | `full` | v2: `kiosk`, `runtime` |
| `download_url` | Sim | URL pré-assinada do blob (quando servidor completo) |

## POST `/v1/updates/ack`

Enviar **apenas após tentativa de apply** (sucesso, rollback ou erro irrecuperável).

```json
{
  "device_id": "machine-001",
  "app": "jukeeo",
  "channel": "beta",
  "version_attempted": "1.4.2",
  "version_previous": "1.4.1",
  "version_current": "1.4.1",
  "result": "rolled_back",
  "error_code": "health_version_mismatch",
  "error_message": "opcional",
  "package_type": "full",
  "arch": "aarch64",
  "occurred_at": "2026-06-17T21:30:00Z"
}
```

| `result` | Significado |
|----------|-------------|
| `success` | Health check OK após swap |
| `rolled_back` | Health falhou; `current` reposto para `previous` |
| `error` | Falha antes ou durante apply sem rollback completo |

### `error_code` (enum inicial)

`download_failed`, `hash_mismatch`, `signature_invalid`, `disk_space_insufficient`, `service_inactive`, `health_check_timeout`, `health_version_mismatch`, `rollback_failed`.

## Assinatura

- Algoritmo: **RSA-PSS com SHA-256**
- Payload assinado: JSON canónico do manifesto **sem** `signature_b64`
- Pacote: validado por `sha256` do ficheiro `.tar.zst`
- Chave pública no Pi: `/etc/jukeeo/ota-public-key.pem`
- Assinatura (só Pi builder): `/etc/jukeeo/ota-sign-key.pem` — backup no cofre de segredos

## Blobs

Imutáveis: `/ota/jukeeo/1.4.2/aarch64/jukeeo-1.4.2+aarch64.tar.zst`

## Mock local (POC)

Configurar `ota_base_url` como `file:///caminho/absoluto/manifest.json` em `/etc/jukeeo/ota-agent.json`.

## CLI do agente

```text
jukebox-ota-agent version
jukebox-ota-agent check --config /etc/jukeeo/ota-agent.json
jukebox-ota-agent verify --manifest <manifest.json> --package <arquivo.tar.zst> [--public-key <chave.pem>]
jukebox-ota-agent sign-manifest --manifest <in.json> --private-key <pem> [--output <out.json>]
jukebox-ota-agent apply --config <json> --manifest <json> [--package <path>]
```

### `sign-manifest`

Assina o manifesto com RSA-PSS (SHA-256). Payload canónico: JSON sem `signature_b64` (ver `RsaPssPackageVerifier.BuildCanonicalManifestJson`). Grava `signature_b64` e `signature_algorithm: rsa-pss-sha256`.

### `apply`

Fluxo mínimo para piloto:

1. `systemctl stop` do kiosk (`jukeeo_kiosk_flutterpi`)
2. `previous` ← alvo de `current`
3. Backup SQLite + `shared_preferences.json`
4. Extrair pacote para `releases/<version>+<arch>/`
5. `current` → nova release
6. `systemctl start`
7. Health: serviço activo + `GET /api/health` com `app_version` (timeout 90s)
8. `POST /v1/updates/ack`
9. Em falha de health: rollback `current` → `previous`, restart, `ack` com `rolled_back`

### Configuração (`/etc/jukeeo/ota-agent.json`)

| Campo | Padrão | Notas |
|-------|--------|-------|
| `device_id` | — | Obrigatório |
| `channel` | `stable` | |
| `ota_base_url` | — | Base HTTP(S) ou `file://` |
| `current_version` | `0.0.0` | |
| `public_key_path` | — | `/etc/jukeeo/ota-public-key.pem` |
| `kiosk_service_name` | `jukeeo_kiosk_flutterpi` | systemd |
| `releases_dir` | `/opt/jukeeo/releases` | |
| `current_symlink` | `/opt/jukeeo/current` | |
| `previous_symlink` | `/opt/jukeeo/previous` | |
| `backups_dir` | `/opt/jukeeo/backups` | |
| `health_url` | `http://127.0.0.1:8080/api/health` | |
| `kiosk_data_dir` | `~/.local/share/com.jukeeo.kiosk` | Backup pré-update |
| `max_release_folders` | `7` | GC releases e backups |

## Layout no Pi (referência)

| Caminho | Função |
|---------|--------|
| `/opt/jukeeo/current` | Release em execução |
| `/opt/jukeeo/previous` | Rollback |
| `/opt/jukeeo/releases/` | Histórico (GC: máx. 7) |
| `/opt/jukeeo/backups/` | Snapshot pré-update |
| `/opt/jukeeo/ota-agent/` | Binário deste repo |
