#!/usr/bin/env bash
# Instala o agente OTA Jukebox no Raspberry Pi (executar NO Pi ou via SSH).
#
# Espera staging em /tmp/jukebox-ota-staging/ com:
#   artifacts/   — publish linux-arm64 (jukebox-ota-agent + runtime)
#   systemd/     — jukebox_ota_agent.service + .timer
#   config/      — ota-agent.example.json (template)
#
# Uso:
#   sudo ./pi_install_ota.sh
#   sudo ./pi_install_ota.sh --enable-timer
#   sudo ./pi_install_ota.sh --force-config
#   sudo ./pi_install_ota.sh --staging-dir /tmp/jukebox-ota-staging --install-dir /opt/jukebox/ota-agent

set -euo pipefail

STAGING_DIR="/tmp/jukebox-ota-staging"
INSTALL_DIR="/opt/jukebox/ota-agent"
CONFIG_PATH="/etc/jukebox/ota-agent.json"
CONFIG_TEMPLATE_NAME="ota-agent.example.json"
OTA_USER="jukebox-ota"
OTA_GROUP="jukebox-ota"
STATE_DIR="/var/lib/jukebox-ota"
ENABLE_TIMER=false
FORCE_CONFIG=false

usage() {
  cat <<'EOF'
pi_install_ota.sh — instala binário, units systemd e config do agente OTA

Opções:
  --staging-dir <dir>    Diretório de staging (padrão: /tmp/jukebox-ota-staging)
  --install-dir <dir>    Destino do binário (padrão: /opt/jukebox/ota-agent)
  --enable-timer         Habilita e inicia jukebox_ota_agent.timer
  --force-config         Sobrescreve /etc/jukebox/ota-agent.json se já existir
  -h, --help             Exibe esta ajuda

Requer privilégios de root (sudo).
EOF
}

log() {
  echo "[pi_install_ota] $*"
}

ensure_ota_user() {
  if ! getent group "$OTA_GROUP" >/dev/null 2>&1; then
    log "Criando grupo de sistema ${OTA_GROUP}..."
    groupadd --system "$OTA_GROUP"
  fi

  if ! getent passwd "$OTA_USER" >/dev/null 2>&1; then
    log "Criando utilizador de sistema ${OTA_USER}..."
    useradd \
      --system \
      --gid "$OTA_GROUP" \
      --home-dir "$STATE_DIR" \
      --shell /usr/sbin/nologin \
      --comment "Jukebox OTA Agent" \
      "$OTA_USER"
  fi

  mkdir -p "$STATE_DIR"
  chown "${OTA_USER}:${OTA_GROUP}" "$STATE_DIR"
  chmod 750 "$STATE_DIR"
}

apply_install_permissions() {
  log "Aplicando permissões (${OTA_USER} sem escrita em binário/config)..."

  chown -R "root:${OTA_GROUP}" "$INSTALL_DIR"
  find "$INSTALL_DIR" -type d -exec chmod 750 {} \;
  find "$INSTALL_DIR" -type f -exec chmod 640 {} \;
  chmod 750 "${INSTALL_DIR}/${BINARY_NAME}"

  mkdir -p /etc/jukebox
  chown "root:${OTA_GROUP}" /etc/jukebox
  chmod 750 /etc/jukebox

  if [[ -f "$CONFIG_PATH" ]]; then
    chown "root:${OTA_GROUP}" "$CONFIG_PATH"
    chmod 640 "$CONFIG_PATH"
  fi

  shopt -s nullglob
  for f in /etc/jukebox/*.json /etc/jukebox/*.pem; do
    chown "root:${OTA_GROUP}" "$f"
    chmod 640 "$f"
  done
  shopt -u nullglob
}

die() {
  echo "[pi_install_ota] ERRO: $*" >&2
  exit 1
}

while [[ $# -gt 0 ]]; do
  case "$1" in
    --staging-dir)
      STAGING_DIR="${2:?--staging-dir requer valor}"
      shift 2
      ;;
    --install-dir)
      INSTALL_DIR="${2:?--install-dir requer valor}"
      shift 2
      ;;
    --enable-timer)
      ENABLE_TIMER=true
      shift
      ;;
    --force-config)
      FORCE_CONFIG=true
      shift
      ;;
    -h|--help)
      usage
      exit 0
      ;;
    *)
      die "opção desconhecida: $1"
      ;;
  esac
done

if [[ "$(id -u)" -ne 0 ]]; then
  die "execute com sudo (instalação em ${INSTALL_DIR} e /etc/systemd/system/)"
fi

ARTIFACTS_DIR="${STAGING_DIR}/artifacts"
SYSTEMD_DIR="${STAGING_DIR}/systemd"
CONFIG_TEMPLATE="${STAGING_DIR}/config/${CONFIG_TEMPLATE_NAME}"
BINARY_NAME="jukebox-ota-agent"

[[ -d "$ARTIFACTS_DIR" ]] || die "artifacts ausentes em ${ARTIFACTS_DIR} — execute deploy_to_pi.ps1 primeiro"
[[ -f "${ARTIFACTS_DIR}/${BINARY_NAME}" ]] || die "binário ${BINARY_NAME} não encontrado em ${ARTIFACTS_DIR}"

ensure_ota_user

log "Instalando binário em ${INSTALL_DIR}..."
mkdir -p "$INSTALL_DIR"
cp -a "${ARTIFACTS_DIR}/." "${INSTALL_DIR}/"
chmod +x "${INSTALL_DIR}/${BINARY_NAME}"
apply_install_permissions

if [[ -d "$SYSTEMD_DIR" ]]; then
  log "Instalando units systemd..."
  for unit in jukebox_ota_agent.service jukebox_ota_agent.timer; do
    if [[ -f "${SYSTEMD_DIR}/${unit}" ]]; then
      cp "${SYSTEMD_DIR}/${unit}" "/etc/systemd/system/${unit}"
    else
      log "AVISO: unit ausente: ${SYSTEMD_DIR}/${unit}"
    fi
  done
else
  log "AVISO: pasta systemd ausente em ${SYSTEMD_DIR}"
fi

mkdir -p /etc/jukebox

if [[ -f "$CONFIG_PATH" && "$FORCE_CONFIG" != true ]]; then
  log "Config existente preservada: ${CONFIG_PATH} (use --force-config para sobrescrever)"
elif [[ -f "$CONFIG_TEMPLATE" ]]; then
  log "Criando ${CONFIG_PATH} a partir do template..."
  cp "$CONFIG_TEMPLATE" "$CONFIG_PATH"
else
  log "AVISO: template ${CONFIG_TEMPLATE} ausente — crie ${CONFIG_PATH} manualmente"
fi

apply_install_permissions

log "Recarregando systemd..."
systemctl daemon-reload

if [[ "$ENABLE_TIMER" == true ]]; then
  log "Habilitando jukebox_ota_agent.timer..."
  systemctl enable --now jukebox_ota_agent.timer
else
  log "Timer NÃO habilitado (passe --enable-timer para ativar periodicidade)"
fi

log "Instalação concluída."
log "Validar: sudo -u ${OTA_USER} ${INSTALL_DIR}/${BINARY_NAME} version"
log "Timer/systemd corre como ${OTA_USER} (não root)."
