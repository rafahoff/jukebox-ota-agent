# jukebox-ota-agent

Agente de atualização over-the-air (OTA) para quiosques Jukeeo — processo **separado** do app Flutter (`jukebox_tv`) e do sync agent.

- **Linguagem:** .NET 8 (`linux-arm64` self-contained no Pi)
- **POC .NET:** concluída (fases 0–5); decisão §7 **manter .NET** ([JUK-70](https://linear.app/jukeeo/issue/JUK-70))
- **Plano unificado:** [[PLANO_OTA_RASPBERRY_PI_SERVIDOR_PROPRIO]] (`jukeeo-knowledge`)
- **POC local:** [[PLANO_POC_DOTNET_PI]] · brainstorm [[PLANO_JUKEBOX_OTA_AGENT_DOTNET_POC]]
- **Execução OTA no Pi:** `jukebox_tv/docs/plans/PLANO_OTA_EXECUCAO_PI.md` ([[PLANO_OTA_EXECUCAO_PI]])
- **Arquitetura relacionada:** [[SYNC_PROCESSO_SEPARADO]] · [[OBSERVABILIDADE_LOKI_PROMETHEUS]]

## Escopo deste repositório

| Área | Capacidades |
|------|-------------|
| **CLI** | `version`, `check`, `verify`, `sign-manifest`, `apply` |
| **API OTA** | Cliente `GET /v1/updates/check` e `POST /v1/updates/ack` |
| **Segurança** | Verificação `sha256` + assinatura RSA-PSS do manifesto |
| **Apply** | Stop kiosk, backup SQLite/SharedPreferences, swap em `/opt/jukeeo/current`, health check, rollback automático |
| **Runtime Pi** | Utilizador `jukebox-ota`, units systemd + timer (`upgrade` automático), sudoers restrito para `systemctl` do kiosk |
| **Observabilidade** | Stdout → journald (`SyslogIdentifier=jukebox-ota`); log opcional em ficheiro |

**Fora deste repo (backlog ou futuro):** servidor OTA produção JUK-72 (`jukebox-ota-server`), pipeline fleet off-device ([JUK-71](https://linear.app/jukeeo/issue/JUK-71)), TLS agente↔servidor ([JUK-73](https://linear.app/jukeeo/issue/JUK-73)), auto-update do próprio binário do agente.

**Servidor OTA de desenvolvimento:** [`jukebox-ota-server-dev`](https://github.com/rafahoff/jukebox-ota-server-dev) (Docker, paliativo para testes).

**Repo irmão:** app Flutter [`jukebox_tv`](https://github.com/rafahoff/jukebox_tv).

## Testar o servidor OTA manualmente

O servidor paliativo [`jukebox-ota-server-dev`](https://github.com/rafahoff/jukebox-ota-server-dev) escuta na porta **9080** (Docker local ou LAN). Use estes passos para confirmar que o processo está no ar **antes** de rodar `check` no agente ou no Pi.

### Liveness (`GET /health`)

Endpoint dedicado ao **ping** — não faz parte do contrato de update, só confirma que o HTTP responde.

| Aspecto | Valor |
|---------|-------|
| **Rota** | `GET /health` |
| **Resposta** | `200` com corpo `{"status":"ok"}` |

```powershell
# Servidor no mesmo host (Docker local)
curl -i "http://127.0.0.1:9080/health"

# Servidor na LAN (substituir pelo IP do PC/VPS visível ao Pi)
curl -i "http://192.168.15.129:9080/health"
```

Saída esperada (resumo): `HTTP/1.0 200 OK` e `{"status":"ok"}`.

### Consulta de update (`GET /v1/updates/check`)

Valida o **contrato real** consumido pelo agente (mesma rota que `ota_base_url` + `/v1/updates/check`).

**Query:** `device_id`, `channel`, `version` (versão actual do dispositivo), `arch` (opcional; default `aarch64` no server dev).

| Código | Significado |
|--------|-------------|
| `204` | Sem actualização (`version` ≥ versão publicada no canal) |
| `200` | JSON do manifesto — update disponível |
| `4xx` | Canal/arch inválido ou índice sem release |

```powershell
curl -i "http://127.0.0.1:9080/v1/updates/check?device_id=pi-001&channel=beta&version=0.0.0&arch=aarch64"
```

Com release publicada em `beta` e `version` inferior à do índice, a resposta traz o manifesto JSON (ver [`docs/API.md`](docs/API.md)).

### Via CLI do agente

Com `ota_base_url` apontando para o servidor (ex.: `http://192.168.15.129:9080` no JSON do Pi ou num config local). Exemplo LAN: `tools/mock/ota-agent.pi-100.json`.

```powershell
dotnet run --project src/Jukebox.Ota.Agent -- check --config tools/mock/ota-agent.pi-100.json
```

| Exit code | Significado |
|-----------|-------------|
| `0` | Sem update |
| `2` | Update disponível |
| `1` | Erro (servidor inacessível, TLS, resposta inválida, etc.) |

No Pi: `sudo -u jukebox-ota /opt/jukeeo/ota-agent/jukebox-ota-agent check --config /etc/jukeeo/ota-agent.json`.

### Subir o server dev e simulação LAN

```bash
# jukebox-ota-server-dev
cp .env.example .env   # OTA_PUBLIC_BASE_URL=http://<IP-LAN>:9080 se o Pi aceder pela rede
cd tools/deploy && docker compose up --build
```

How-tos: [`jukebox-ota-server-dev/docs/howto/simulacao-lan.md`](https://github.com/rafahoff/jukebox-ota-server-dev/blob/main/docs/howto/simulacao-lan.md) · [`docs/howto/FASE4_MOCK_LAN.md`](docs/howto/FASE4_MOCK_LAN.md) (VPS).

## CLI

```text
jukebox-ota-agent version
jukebox-ota-agent check --config <arquivo.json>
jukebox-ota-agent verify --manifest <manifest.json> --package <pacote.tar.zst> [--public-key <chave.pem>]
jukebox-ota-agent sign-manifest --manifest <in.json> --private-key <pem> [--output <out.json>]
jukebox-ota-agent apply --config <json> --manifest <json> --package <pacote.tar.zst>
```

| Comando | Códigos de saída relevantes |
|---------|----------------------------|
| `check` | `0` sem update · `2` update disponível · `1` erro |
| `apply` | `0` sucesso · `1` falha (com ack ao servidor quando configurado) |

Config de referência: `tools/mock/ota-agent.example.json` · contrato HTTP: `docs/API.md`.

## Backup pré-update (SQLite)

Durante o `apply`, o agente copia um snapshot dos dados persistentes do kiosk **depois** de parar o serviço e **antes** de extrair a nova release. Implementação: `IBackupService` → `Infrastructure/Backup/FileSystemBackupService.cs`.

| Aspecto | Detalhe |
|---------|---------|
| **Origem** | `kiosk_data_dir` no JSON (Pi: `/home/jukebox/.local/share/com.jukeeo.kiosk`; default sem config: `~/.local/share/com.jukeeo.kiosk`) |
| **Ficheiros** | `jukebox_library.db`, `jukebox_library.db-wal`, `jukebox_library.db-shm` (se existirem), `shared_preferences.json` |
| **Destino** | `{backups_dir}/pre-{versão}-{timestamp UTC}/` — default `/opt/jukeeo/backups/` |
| **Método** | `File.Copy` (cópia de ficheiros; kiosk parado) |
| **GC** | Após apply com sucesso, mantém no máximo `max_release_folders` pastas (default **7**), igual às releases |
| **Rollback** | Automático só repõe o symlink `current` → `previous`; **não** restaura SQLite — o backup é rede de segurança para recuperação manual |

Permissões no Pi: grupo `jukebox-ota` grava em `/opt/jukeeo/backups/`; ACL de leitura em `kiosk_data_dir` (ver [`docs/howto/DEPLOY_PI.md`](docs/howto/DEPLOY_PI.md)).

Produto (vault): [[PLANO_OTA_RASPBERRY_PI_SERVIDOR_PROPRIO]] §10–11 · execução: `jukebox_tv` [[PLANO_OTA_EXECUCAO_PI]].

## Desenvolvimento

Pré-requisito: .NET SDK 8+.

```powershell
dotnet build
dotnet test
dotnet run --project src/Jukebox.Ota.Agent -- version
dotnet run --project src/Jukebox.Ota.Agent -- check --config tools/mock/ota-agent.example.json
```

## Deploy no Raspberry Pi

Pi de referência: `jukebox@192.168.15.100`.

O fluxo recomendado usa **`deploy_to_pi.ps1`**, que orquestra publish, envio e instalação num único comando. Alternativa manual: `publish-linux-arm64.ps1` → `deploy_to_pi.ps1 -SkipPublish` ou install no Pi.

### `deploy_to_pi.ps1`

Script principal de deploy **Windows → Pi** (PowerShell, raiz do repo).

**Pré-requisitos:** .NET SDK 8+ (salvo `-SkipPublish`); SSH sem senha para `jukebox@<IP>`; WSL com `rsync` (preferido) ou OpenSSH Client (`scp`).

| Parâmetro | Padrão | Descrição |
|-----------|--------|-----------|
| `-PiHost` | `192.168.15.100` | IP do Raspberry Pi |
| `-PiUser` | `jukebox` | Utilizador SSH |
| `-RemotePath` | `/opt/jukeeo/ota-agent` | Destino do binário no Pi |
| `-RemoteStaging` | `/tmp/jukebox-ota-staging` | Staging temporário no Pi |
| `-SkipPublish` | — | Usa `artifacts/linux-arm64/` existente |
| `-SkipInstall` | — | Só envia staging; install manual no Pi |
| `-EnableTimer` | — | Habilita `jukebox_ota_agent.timer` no install |
| `-ForceConfig` | — | Sobrescreve `/etc/jukeeo/ota-agent.json` |

**Etapas internas:** (1) publish `linux-arm64` → (2) staging local em `artifacts/pi-deploy-staging/` (binário, systemd, sudoers, template config, `pi_install_ota.sh`) → (3) rsync via WSL ou fallback scp → (4) `sudo bash pi_install_ota.sh` no Pi.

```powershell
# Deploy completo (rebuild + install)
.\tools\deploy\deploy_to_pi.ps1 -PiHost 192.168.15.100

# Com timer periódico (após validar em lab)
.\tools\deploy\deploy_to_pi.ps1 -PiHost 192.168.15.100 -EnableTimer

# Só reenviar artefato já publicado
.\tools\deploy\deploy_to_pi.ps1 -SkipPublish

# Só enviar staging (install manual no Pi)
.\tools\deploy\deploy_to_pi.ps1 -SkipInstall
```

**Validação pós-deploy:**

```powershell
.\tools\deploy\verify_pi_from_windows.ps1 -PiHost 192.168.15.100
```

Saída do publish: `artifacts/linux-arm64/`. No Pi: binário em `/opt/jukeeo/ota-agent/`, config em `/etc/jukeeo/ota-agent.json`.

Documentação detalhada do script: [`tools/README.md`](tools/README.md#deploy_to_pips1) · how-to completo: [`docs/howto/DEPLOY_PI.md`](docs/howto/DEPLOY_PI.md).

| Guia | Conteúdo |
|------|----------|
| [`packaging/pi/README.md`](packaging/pi/README.md) | Checklist e copy-paste |
| [`docs/howto/DEPLOY_PI.md`](docs/howto/DEPLOY_PI.md) | Deploy, systemd, permissões, validação |
| [`tools/README.md`](tools/README.md) | Índice de scripts e fixtures mock |

## Estrutura do código

| Caminho | Papel |
|---------|-------|
| `src/Jukebox.Ota.Agent/Domain/` | Entidades, contratos, serviços de domínio |
| `src/Jukebox.Ota.Agent/Application/` | Casos de uso (`check`, `apply`, …) |
| `src/Jukebox.Ota.Agent/Infrastructure/` | HTTP, config, criptografia, releases, backup |
| `src/Jukebox.Ota.Agent/Interfaces/Cli/` | Entrada CLI |
| `tests/Jukebox.Ota.Agent.Tests/` | xUnit |
| `tools/deploy/` | Publish e deploy no Pi |
| `tools/mock/` | Fixtures JSON; mock HTTP **deprecado** — usar `jukebox-ota-server-dev` |

## Documentação

| Nota | Tema |
|------|------|
| [`docs/index.md`](docs/index.md) | MOC do repositório |
| [`docs/plans/`](docs/plans/) | Planos de execução (POC, segurança) |
| [`docs/howto/`](docs/howto/) | Deploy Pi, mock LAN |
| [`docs/API.md`](docs/API.md) | Contrato `/v1/updates/check` e `/v1/updates/ack` |
| [`.cursor/napkin.md`](.cursor/napkin.md) | Runbook operacional do agente |

## Workspace Cursor

Abrir `C:\GitHub\jukebox\jukeeo.code-workspace` (inclui `jukeeo-knowledge`, `jukebox_tv` e este repo).
