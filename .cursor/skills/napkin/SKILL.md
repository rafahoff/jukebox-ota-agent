---
name: napkin
description: >-
  Mantém o runbook em `.cursor/napkin.md` para o jukebox-ota-agent (.NET, Pi, systemd, publish linux-arm64).
  Aplicar no início de sessão neste repo e ao aprender pegas de deploy, journald ou verificação OTA.
disable-model-invocation: false
---

# Napkin — jukebox-ota-agent

Manter **`.cursor/napkin.md`** como runbook curado (português do Brasil).

## Início da sessão

Ler `.cursor/napkin.md`, curar na hora (máx. 10 itens/categoria) e aplicar em silêncio.

## O que registrar

- Comandos `dotnet publish` que falharam ou surpresas no Pi
- Pegas de `systemd`/`journalctl`
- Formato de manifesto ou assinatura RSA-PSS
- Tamanho de artefato e medições de RAM/startup (fase 5 da POC)

## Planos

Criar em `docs/plans/`; concluídos em `docs/plans/archive/`. Brainstorm de produto em `jukeeo-knowledge`.

## Não usar napkin para

- Backlog de features do kiosk (`jukebox_tv/TODO.MD`)
- Duplicar [[PLANO_JUKEBOX_OTA_AGENT_DOTNET_POC]] inteiro
