# Tools — scripts auxiliares

## Organização

| Local | Uso |
|-------|-----|
| `tools/deploy/` | Build, publish `linux-arm64`, empacotamento para Pi |
| `tools/mock/` | Fixtures de configuração e manifesto para desenvolvimento local |
| `tools/` (raiz) | Workarounds e diagnóstico pontual |

Scripts de **deploy** não ficam na raiz do repositório.

## Scripts

| Script | Descrição |
|--------|-----------|
| `deploy/publish-linux-arm64.ps1` | Publish self-contained `linux-arm64` para `artifacts/` |
| `deploy/publish-linux-arm64.sh` | Equivalente bash (WSL/Linux) |

## Mock local

1. Copiar `tools/mock/ota-agent.example.json` e apontar `ota_base_url` para `file://` + caminho absoluto do manifesto.
2. Executar: `dotnet run --project src/Jukebox.Ota.Agent -- check --config <config.json>`
