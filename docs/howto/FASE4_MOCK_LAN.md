# Fase 4 — Servidor OTA de desenvolvimento (VPS + Pi)

> **O mock LAN (`tools/mock/ota_mock_server.py`) está deprecado.** Usar o repositório [`jukebox-ota-server-dev`](https://github.com/rafahoff/jukebox-ota-server-dev) (Docker na VPS ou local).

Fluxo para validar `check` e `upgrade` do agente contra o servidor OTA de desenvolvimento, com download real de pacotes.

## Pré-requisitos

- VPS com `jukebox-ota-server-dev` em Docker (ver `docs/deploy-vps.md` nesse repo)
- HAProxy no pfSense a terminar TLS (rotas `/v1/*` e `/ota/*`)
- Pi com agente publicado e config em `/etc/jukeeo/ota-agent.json`
- Release publicada via `jukebox_tv/tools/ota/publish_to_ota_dev.ps1`

## 1. URL pública

Anotar o host HTTPS do frontend HAProxy (ex.: `https://ota-dev.seudominio.com`).

Definir `OTA_PUBLIC_BASE_URL` no `.env` do servidor dev com o mesmo valor.

## 2. Publicar release

No Windows, após `prepare_ota_release.ps1`:

```powershell
$env:OTA_DEV_VPS_HOST = "vps.exemplo.com"
$env:OTA_DEV_SSH_USER = "root"
$env:OTA_DEV_REMOTE_DATA_PATH = "/opt/jukebox-ota-server-dev/data"
.\tools\ota\publish_to_ota_dev.ps1
```

## 3. Config no Pi

```json
{
  "device_id": "machine-001",
  "channel": "beta",
  "ota_base_url": "https://ota-dev.seudominio.com",
  "current_version": "1.0.14",
  "public_key_path": "/etc/jukeeo/ota-public-key.pem",
  "kiosk_service_name": "jukeeo_kiosk_flutterpi.service",
  "kiosk_data_dir": "/home/jukebox/.local/share/com.jukeeo.kiosk"
}
```

## 4. Teste com curl

```bash
curl -i "https://ota-dev.seudominio.com/v1/updates/check?device_id=pi-001&channel=beta&version=1.0.14"
```

- Versão reportada **igual** à do índice → **204**
- Versão **anterior** → **200** + manifesto + `download_url`

## 5. Executar check no Pi

```bash
sudo -u jukebox-ota /opt/jukeeo/ota-agent/jukebox-ota-agent check --config /etc/jukeeo/ota-agent.json
```

Código de saída `2` indica actualização disponível.

## Teste local (sem VPS)

Na pasta `jukebox-ota-server-dev`:

```bash
docker compose up --build
curl -i "http://127.0.0.1:9080/v1/updates/check?device_id=pi&channel=beta&version=0.0.0"
```

## Referências

- Servidor dev: repositório `jukebox-ota-server-dev`
- Contrato API: `docs/API.md`
- Deploy Pi: `docs/howto/DEPLOY_PI.md`
- Mock deprecado: `tools/mock/ota_mock_server.py`
