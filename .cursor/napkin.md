# Napkin — runbook

Runbook curado do **jukebox-ota-agent**. Idioma: português (Brasil). Skill: `.cursor/skills/napkin/SKILL.md`.

## Regras de curadoria

- Reordenar a cada leitura por importância.
- Máx. 10 itens por categoria.
- Cada item: data `[YYYY-MM-DD]` + **Faça assim:**

## Execução e validação (prioridade máxima)

- [2026-06-12] **Build e testes locais**
  Faça assim: `dotnet build` e `dotnet test` na raiz antes de publicar para o Pi.

- [2026-06-12] **Publish Pi (self-contained)**
  Faça assim: `.\tools\deploy\publish-linux-arm64.ps1` → saída em `artifacts/linux-arm64/`; não instalar runtime .NET no Pi para a POC.

- [2026-06-12] **Mock OTA local**
  Faça assim: `ota_base_url` com `file://` + caminho absoluto do manifesto JSON; `check --config` retorna código `2` quando há versão mais nova.

## Shell e confiabilidade

- [2026-06-12] **CLI mínima**
  Faça assim: `dotnet run --project src/Jukebox.Ota.Agent -- version|check|verify`; binário publicado chama-se `jukebox-ota-agent`.

## Pi e systemd

- [2026-06-12] **Logs separados do kiosk**
  Faça assim: unit com `SyslogIdentifier=jukebox-ota`; validar com `journalctl -t jukebox-ota`.

- [2026-06-12] **Timer, não daemon residente**
  Faça assim: `jukebox_ota_agent.service` tipo `oneshot` + `jukebox_ota_agent.timer` (6h); reduz RAM idle no Pi.

## Segurança

- [2026-06-12] **Chaves PEM**
  Faça assim: chave privada **nunca** no Pi; só `public_key_path` em `/etc/jukebox/`; não versionar `*.pem` (ver `.gitignore`).
