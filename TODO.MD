# TODO – jukebox-ota-agent

Backlog curado **deste repositório**. Itens de fleet OTA partilhados com `jukebox_tv` (servidor, pipeline kiosk, TLS) permanecem espelhados em `jukebox_tv/TODO.md` § 6.

**Links (Obsidian):** notas em `docs/` → **`[[NOME]]`** (nome do ficheiro sem extensão). Produto canónico em `jukeeo-knowledge/docs/`. **Código** (`src/`, `tests/`, `tools/`, `packaging/`) → caminhos entre crases. **Linear** → `[JUK-n](url)`.

**Última revisão:** 2026-07-09 — criação do backlog local; sync Linear JUK-83 (auto-update do agente). POC fases 0–5 concluídas (JUK-62…JUK-70, espelhadas em `jukebox_tv/TODO.md` § 6).

**Legenda:** **☐** = pendente; **`- [x]`** = concluída. Linhas com **[JUK-n](url)** estão espelhadas no Linear.

---

## 1. POC .NET no Raspberry Pi (fases 0–5) — concluída

Contexto: [[PLANO_POC_DOTNET_PI]] · [[PLANO_JUKEBOX_OTA_AGENT_DOTNET_POC]] · decisão §7 **manter .NET** ([JUK-70](https://linear.app/jukeeo/issue/JUK-70)).

- [x] **[JUK-62](https://linear.app/jukeeo/issue/JUK-62)** **Fase 0 — scaffold .NET + CLI** — `version`/`check`/`verify`.
- [x] **[JUK-63](https://linear.app/jukeeo/issue/JUK-63)** **Fase 1 — manifesto + SHA-256 + RSA-PSS**.
- [x] **[JUK-64](https://linear.app/jukeeo/issue/JUK-64)** **Fase 2 — publish `linux-arm64` self-contained** — `tools/deploy/publish-linux-arm64.ps1`, `ota_deploy_to_pi.ps1`.
- [x] **[JUK-65](https://linear.app/jukeeo/issue/JUK-65)** **Fase 3 — systemd + journald** — `packaging/systemd/`.
- [x] **[JUK-66](https://linear.app/jukeeo/issue/JUK-66)** **Fase 4 — `check` contra mock HTTP LAN**.
- [x] **[JUK-67](https://linear.app/jukeeo/issue/JUK-67)** **Fase 5 — medições no Pi** — tamanho, RAM, startup.
- [x] **[JUK-68](https://linear.app/jukeeo/issue/JUK-68)** **systemd `SuccessExitStatus=0 2`**.
- [x] **[JUK-69](https://linear.app/jukeeo/issue/JUK-69)** **Habilitar timer após validação** — `pi_install_ota.sh --enable-timer`.
- [x] **[JUK-70](https://linear.app/jukeeo/issue/JUK-70)** **Avaliar critérios §7 — manter .NET vs Go**.

---

## 2. Roadmap pós-POC (agente + dependências fleet)

Itens que tocam este repo mas são geridos no backlog unificado OTA (`jukebox_tv/TODO.md` § 6). Referência cruzada — não duplicar fecho manual aqui sem sync Linear.

☐ **[JUK-71](https://linear.app/jukeeo/issue/JUK-71)** **Pipeline off-device** — tarball kiosk em `jukebox_tv` `tools/ota/` (fora deste repo).
☐ **[JUK-72](https://linear.app/jukeeo/issue/JUK-72)** **Servidor OTA mínimo** — `jukebox-ota-server`; contrato em `docs/API.md`.
☐ **[JUK-73](https://linear.app/jukeeo/issue/JUK-73)** **Conexão segura Agent ↔ Server** — HTTPS, auth dispositivo, `HttpOtaUpdateClient`.
☐ **[JUK-80](https://linear.app/jukeeo/issue/JUK-80)** **Rollback robusto em falhas de `apply`** — matriz `error_code`, `rollback_failed`, runbook.

- [x] **Preservar logs entre updates OTA** — logs em `~/.local/share/com.jukeeo.kiosk/logs/`; ver handoff em `jukebox_tv` [[HANDOFF_OTA_JUKEEO_PI]].

---

## 3. Auto-update do próprio binário do agente (pós-v1)

Contexto: [[BRAINSTORM_AUTO_UPDATE_AGENTE]] · [[PLANO_OTA_RASPBERRY_PI_SERVIDOR_PROPRIO]] (decisão v1: fora de escopo; provisionamento manual).

**Estado v1:** o agente actualiza o kiosk (`/opt/jukeeo/releases/`), não a si mesmo (`/opt/jukeeo/ota-agent/`). Deploy manual: `tools/deploy/ota_deploy_to_pi.ps1` → `pi_install_ota.sh`.

☐ **[JUK-83](https://linear.app/jukeeo/issue/JUK-83)** **Auto-update do binário do agente** — definir estratégia (artefacto OTA dedicado, bootstrap, mesh + deploy remoto ou híbrido), critérios de rollback e implementação pós-[JUK-72](https://linear.app/jukeeo/issue/JUK-72)/[JUK-73](https://linear.app/jukeeo/issue/JUK-73).

---

## 4. Segurança e endurecimento (repo)

☐ **[[PLANO_SEGURANCA_OTA_PENDENCIAS]]** — pendências documentadas (verify obrigatório, chave pública no Pi, servidor).
