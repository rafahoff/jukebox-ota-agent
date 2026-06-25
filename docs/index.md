# MOC — jukebox-ota-agent

Ponto de entrada para agentes neste repositório.

## Produto (irmão)

- `../jukeeo-knowledge/docs/index.md`
- [[PLANO_OTA_RASPBERRY_PI_SERVIDOR_PROPRIO]]
- [[PLANO_JUKEBOX_OTA_AGENT_DOTNET_POC]]

## Repo

| Nota | Tema |
|------|------|
| [[PLANO_POC_DOTNET_PI]] | POC .NET no Raspberry Pi (fases 0–5) |
| [[PLANO_SEGURANCA_OTA_PENDENCIAS]] | Pendências de segurança — servidor, verify, chave pública no Pi |
| [[PLANO_OTA_CHECK_DOWNLOAD_APPLY]] | `check` com download, `upgrade` só apply, grace period (ADR 0002) |
| `docs/adr/0002-ota-check-download-e-grace-period-kiosk.md` | ADR — fase `ready_to_apply`, timer só `check` |
| `jukebox_tv/docs/plans/PLANO_OTA_GRACE_PERIOD_POPUP.md` | Overlay grace period no kiosk ([[PLANO_OTA_GRACE_PERIOD_POPUP]]) |
| `jukebox_tv/docs/plans/PLANO_OTA_EXECUCAO_PI.md` | Execução OTA: `/opt/jukeeo`, apply, GC, ack ([[PLANO_OTA_EXECUCAO_PI]]) |
| `docs/API.md` | Contrato HTTP (`jukeeo`, ack completo); backup pré-update no `apply` |
| `docs/howto/DEPLOY_PI.md` | Deploy self-contained no Pi |
| `packaging/pi/README.md` | Checklist systemd |

## Código

| Caminho | Papel |
|---------|-------|
| `src/Jukebox.Ota.Agent/Domain/` | Entidades, contratos |
| `src/Jukebox.Ota.Agent/Application/` | Casos de uso |
| `src/Jukebox.Ota.Agent/Infrastructure/` | HTTP, config, criptografia |
| `src/Jukebox.Ota.Agent/Interfaces/Cli/` | CLI |
| `tests/Jukebox.Ota.Agent.Tests/` | xUnit |
| `tools/deploy/` | Publish `linux-arm64` |

## Runbook

- `.cursor/napkin.md`
- `.cursor/knowledge.manifest.md`
