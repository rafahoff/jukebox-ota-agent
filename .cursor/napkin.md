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

- [2026-06-17] **Mock OTA HTTP (LAN)**
  Faça assim: no Windows, `python tools/mock/ota_mock_server.py --mode auto`; no Pi, `ota_base_url` `http://<IP-Windows>:8080` (modelo `tools/mock/ota-agent.pi-lan.example.json`). Ver `docs/howto/FASE4_MOCK_LAN.md`.

## Shell e confiabilidade

- [2026-06-12] **CLI mínima**
  Faça assim: `dotnet run --project src/Jukebox.Ota.Agent -- version|check|verify`; binário publicado chama-se `jukebox-ota-agent`.

## Pi e systemd

- [2026-06-12] **Logs separados do kiosk**
  Faça assim: unit com `SyslogIdentifier=jukebox-ota`; validar com `journalctl -t jukebox-ota`.

- [2026-06-12] **Timer, não daemon residente**
  Faça assim: `jukebox_ota_agent.service` tipo `oneshot` + `jukebox_ota_agent.timer` (6h); reduz RAM idle no Pi.

## Segurança

- [2026-06-18] **Chave pública no Pi**
  Faça assim: chave pública **não é segredo** — o risco é troca do PEM ou do binário com root; ver [[PLANO_SEGURANCA_OTA_PENDENCIAS]] §3 (user dedicado, fingerprint embutido, RO rootfs).

- [2026-06-12] **Chaves PEM**
  Faça assim: chave privada **nunca** no Pi; só `public_key_path` em `/etc/jukebox/`; não versionar `*.pem` (ver `.gitignore`).
