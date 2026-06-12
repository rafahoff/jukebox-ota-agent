# Deploy no Raspberry Pi

## Build (Windows/WSL/Linux)

```bash
./tools/deploy/publish-linux-arm64.sh
```

Ou no PowerShell: `.\tools\deploy\publish-linux-arm64.ps1`

## Copiar para o Pi

```bash
rsync -avz artifacts/linux-arm64/ jukebox@<IP>:/opt/jukebox/ota-agent/
```

## Configuração

Criar `/etc/jukebox/ota-agent.json` (modelo em `tools/mock/ota-agent.example.json`).

## systemd

```bash
sudo cp packaging/systemd/jukebox_ota_agent.service /etc/systemd/system/
sudo cp packaging/systemd/jukebox_ota_agent.timer /etc/systemd/system/
sudo systemctl daemon-reload
sudo systemctl enable --now jukebox_ota_agent.timer
```

## Validar

```bash
/opt/jukebox/ota-agent/jukebox-ota-agent version
journalctl -t jukebox-ota -n 50
```
