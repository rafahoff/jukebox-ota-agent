# Plano — POC `jukebox-ota-agent` em .NET no Raspberry Pi

- **Status:** em execução
- **Data:** 2026-06-12
- **Escopo:** validar agente .NET self-contained no Pi antes de swap real de bundles.

**Origem:** promovido de [[PLANO_JUKEBOX_OTA_AGENT_DOTNET_POC]] · contexto [[PLANO_OTA_RASPBERRY_PI_SERVIDOR_PROPRIO]].

## Decisões fechadas (bootstrap)

| Tópico | Decisão |
|--------|---------|
| Chave pública | PEM em `public_key_path` |
| Objeto assinado | Manifesto JSON canónico |
| API mock na POC | `file://` ou HTTPS estático |
| Modo de execução | Timer systemd (oneshot) |
| Auto-update do agente | Fora do escopo da POC |

## Fases

| Fase | Entrega | Critério de saída | Estado |
|------|---------|-------------------|--------|
| 0 | Scaffold .NET + `version`/`check` | Build e testes no Windows/WSL | **concluída** (bootstrap) |
| 1 | Manifesto + `sha256` + RSA-PSS | Testes assinatura válida/inválida | em progresso |
| 2 | Publish `linux-arm64` self-contained | Artefato roda `version` no Pi | pendente |
| 3 | systemd + journald | `SyslogIdentifier=jukebox-ota` | pendente |
| 4 | `check` contra API mock HTTPS | Telemetria registada | pendente |
| 5 | Medições no Pi | Tamanho, RAM, startup documentados | pendente |

## CLI

```text
jukebox-ota-agent version
jukebox-ota-agent check --config /etc/jukebox/ota-agent.json
jukebox-ota-agent verify --manifest manifest.json --package package.tar.zst [--public-key chave.pem]
```

## Fora de escopo

- Swap de `/opt/jukebox_tv/current`
- Rollback automático
- Parar/iniciar `jukebox_tv_flutterpi.service` ou sync agent

## Critérios para manter .NET

Ver [[PLANO_JUKEBOX_OTA_AGENT_DOTNET_POC]] §7. Reavaliar Go se dois ou mais critérios falharem sem correção simples.
