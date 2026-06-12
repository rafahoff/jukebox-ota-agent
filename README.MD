# jukebox-ota-agent

Agente de atualização over-the-air (OTA) para quiosques Jukebox — processo **separado** do app Flutter e do sync agent.

- **Linguagem:** .NET 8 (`linux-arm64` self-contained no Pi)
- **Plano unificado:** [[PLANO_OTA_RASPBERRY_PI_SERVIDOR_PROPRIO]] (`jukeeo-knowledge`)
- **POC .NET:** [[PLANO_JUKEBOX_OTA_AGENT_DOTNET_POC]] · plano local [[PLANO_POC_DOTNET_PI]]
- **Arquitetura relacionada:** [[SYNC_PROCESSO_SEPARADO]] · [[OBSERVABILIDADE_LOKI_PROMETHEUS]]

## Escopo deste repositório

- CLI: `version`, `check`, `verify`
- Cliente da API OTA (`/v1/updates/check`)
- Verificação `sha256` + assinatura RSA-PSS do manifesto
- Units systemd + timer (`packaging/systemd/`)
- Telemetria mínima (stdout → journald)

**Fora de escopo na POC:** swap de bundle, rollback, parar/iniciar serviços do kiosk.

**Outros repos:** app Flutter (`jukebox_tv`), servidor OTA (`jukebox-ota-server` — futuro).

## Desenvolvimento

```powershell
dotnet build
dotnet test
dotnet run --project src/Jukebox.Ota.Agent -- version
dotnet run --project src/Jukebox.Ota.Agent -- check --config tools/mock/ota-agent.example.json
```

## Publish para Raspberry Pi

```powershell
.\tools\deploy\publish-linux-arm64.ps1
```

Saída em `artifacts/linux-arm64/`. Deploy no Pi: `packaging/pi/README.md`.

## Documentação

- Índice do repo: `docs/index.md`
- Planos: `docs/plans/`
- How-to: `docs/howto/`
- Runbook do agente: `.cursor/napkin.md`

## Workspace Cursor

Abrir `C:\GitHub\jukebox\jukeeo.code-workspace` (inclui `jukeeo-knowledge`, `jukebox_tv` e este repo).
