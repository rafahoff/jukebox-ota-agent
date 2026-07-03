# Glossário — jukebox-ota-agent

Atalho local. Glossário de produto canônico: `../jukeeo-knowledge/CONTEXT.md`.

## Agente OTA

Processo **separado** do app Flutter (`jukebox_tv`) e do sync agent. Responsável por check de versão, verificação de pacote, apply em `/opt/jukeeo/releases/`, health check, rollback e `ack` ao servidor.

Auto-update do próprio agente: **fora de escopo v1** (provisionamento manual via `ota_deploy_to_pi.ps1`).

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
| `jukebox-ota-server-dev` | Servidor OTA paliativo de desenvolvimento (Docker) |
| *(futuro)* `jukebox-ota-server` | API de rollout produção (JUK-72) |
| `jukeeo-knowledge` | Brainstorm e decisões de produto |

Plano de execução: `jukebox_tv/docs/plans/PLANO_OTA_EXECUCAO_PI.md`. UI e estado partilhado: `jukebox_tv/docs/plans/PLANO_OTA_UI_SETTINGS.md` (ADR 0001). Check/download vs apply: `docs/plans/PLANO_OTA_CHECK_DOWNLOAD_APPLY.md` (ADR 0002).

## ota_update_status

Ficheiro `ota_update_status.json` em `kiosk_data_dir` (`~/.local/share/com.jukeeo.kiosk/`), escrito pelo agente e lido pelo kiosk Flutter. Contrato de estado OTA visível na UI (versão remota, disponibilidade de update, fase corrente, última verificação). Ver ADR 0001.

## phase

Campo `phase` do `ota_update_status.json`: estado do ciclo OTA. Valores: `idle`, `checking`, `downloading`, `ready_to_apply`, `applying`, `error` (ADR 0002). A fase `update_available` (ADR 0001) está obsoleta. O kiosk usa `phase` para feedback na UI e grace period; o agente é a única fonte de escrita.

## ready_to_apply

Fase em que o pacote OTA foi descarregado e validado em cache local, aguardando o comando `upgrade` (apply). O kiosk inicia a contagem de grace period nesta fase após `check` automático. Ver ADR 0002.

## Comando check

Subcomando CLI `jukebox-ota-agent check --config <arquivo> [--force]`: verificação HTTP; se houver update, download e verificação do pacote até `ready_to_apply`. O timer systemd invoca apenas `check` (ADR 0002).

## Comando upgrade

Subcomando CLI `jukebox-ota-agent upgrade --config <arquivo> [--force]`: **apenas apply** do pacote em cache e restart do kiosk — sem novo download (ADR 0002). Disparo manual a partir do Pi via `systemd-run`; download permanece exclusivo do agente durante `check`.
