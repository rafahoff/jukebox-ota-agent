# Fase 4 — Mock OTA em LAN (Windows + Raspberry Pi)

Fluxo para validar `check` do agente contra um servidor HTTP mock no PC Windows, com o Pi como cliente na mesma rede.

## Pré-requisitos

- Python 3 no Windows (stdlib apenas)
- Pi com agente publicado e config em `/etc/jukeeo/ota-agent.json`
- PC e Pi na mesma LAN

## 1. Descobrir IP do Windows

No PowerShell:

```powershell
ipconfig
```

Anotar o IPv4 da interface LAN (ex.: `192.168.15.42`).

## 2. Firewall — porta 8080

Permitir entrada TCP na porta 8080 (ajustar perfil conforme a rede):

```powershell
New-NetFirewallRule -DisplayName "Jukebox OTA Mock" -Direction Inbound -Protocol TCP -LocalPort 8080 -Action Allow
```

Ou via Painel de Controlo → Firewall do Windows → Regras de entrada.

## 3. Iniciar mock no Windows

Na raiz do repositório `jukebox-ota-agent`:

```powershell
python tools/mock/ota_mock_server.py --mode auto
```

Modos úteis:

| `--mode` | Comportamento |
|----------|----------------|
| `no-update` | Sempre HTTP 204 |
| `has-update` | Sempre HTTP 200 + manifesto |
| `auto` | 204 se `version` na query == versão do manifesto; senão 200 |

Manifesto padrão: `tools/mock/manifest.example.json` (ou `manifest.json` na mesma pasta).

## 4. Teste com curl (Windows ou Pi)

```bash
curl -i "http://192.168.15.42:8080/v1/updates/check?device_id=pi-001&channel=beta&version=1.4.1"
```

- `version=1.4.1` com manifesto `1.4.2` em modo `auto` → **200** + JSON
- `version=1.4.2` em modo `auto` → **204** sem corpo

## 5. Config no Pi

Copiar modelo `tools/mock/ota-agent.pi-lan.example.json` para `/etc/jukeeo/ota-agent.json` e substituir `192.168.15.XXX` pelo IP real do Windows.

```json
{
  "device_id": "machine-001",
  "channel": "beta",
  "ota_base_url": "http://192.168.15.42:8080",
  "current_version": "1.4.1",
  "public_key_path": "/etc/jukeeo/ota-public-key.pem",
  "kiosk_service_name": "jukeeo_kiosk_flutterpi.service",
  "kiosk_data_dir": "/home/jukebox/.local/share/com.jukeeo.kiosk"
}
```

## 6. Executar check no Pi

```bash
sudo -u jukebox-ota /opt/jukeeo/ota-agent/jukebox-ota-agent check --config /etc/jukeeo/ota-agent.json
```

Código de saída `2` indica actualização disponível (manifesto recebido com versão superior). Ver telemetria em `journalctl -t jukebox-ota` quando o timer systemd estiver activo.

## Referências

- Mock: `tools/mock/ota_mock_server.py`
- Contrato API: `docs/API.md`
- Deploy Pi: `docs/howto/DEPLOY_PI.md`
