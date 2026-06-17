# Tools — scripts auxiliares

## Organização

| Local | Uso |
|-------|-----|
| `tools/deploy/` | Build, publish `linux-arm64`, empacotamento para Pi |
| `tools/mock/` | Fixtures, servidor HTTP mock OTA e configs de exemplo |
| `tools/` (raiz) | Workarounds e diagnóstico pontual |

Scripts de **deploy** não ficam na raiz do repositório.

## Scripts

| Script | Descrição |
|--------|-----------|
| `deploy/publish-linux-arm64.ps1` | Publish self-contained `linux-arm64` para `artifacts/` |
| `deploy/publish-linux-arm64.sh` | Equivalente bash (WSL/Linux) |
| `deploy/deploy_to_pi.ps1` | Deploy Windows → Pi (staging + install via SSH) |
| `deploy/deploy_to_pi_rsync.sh` | Rsync WSL (auxiliar do deploy_to_pi) |
| `deploy/pi_install_ota.sh` | Instalação no Pi: binário, systemd, config |
| `deploy/verify_pi_from_windows.ps1` | Validação orquestrada via SSH (checklist pass/fail) |
| `mock/ota_mock_server.py` | Servidor HTTP mock da API OTA (Fase 4, stdlib Python 3) |

## Mock local (`file://`)

1. Copiar `tools/mock/ota-agent.example.json` e apontar `ota_base_url` para `file://` + caminho absoluto do manifesto.
2. Executar: `dotnet run --project src/Jukebox.Ota.Agent -- check --config <config.json>`

## Mock HTTP em LAN (Windows)

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

Config Pi + firewall: ver `docs/howto/FASE4_MOCK_LAN.md`. Modelo LAN: `tools/mock/ota-agent.pi-lan.example.json`.
