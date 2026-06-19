# ADR 0001 — Estado OTA partilhado (`ota_update_status.json`) e comando `upgrade`

**Status:** aceito  
**Data:** 2026-06-19

## Contexto

O kiosk Flutter (`jukebox_tv`) e o agente OTA (`jukebox-ota-agent`) correm como processos separados no Raspberry Pi (flutter-pi). O operador precisa de:

- ver na UI de definições se há actualização disponível e qual a versão remota;
- disparar manualmente uma verificação ou uma actualização completa;
- manter o agente como único componente com privilégios para download, verificação de pacote, swap de release e restart do serviço kiosk.

Hoje o agente persiste apenas `last_check_at_ms` em `/var/lib/jukebox-ota/` e não expõe estado legível ao Flutter. As definições OTA no kiosk (`ota_check_enabled`, intervalo, janela horária) já vivem em `machine_config` no SQLite, mas não há contrato de leitura cruzada para fase da actualização, versão remota ou erros.

Foi realizada sessão **grill** (2026-06-19) para fechar o contrato entre repos sem duplicar lógica de download no Flutter.

## Decisão

### 1. Ficheiro de estado partilhado

| Aspecto | Valor |
|---------|-------|
| Caminho | `{kiosk_data_dir}/ota_update_status.json` |
| `kiosk_data_dir` | `~/.local/share/com.jukeeo.kiosk/` (mesmo valor de `kiosk_data_dir` em `/etc/jukeeo/ota-agent.json`) |
| Escritor | Agente OTA (único) |
| Leitor | Kiosk Flutter (Pi / flutter-pi) |
| Persistência | Writes atómicos: escrever em ficheiro temporário no mesmo directório + `rename` |

**Schema v1** (`schema_version: 1`):

| Campo | Tipo | Descrição |
|-------|------|-----------|
| `schema_version` | int | Sempre `1` na v1 |
| `phase` | string | `idle` \| `checking` \| `update_available` \| `downloading` \| `applying` \| `error` |
| `checked_at_ms` | int \| null | Epoch ms da última verificação HTTP concluída com sucesso (com ou sem update) |
| `current_version` | string | Versão instalada no momento da escrita |
| `remote_version` | string \| null | Versão oferecida pelo servidor; `null` se indisponível |
| `update_available` | bool | `true` quando há pacote mais recente elegível |
| `error_message` | string \| null | Mensagem legível quando `phase=error`; `null` caso contrário |

O agente actualiza o JSON em cada transição relevante (`check`, download, `apply`, erro recuperável).

### 2. Unificar intervalo de verificação

- `checked_at_ms` no JSON **substitui** o ficheiro `last_check_at_ms` em `state_directory` (`/var/lib/jukebox-ota/`).
- Na **primeira execução** após deploy desta mudança, o agente faz migração one-shot: se existir `last_check_at_ms` e `checked_at_ms` estiver ausente no JSON, copia o valor e deixa de ler o ficheiro legado.
- A política de intervalo mínimo do comando `check` passa a usar `checked_at_ms` do JSON.

### 3. CLI do agente

| Comando | Sintaxe | Comportamento |
|---------|---------|---------------|
| `check` | `check --config <arquivo.json> [--force]` | Verificação HTTP; actualiza JSON; exit **2** se há update (inalterado) |
| `upgrade` | `upgrade --config <arquivo.json> [--force]` | Orquestração: `check` → download → `apply` → restart kiosk; **novo** |

- **Download** de pacotes OTA permanece **exclusivo** do agente; o Flutter nunca descarrega blobs.
- `upgrade` reutiliza os serviços internos de `check` e `apply` existentes.

### 4. Semântica de `--force`

`--force` em `check` ou `upgrade` faz **bypass total** de:

- intervalo mínimo (`checked_at_ms`);
- janela horária (`ota_check_window_start` / `ota_check_window_end`);
- `ota_check_enabled=false` em `machine_config`.

O toggle **«Verificar atualizações automaticamente»** no kiosk continua a afectar **apenas** o timer systemd (`jukebox-ota-check.timer`); não impede disparo manual com `--force` nem leitura do JSON.

### 5. Disparo a partir do kiosk

| Acção UI | Mecanismo | Notas |
|----------|-----------|-------|
| Verificar agora | `Process.run` → `jukebox-ota-agent check --config … --force` | Timeout **3 min**; abortar e reflectir erro na UI se expirar |
| Actualizar agora | `systemd-run` → `jukebox-ota-agent upgrade --config … --force` | Fire-and-forget; progresso via JSON (`phase`) |

### 6. UI no kiosk (referência cruzada)

Decisão de produto registada aqui para alinhamento entre repos; implementação em `jukebox_tv` (ver [[PLANO_OTA_UI_SETTINGS]]):

- **Versão instalada:** `AppVersionUtil` (fonte local do bundle em execução).
- **Versão remota / pendente:** campos `remote_version` e `update_available` do JSON.
- Banner e botão «Actualizar» só quando `update_available=true`.
- Secção OTA na UI de definições: **apenas** em builds Pi (flutter-pi), não Android TV nem Windows.

## Alternativas rejeitadas

| Alternativa | Motivo da rejeição |
|-------------|-------------------|
| Novas chaves em `machine_config` para fase/versão remota | SQLite é escrito pelo sync/kiosk; misturar estado volátil do agente aumenta contenção e risco de escrita concorrente; o agente já precisa de ficheiro para `checked_at_ms` unificado |
| Parse de logs do agente (`journalctl`) no Flutter | Frágil, acoplado a formato de log, difícil de testar; não expõe estado estruturado |
| `check` HTTP directo no Flutter contra o servidor OTA | Duplica política, credenciais e contrato OTA; viola fronteira agente↔kiosk; download continuaria no agente, gerando duas fontes de verdade |

## Consequências

### Positivas

- Contrato estável e versionado (`schema_version`) entre dois processos sem partilhar memória.
- Uma única fonte para «última verificação» (`checked_at_ms`), simplificando política e UI.
- `upgrade` oferece fluxo operador único sem expor `apply` + manifesto ao kiosk.
- Flutter permanece sem privilégios de download nem swap de `/opt/jukeeo/current`.

### Negativas / riscos

- Leitura de ficheiro JSON no hot path da UI exige polling ou `FileWatcher` com tolerância a leituras durante rename atómico.
- Migração one-shot deve ser idempotente; Pis antigos com só `last_check_at_ms` precisam de um ciclo de agente actualizado.
- `systemd-run` para `upgrade` requer unidade/template ou permissões adequadas no utilizador do kiosk (validar em piloto).

### Seguintes passos

- Plano de execução: `jukebox_tv/docs/plans/PLANO_OTA_UI_SETTINGS.md` (tarefas Agente A + Agente B).
- Actualizar `docs/API.md` do agente com schema JSON e comando `upgrade` após implementação.
- Testes de integração: escrita atómica, migração `last_check_at_ms`, fluxo `upgrade` com mock `file://`.

## Ver também

- `jukebox_tv/docs/plans/PLANO_OTA_EXECUCAO_PI.md` — layout `/opt/jukeeo`, apply, rollback
- `docs/API.md` — contrato HTTP e política `machine_config` actual
- [[PLANO_OTA_RASPBERRY_PI_SERVIDOR_PROPRIO]] — visão de produto OTA
