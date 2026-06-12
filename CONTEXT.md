# Glossário — jukebox-ota-agent

Atalho local. Glossário de produto canônico: `../jukeeo-knowledge/CONTEXT.md`.

## Agente OTA

Processo **separado** do app Flutter (`jukebox_tv`) e do sync agent. Responsável por check de versão, verificação de pacote e (futuro) swap de bundle no Pi.

## device_id

Identificador único do quiosque na API OTA (`machine-001`, etc.).

## channel

Canal de rollout (`stable`, `beta`). Definido no servidor OTA.

## Manifesto OTA

JSON com `version`, `sha256`, `signature_b64` e metadados. A assinatura RSA-PSS cobre o manifesto canónico; o pacote é validado por hash.

## Fronteiras

| Repo | Papel |
|------|-------|
| `jukebox-ota-agent` | Binário no dispositivo |
| `jukebox_tv` | App kiosk + empacotamento tarball |
| `jukebox-ota-server` | API de rollout (futuro) |
| `jukeeo-knowledge` | Brainstorm e decisões de produto |
