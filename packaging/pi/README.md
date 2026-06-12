# Deploy no Raspberry Pi (POC)

## Pré-requisitos

- Raspberry Pi OS 64-bit (aarch64)
- .NET **não** precisa estar instalado no Pi (publish self-contained)

## Passos resumidos

1. No build server: `tools/deploy/publish-linux-arm64.sh`
2. Copiar `artifacts/linux-arm64/` para `/opt/jukebox/ota-agent/` no Pi
3. Criar `/etc/jukebox/ota-agent.json` (ver `tools/mock/ota-agent.example.json`)
4. Instalar units: `packaging/systemd/jukebox_ota_agent.service` + `.timer`
5. `sudo systemctl daemon-reload && sudo systemctl enable --now jukebox_ota_agent.timer`
6. Validar: `journalctl -t jukebox-ota -n 50`

Plano completo: [[PLANO_POC_DOTNET_PI]] · brainstorm [[PLANO_JUKEBOX_OTA_AGENT_DOTNET_POC]].
