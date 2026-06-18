# Plano — POC `jukebox-ota-agent` em .NET no Raspberry Pi

- **Status:** Fase 5 concluída (JUK-67); decisão formal §7 pendente (JUK-70)
- **Data:** 2026-06-12
- **Escopo:** validar agente .NET self-contained no Pi antes de swap real de bundles.

**Origem:** promovido de [[PLANO_JUKEBOX_OTA_AGENT_DOTNET_POC]] · contexto [[PLANO_OTA_RASPBERRY_PI_SERVIDOR_PROPRIO]].

## Decisões fechadas (bootstrap)

| Tópico | Decisão |
|--------|---------|
| Chave pública | PEM em `public_key_path` |
| Objeto assinado | Manifesto JSON canónico |
| API mock na POC | `file://` ou HTTPS estático |
| Modo de execução | Timer systemd (oneshot) |
| Auto-update do agente | Fora do escopo da POC |

## Fases

| Fase | Entrega | Critério de saída | Estado |
|------|---------|-------------------|--------|
| 0 | Scaffold .NET + `version`/`check` | Build e testes no Windows/WSL | **concluída** (JUK-62) |
| 1 | Manifesto + `sha256` + RSA-PSS | Testes assinatura válida/inválida | **concluída** (JUK-63) |
| 2 | Publish `linux-arm64` self-contained | Artefato roda `version` no Pi | **concluída** (JUK-64) |
| 3 | systemd + journald | `SyslogIdentifier=jukebox-ota` | **concluída** (JUK-65) |
| 4 | `check` contra API mock HTTPS | Telemetria registada | **concluída** (JUK-66) — mock HTTP: `tools/mock/ota_mock_server.py`, howto [[FASE4_MOCK_LAN]] |
| 5 | Medições no Pi | Tamanho, RAM, startup documentados | **concluída** (JUK-67) |

## Medições no Pi (Fase 5 — JUK-67)

**Data:** 2026-06-18 · **Host:** `jukebox@192.168.15.100` (aarch64) · **Versão agente:** `0.1.0` · **Utilizador de execução:** `jukebox-ota`

| Métrica | Valor | Notas |
|---------|-------|-------|
| Artefacto self-contained (`/opt/jukeeo/ota-agent`) | **78 MiB** | 187 ficheiros; binário apphost 72 KiB |
| Maiores componentes | CoreLib ~14 MiB, libcoreclr ~6,4 MiB | `du` por ficheiro no Pi |
| RAM pico — `version` | **~6,4 MiB** (6524 KiB RSS) | Amostra `/proc/PID/status` durante execução |
| RAM pico — `check` | **~45 MiB** (46 440 KiB RSS) | Inclui HTTP + parse manifesto |
| RAM idle (processo residente) | **0** | Timer oneshot; sem daemon após saída |
| Tempo startup — `version` | **~0,25 s** | `bash` builtin `time` (pacote `time` GNU ausente no Pi) |
| Tempo — `check --config` | **~0,92 s** | Mock HTTP LAN; exit `2` quando há update |
| Pacote OTA comprimido | **12 MiB** | `/opt/jukeeo/ota/out/jukeeo-1.0.14+aarch64.tar.zst` (11 866 075 bytes) |
| Disco `/opt/jukeeo` | 29 GiB total, 48 % usado | Margem confortável para artefacto + pacotes |
| Memória sistema | 3,7 GiB RAM, ~406 MiB usada | Livre ~3,3 GiB disponível no momento da medição |

### Comparação com critérios §7 ([[PLANO_JUKEBOX_OTA_AGENT_DOTNET_POC]])

| Critério | Resultado | Avaliação |
|----------|-----------|-----------|
| Artefacto self-contained aceitável no Pi | 78 MiB em SD 29 GiB | **OK** |
| RAM idle não compete com kiosk/sync/GUI | 0 residente; pico &lt;50 MiB em `check` | **OK** |
| Startup rápido para timer/oneshot | &lt;1 s em `check` completo | **OK** |
| systemd/journald sem wrappers frágeis | Validado Fase 3 (JUK-65) | **OK** |
| Assinatura sem dependência nativa problemática | RSA-PSS `System.Security.Cryptography` (JUK-63) | **OK** |
| Deploy `linux-arm64` reproduzível | `publish-linux-arm64.ps1` + CI local (JUK-64) | **OK** |

**Conclusão:** todos os critérios §7 satisfeitos — **manter .NET** como linguagem do `jukebox-ota-agent`. Reavaliar Go apenas se custo operacional mudar (ex.: artefacto &gt;200 MiB ou pico RAM &gt;150 MiB em cenários futuros com download/apply).

### Comandos de reprodução

Ver `.cursor/napkin.md` (secção medições Pi).

## CLI

```text
jukebox-ota-agent version
jukebox-ota-agent check --config /etc/jukeeo/ota-agent.json
jukebox-ota-agent verify --manifest manifest.json --package package.tar.zst [--public-key chave.pem]
jukebox-ota-agent sign-manifest --manifest in.json --private-key key.pem [--output out.json]
jukebox-ota-agent apply --config ota-agent.json --manifest manifest.json --package bundle.tar.zst
```

## Fora de escopo

- Swap de `/opt/jukeeo/current` (ver [[PLANO_OTA_EXECUCAO_PI]])
- Rollback automático
- Parar/iniciar `jukebox_tv_flutterpi.service` ou sync agent

## Critérios para manter .NET

Ver [[PLANO_JUKEBOX_OTA_AGENT_DOTNET_POC]] §7. Reavaliar Go se dois ou mais critérios falharem sem correção simples.
