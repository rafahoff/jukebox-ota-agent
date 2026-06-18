# Glossário — jukebox-ota-agent

Atalho local. Glossário de produto canônico: `../jukeeo-knowledge/CONTEXT.md`.

## Agente OTA

Processo **separado** do app Flutter (`jukebox_tv`) e do sync agent. Responsável por check de versão, verificação de pacote, apply em `/opt/jukeeo/releases/`, health check, rollback e `ack` ao servidor.

Auto-update do próprio agente: **fora de escopo v1** (provisionamento manual via `deploy_to_pi.ps1`).

## device_id

Identificador único do quiosque na API OTA (`machine-001`, etc.).

## channel

Canal de rollout (`stable`, `beta`). Definido no servidor OTA e em `/etc/jukeeo/ota-agent.json`.

## Manifesto OTA

JSON com `app: jukeeo`, `version`, `package_type`, `sha256`, `signature_b64` e metadados. Assinatura RSA-PSS cobre o manifesto canónico; o pacote é validado por hash.

## Release OTA

Pacote `jukeeo-<versão>+aarch64.tar.zst` com bundle flutter-pi completo (v1: só `full`).

## Fronteiras

| Repo | Papel |
|------|-------|
| `jukebox-ota-agent` | Binário em `/opt/jukeeo/ota-agent/` |
| `jukebox_tv` | App kiosk + `tools/ota/` (empacotamento) |
| `jukebox-ota-server` | API de rollout (futuro) |
| `jukeeo-knowledge` | Brainstorm e decisões de produto |

Plano de execução: `jukebox_tv/docs/plans/PLANO_OTA_EXECUCAO_PI.md`.
