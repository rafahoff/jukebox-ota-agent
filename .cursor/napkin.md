# Napkin — runbook

Runbook curado do **jukebox-ota-agent**. Idioma: português (Brasil). Skill: `.cursor/skills/napkin/SKILL.md`.

## Regras de curadoria

- Reordenar a cada leitura por importância.
- Máx. 10 itens por categoria.
- Cada item: data `[YYYY-MM-DD]` + **Faça assim:**

## Execução e validação (prioridade máxima)

- [2026-06-18] **Decisão §7 — manter .NET (JUK-70)**
  Faça assim: POC fechada; linguagem definitiva .NET 8; critérios §7 OK (78 MiB, pico ~45 MiB, idle 0, startup <1s). Documentar em `docs/plans/PLANO_POC_DOTNET_PI.md` — não reavaliar Go salvo regressão material.

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

## Medições no Pi (Fase 5 — JUK-67)

- [2026-06-18] **Tamanho do artefacto**
  Faça assim: `ssh jukebox@192.168.15.100 'sudo -u jukebox-ota du -sh /opt/jukeeo/ota-agent; sudo -u jukebox-ota find /opt/jukeeo/ota-agent -type f | wc -l'`

- [2026-06-18] **Tempo de startup (`version`)**
  Faça assim: `ssh jukebox@192.168.15.100 'sudo -u jukebox-ota bash -lc "TIMEFORMAT=real_%R_sec; { time /opt/jukeeo/ota-agent/jukebox-ota-agent version; } 2>&1"'` — pacote GNU `time` ausente no Pi; usar builtin `bash`.

- [2026-06-18] **RAM pico durante execução**
  Faça assim: `ssh jukebox@192.168.15.100 'sudo -u jukebox-ota bash -lc "/opt/jukeeo/ota-agent/jukebox-ota-agent version & PID=\$!; for i in \$(seq 1 50); do RSS=\$(awk \"/^VmRSS:/ {print \\\$2}\" /proc/\$PID/status 2>/dev/null); [ -n \"\$RSS\" ] && echo peak_rss_kb=\$RSS && break; sleep 0.002; done; wait \$PID"'` — trocar `version` por `check --config /etc/jukeeo/ota-agent.json` para pico com HTTP.

- [2026-06-18] **Pacote OTA comprimido**
  Faça assim: `ssh jukebox@192.168.15.100 'du -sh /opt/jukeeo/ota/out/*.tar.zst; ls -lh /opt/jukeeo/ota/out/*.tar.zst'`

- [2026-06-18] **RAM idle**
  Faça assim: `ssh jukebox@192.168.15.100 "ps aux | grep -E '[j]ukebox-ota-agent'"` — esperado vazio (timer oneshot, sem processo residente).

## Pi e systemd

- [2026-06-19] **Backup pré-update no apply**
  Faça assim: origem `kiosk_data_dir` (`jukebox_library.db` + wal/shm + `shared_preferences.json`); destino `/opt/jukeeo/backups/pre-{versão}-{ts UTC}/`; `IBackupService` em `Infrastructure/Backup/FileSystemBackupService.cs`; rollback automático **não** restaura DB — ver `README.md` § Backup e `docs/API.md` § apply.

- [2026-06-19] **Política OTA via SQLite do kiosk**
  Faça assim: chaves em `machine_config` (`ota_check_enabled`, `ota_check_interval_hours`, `ota_check_window_start/end`); agente lê `jukebox_library.db` em `kiosk_data_dir`; defaults se DB ausente; `last_check_at_ms` em `/var/lib/jukebox-ota/`; timer systemd `OnUnitActiveSec=10min` (política no agente, não no timer).

- [2026-06-12] **Logs separados do kiosk**
  Faça assim: unit com `SyslogIdentifier=jukebox-ota`; validar com `journalctl -t jukebox-ota`. Arquivo compartilhado: `/home/jukebox/.local/share/com.jukeeo.kiosk/logs/jukebox_ota_agent.log` (rotação 5 MB × 5, `FileAgentLogger`); Windows dev: `%APPDATA%\com.jukeeo\kiosk\logs\`. Unit precisa `ReadWritePaths` nessa pasta; `pi_install_ota.sh` aplica ACL escrita `jukebox-ota` + leitura default `jukebox` (debug screen). Falhas de check no arquivo usam `OtaCheckErrorFormatter` (PT); journald mantém mensagem técnica em inglês.

- [2026-06-12] **Timer, não daemon residente**
  Faça assim: `jukebox_ota_agent.service` tipo `oneshot` + `jukebox_ota_agent.timer` (10min); reduz RAM idle no Pi.

- [2026-06-18] **Exit 2 no check ≠ falha systemd**
  Faça assim: unit com `SuccessExitStatus=0 2`; validar com `systemctl show -p ExecMainStatus,Result jukebox_ota_agent.service` após `systemctl start` quando há update disponível.

## Segurança

- [2026-06-18] **CRLF em scripts .sh**
  Faça assim: `.gitattributes` força LF; `deploy_to_pi.ps1` normaliza staging; no Pi, `sed -i 's/\r$//'` se script falhar com `$'\r': command not found`.

- [2026-06-18] **Apply sem root integral**
  Faça assim: `pi_install_ota.sh` instala sudoers + permissões em `/opt/jukeeo`; `apply` como `sudo -u jukebox-ota …` (não `sudo` no binário inteiro); validar `sudo -u jukebox-ota sudo -n /bin/systemctl is-active jukeeo_kiosk_flutterpi.service`.

- [2026-06-18] **Teste 4.4 rollback (Pi)**
  Faça assim: manifesto com `version` falsa (ex. `9.9.9`) + `signature_b64` vazio + pacote real `1.0.14`; `apply` como `jukebox-ota`; após ~90s health falha → `rolled_back` + `current` reposto. Ver [[HANDOFF_OTA_JUKEEO_PI]] § Validação 4.4.

- [2026-06-18] **Timer no install**
  Faça assim: `pi_install_ota.sh --enable-timer` habilita `jukebox_ota_agent.timer` (não `.time`).

- [2026-06-18] **Timer habilitado no Pi (JUK-69)**
  Faça assim: após JUK-67/JUK-68, `.\tools\deploy\deploy_to_pi.ps1 -PiHost 192.168.15.100 -EnableTimer` (executar na raiz do repo — `powershell -File` aninhado pode falhar parse). Validar: `systemctl is-enabled jukebox_ota_agent.timer` → `enabled`; `systemctl list-timers jukebox_ota_agent.timer`; `systemctl start jukebox_ota_agent.service` → `Result=success` com `ExecMainStatus=2` (update disponível, não failed).

- [2026-06-18] **sudoers + nome da unit**
  Faça assim: `kiosk_service_name` e comandos `systemctl` devem usar sufixo `.service` — o fragmento sudoers lista paths literais; o agente normaliza automaticamente desde 2026-06-18.

- [2026-06-18] **Backup SQLite no apply**
  Faça assim: além da ACL em `~/.local/share/com.jukeeo.kiosk`, aplicar `setfacl -m u:jukebox-ota:--x` em `/home/jukebox`, `.local` e `.local/share` (`pi_install_ota.sh`).

- [2026-06-18] **Execução sem root**
  Faça assim: timer/service com `User=jukebox-ota`; install via `pi_install_ota.sh` (root só no deploy); testar com `sudo -u jukebox-ota …/jukebox-ota-agent version`.

- [2026-06-18] **Chave pública no Pi**
  Faça assim: chave pública **não é segredo** — o risco é troca do PEM ou do binário com root; ver [[PLANO_SEGURANCA_OTA_PENDENCIAS]] §3 (user dedicado, fingerprint embutido, RO rootfs).

- [2026-06-12] **Chaves PEM**
  Faça assim: chave privada **nunca** no Pi; só `public_key_path` em `/etc/jukeeo/`; não versionar `*.pem` (ver `.gitignore`).
