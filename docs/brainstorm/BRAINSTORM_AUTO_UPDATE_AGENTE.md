# Brainstorm — auto-update do binário `jukebox-ota-agent`

- **Status:** discussão aberta
- **Data:** 2026-07-09
- **Escopo:** como actualizar o **próprio agente OTA** em fleet, sem depender de visita física ao Pi
- **Issue Linear:** [JUK-83](https://linear.app/jukeeo/issue/JUK-83)
- **Backlog:** `TODO.MD` § 3

**Produto canónico:** [[PLANO_OTA_RASPBERRY_PI_SERVIDOR_PROPRIO]] · [[PLANO_JUKEBOX_OTA_AGENT_DOTNET_POC]] §9

---

## 1. Problema

O `jukebox-ota-agent` é o processo que verifica, descarrega e aplica updates do **app kiosk** (bundle flutter-pi em `/opt/jukeeo/releases/`). Porém, o binário do agente em `/opt/jukeeo/ota-agent/` **não participa** desse ciclo OTA.

Na v1, qualquer correcção ou evolução do agente exige **provisionamento manual** a partir da máquina de desenvolvimento:

```powershell
.\tools\deploy\ota_deploy_to_pi.ps1 -PiHost <IP>
```

Isto escala mal em fleet e contradiz o objectivo de operação remota — mas foi uma decisão consciente para reduzir risco na POC (problema clássico do «quem actualiza o actualizador»).

---

## 2. Auditoria (2026-07-09)

| Fonte | O que diz |
|-------|-----------|
| `README.MD` | Auto-update do binário listado como **fora deste repo / futuro** |
| `CONTEXT.md` | **Fora de escopo v1**; manual via `ota_deploy_to_pi.ps1` |
| `jukeeo-knowledge` [[PLANO_OTA_RASPBERRY_PI_SERVIDOR_PROPRIO]] | Decisão fechada 2026-06-17: adiado |
| `jukebox_tv/TODO.md` § 6 | Pendências JUK-71…80; **nenhuma** sobre self-update do agente |
| Linear (projeto OTA) | **Nenhuma issue** até criação de [JUK-83](https://linear.app/jukeeo/issue/JUK-83) |

---

## 3. Arquitectura actual (v1)

```text
Windows dev                    Raspberry Pi
───────────                    ────────────
ota_deploy_to_pi.ps1  ─SSH─►  /opt/jukeeo/ota-agent/     ← binário agente (manual)
                               /opt/jukeeo/releases/      ← bundles kiosk (OTA)
                               /opt/jukeeo/current → symlink

Timer systemd (jukebox_ota_agent.timer)
  └─► upgrade ─► check + apply do KIOSK apenas
```

| Componente | Caminho | Quem actualiza |
|------------|---------|----------------|
| Agente OTA | `/opt/jukeeo/ota-agent/` | Operador (`ota_deploy_to_pi.ps1`) |
| App kiosk | `/opt/jukeeo/releases/<versão>/` + `current` | Agente (`check` / `upgrade` / `apply`) |
| Config agente | `/etc/jukeeo/ota-agent.json` | Provisionamento / sync config (futuro) |

**Contrato API actual** (`docs/API.md`): `app: "jukeeo"`, `package_type: "full"` — tarball do bundle flutter-pi. Não há canal para o binário .NET do agente.

---

## 4. Restrições e riscos

1. **Processo a substituir-se a si mesmo** — em Linux, não se pode sobrescrever o executável em execução; precisa de staging + swap atómico ou processo auxiliar.
2. **Binário .NET self-contained** — artefacto grande (~dezenas de MB); download e verificação devem reutilizar padrões sha256 + RSA-PSS já existentes.
3. **Utilizador `jukebox-ota`** — permissões restritas; swap em `/opt/jukeeo/ota-agent/` deve respeitar ownership e systemd units.
4. **Falha durante update do agente** — se o agente morrer a meio, o kiosk pode ficar sem OTA até recuperação manual; rollback do binário é obrigatório.
5. **Dependências fleet** — servidor OTA produção ([JUK-72](https://linear.app/jukeeo/issue/JUK-72)) e TLS ([JUK-73](https://linear.app/jukeeo/issue/JUK-73)) são pré-requisitos realistas para auto-update remoto seguro.

---

## 5. Opções em discussão

### Opção A — Novo artefacto no servidor OTA

Publicar releases do agente no mesmo servidor, com identificador distinto:

| Campo | Proposta |
|-------|----------|
| `app` | `jukebox-ota-agent` (ou extensão do contrato actual) |
| `package_type` | `agent` |
| Destino no Pi | `/opt/jukeeo/ota-agent/` (staging → swap) |

**Prós:** reutiliza `check`, assinatura, URLs pré-assinadas, telemetria `ack`.  
**Contras:** o agente precisa de lógica separada para «update de mim» vs «update do kiosk»; risco de confundir os dois ciclos.

### Opção B — Bootstrap mínimo (shell + systemd)

Manter um script pequeno (`/opt/jukeeo/ota-agent/bootstrap.sh` ou unit `jukebox_ota_agent_update.service`) que:

1. Consulta API por versão do agente
2. Descarrega tarball do binário para staging
3. Verifica hash + assinatura
4. Para o timer, troca binário, reinicia unit, valida `jukebox-ota-agent version`

O binário .NET grande **não** se auto-substitui — só o bootstrap faz o swap.

**Prós:** separação clara; bootstrap simples de auditar; recuperação mais previsível.  
**Contras:** dois componentes para manter; bootstrap também precisa de update eventual (raro).

### Opção C — Deploy remoto via mesh (continua «manual»)

Usar NetBird ([JUK-75](https://linear.app/jukeeo/issue/JUK-75)…[JUK-79](https://linear.app/jukeeo/issue/JUK-79)) para SSH overlay e executar `ota_deploy_to_pi.ps1` do operador sem estar na LAN do bar.

**Prós:** zero código novo no agente; reutiliza fluxo validado.  
**Contras:** não é auto-update; depende de operador; não escala para rollout silencioso.

### Opção D — Híbrido (recomendação preliminar para discussão)

| Fase | Mecanismo |
|------|-----------|
| Provisionamento inicial | `ota_deploy_to_pi.ps1` (como hoje) |
| Updates frequentes do agente | Opção A ou B via servidor OTA |
| Recuperação / incidentes | `ota_deploy_to_pi.ps1` ou SSH via mesh |
| Updates do kiosk | Ciclo actual (`upgrade`) — inalterado |

---

## 6. Critérios de decisão (rascunho)

- [ ] Ciclo do kiosk **não** interrompido durante update do agente (ou janela explícita documentada)
- [ ] Rollback automático do binário se `version` ou health falhar após swap
- [ ] Mesma política de assinatura (RSA-PSS + sha256) que o kiosk
- [ ] Compatível com `jukebox-ota` + sudoers actuais
- [ ] Runbook de recuperação manual em `docs/howto/`
- [ ] Testes em lab (Pi `192.168.15.100`) antes de fleet

---

## 7. Próximos passos sugeridos

1. Fechar opção (A, B ou D) em ADR dedicado quando [JUK-72](https://linear.app/jukeeo/issue/JUK-72) estiver em piloto.
2. Estender `docs/API.md` com contrato do artefacto `agent` (se Opção A/D).
3. Protótipo em lab: update 0.0.1 → 0.0.2 do agente sem `ota_deploy_to_pi.ps1`.
4. Actualizar `README.MD` e `CONTEXT.md` quando sair de «fora de escopo v1».

---

## 8. Referências

| Recurso | Caminho |
|---------|---------|
| Deploy manual v1 | `docs/howto/DEPLOY_PI.md` |
| Contrato HTTP | `docs/API.md` |
| Install no Pi | `tools/deploy/pi_install_ota.sh` |
| Backlog local | `TODO.MD` § 3 |
| Backlog fleet | `jukebox_tv/TODO.md` § 6 |
