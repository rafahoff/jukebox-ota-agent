#!/usr/bin/env bash
# Sincroniza staging do agente OTA para o Raspberry Pi via rsync (executado no WSL).
# Chamado por deploy_to_pi.ps1 — não usar diretamente salvo depuração.
#
# Uso: deploy_to_pi_rsync.sh <host> <user> <staging_local> <remote_staging>

set -euo pipefail

PI_HOST="${1:?host obrigatório}"
PI_USER="${2:?user obrigatório}"
STAGING_LOCAL="${3:?staging local obrigatório}"
REMOTE_STAGING="${4:?staging remoto obrigatório}"

if [[ ! -d "$STAGING_LOCAL" ]]; then
  echo "ERRO: staging local não encontrado: $STAGING_LOCAL" >&2
  exit 1
fi

if ! command -v rsync >/dev/null 2>&1; then
  echo "ERRO: rsync não encontrado. Instale com: sudo apt install rsync" >&2
  exit 1
fi

DEST="${PI_USER}@${PI_HOST}:${REMOTE_STAGING}/"

echo "rsync → ${DEST}"
rsync -avz --delete \
  --exclude '.gitkeep' \
  "$STAGING_LOCAL/" "$DEST"

echo "Deploy rsync concluído."
