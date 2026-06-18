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
