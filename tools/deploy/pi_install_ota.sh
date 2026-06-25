#!/usr/bin/env bash
# Instala o agente OTA Jukebox no Raspberry Pi (executar NO Pi ou via SSH).
#
# Espera staging em /tmp/jukebox-ota-staging/ com:
#   artifacts/   — publish linux-arm64 (jukebox-ota-agent + runtime)
#   systemd/     — jukebox_ota_agent.service + .time
#   config/      — ota-agent.example.json (template)
#   sudoers/     — jukebox-ota-systemctl.template
#
# Uso:
#   sudo ./pi_install_ota.sh
#   sudo ./pi_install_ota.sh --enable-time
#   sudo ./pi_install_ota.sh --force-config
#   sudo ./pi_install_ota.sh --staging-dir /tmp/jukebox-ota-staging --install-dir /opt/jukeeo/ota-agent

set -euo pipefail

STAGING_DIR="/tmp/jukebox-ota-staging"
INSTALL_DIR="/opt/jukeeo/ota-agent"
CONFIG_PATH="/etc/jukeeo/ota-agent.json"
CONFIG_TEMPLATE_NAME="ota-agent.example.json"
OTA_USER="jukebox-ota"
OTA_GROUP="jukebox-ota"
STATE_DIR="/var/lib/jukebox-ota"
JUKEEO_ROOT="/opt/jukeeo"
KIOSK_SERVICE="jukeeo_kiosk_flutterpi.service"
KIOSK_USER="jukebox"
SUDOERS_DEST="/etc/sudoers.d/99-jukebox-ota-systemctl"
KIOSK_SUDOERS_DEST="/etc/sudoers.d/99-jukebox-kiosk-ota-check"
ENABLE_TIMER=false
FORCE_CONFIG=false

usage() {
  cat <<'EOF'
pi_install_ota.sh — instala binário, units systemd, sudoers e permissões OTA

Opções:
  --staging-dir <dir>       Diretório de staging (padrão: /tmp/jukebox-ota-staging)
  --install-dir <dir>       Destino do binário (padrão: /opt/jukeeo/ota-agent)
  --kiosk-service <unit>    Unit systemd do kiosk (padrão: jukeeo_kiosk_flutterpi.service)
  --kiosk-user <user>       Utilizador do kiosk para ACL de backup (padrão: jukebox)
  --enable-timer            Habilita e inicia jukebox_ota_agent.timer
  --force-config            Sobrescreve /etc/jukeeo/ota-agent.json se já existi
  -h, --help                Exibe esta ajuda

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

ensure_state_downloads_layout() {
  log "Cache OTA em ${STATE_DIR}/downloads/ (grupo ${OTA_GROUP}, setgid)..."

  mkdir -p "${STATE_DIR}/downloads"
  chown "${OTA_USER}:${OTA_GROUP}" "${STATE_DIR}" "${STATE_DIR}/downloads"
  chmod 750 "${STATE_DIR}"
  # Membros do grupo (ex.: jukebox no check manual) podem gravar; novos ficheiros herdam o grupo.
  chmod 2770 "${STATE_DIR}/downloads"
}

ensure_kiosk_user_in_ota_group() {
  if ! id "$KIOSK_USER" &>/dev/null; then
    log "AVISO: utilizador kiosk ${KIOSK_USER} ausente — omitindo usermod"
    return 0
  fi

  if id -nG "$KIOSK_USER" | tr ' ' '\n' | grep -qx "$OTA_GROUP"; then
    log "Utilizador ${KIOSK_USER} já está no grupo ${OTA_GROUP}."
    return 0
  fi

  log "Adicionando ${KIOSK_USER} ao grupo ${OTA_GROUP} (builder OTA em ota/out)..."
  usermod -aG "$OTA_GROUP" "$KIOSK_USER"
  log "Nova sessão SSH do builder necessária para o grupo fazer efeito."
}

apply_install_permissions() {
  log "Aplicando permissões (${OTA_USER} sem escrita em binário/config)..."

  chown -R "root:${OTA_GROUP}" "$INSTALL_DIR"
  find "$INSTALL_DIR" -type d -exec chmod 750 {} \;
  find "$INSTALL_DIR" -type f -exec chmod 640 {} \;
  chmod 750 "${INSTALL_DIR}/${BINARY_NAME}"

  mkdir -p /etc/jukeeo
  chown "root:${OTA_GROUP}" /etc/jukeeo
  chmod 750 /etc/jukeeo

  if [[ -f "$CONFIG_PATH" ]]; then
    chown "root:${OTA_GROUP}" "$CONFIG_PATH"
    chmod 660 "$CONFIG_PATH"
  fi

  shopt -s nullglob
  for f in /etc/jukeeo/*.json /etc/jukeeo/*.pem; do
    chown "root:${OTA_GROUP}" "$f"
    if [[ "$f" == "$CONFIG_PATH" ]]; then
      chmod 660 "$f"
    else
      chmod 640 "$f"
    fi
  done
  shopt -u nullglob
}

apply_jukeeo_ota_layout_permissions() {
  log "Permissões OTA em ${JUKEEO_ROOT} (grupo ${OTA_GROUP})..."

  local dirs=(
    releases
    backups
    ota
    ota/incoming
    ota/staging
    ota/out
  )

  for rel in "${dirs[@]}"; do
    local target="${JUKEEO_ROOT}/${rel}"
    mkdir -p "$target"
    chown "root:${OTA_GROUP}" "$target"
    chmod 2775 "$target"
  done

  if [[ -d "$JUKEEO_ROOT" ]]; then
    chgrp "${OTA_GROUP}" "$JUKEEO_ROOT" 2>/dev/null || true
    chmod g+w "$JUKEEO_ROOT" 2>/dev/null || true
  fi
}

ensure_acl_package() {
  if command -v setfacl >/dev/null 2>&1; then
    return 0
  fi

  if command -v apt-get >/dev/null 2>&1; then
    log "Instalando pacote acl (setfacl) para backup SQLite no apply..."
    DEBIAN_FRONTEND=noninteractive apt-get install -y -qq acl || \
      log "AVISO: falha ao instalar acl — backup de dados no apply pode falhar"
  else
    log "AVISO: setfacl ausente e apt-get indisponível — instale o pacote acl manualmente"
  fi
}

apply_kiosk_data_read_acl() {
  local data_dir="/home/${KIOSK_USER}/.local/share/com.jukeeo.kiosk"

  ensure_acl_package

  if [[ ! -d "$data_dir" ]]; then
    log "AVISO: dados do kiosk ausentes (${data_dir}) — ACL de leitura omitida"
    return 0
  fi

  if ! command -v setfacl >/dev/null 2>&1; then
    log "AVISO: setfacl ainda indisponível — backup de dados no apply pode falhar"
    return 0
  fi

  log "ACL de leitura para ${OTA_USER} em ${data_dir}..."
  local home_dir="/home/${KIOSK_USER}"
  local parent="${home_dir}/.local/share"
  for traverse in "$home_dir" "${home_dir}/.local" "$parent"; do
    if [[ -d "$traverse" ]]; then
      setfacl -m "u:${OTA_USER}:--x" "$traverse" 2>/dev/null || true
    fi
  done
  setfacl -R -m "u:${OTA_USER}:rx" "$data_dir"
  find "$data_dir" -type f -exec setfacl -m "u:${OTA_USER}:r" {} + 2>/dev/null || true
}

apply_kiosk_logs_write_acl() {
  local logs_dir="/home/${KIOSK_USER}/.local/share/com.jukeeo.kiosk/logs"
  local kiosk_share="/home/${KIOSK_USER}/.local/share/com.jukeeo.kiosk"

  ensure_acl_package

  if [[ ! -d "$kiosk_share" ]]; then
    log "AVISO: diretório kiosk ausente (${kiosk_share}) — ACL de logs omitida"
    return 0
  fi

  if ! command -v setfacl >/dev/null 2>&1; then
    log "AVISO: setfacl ainda indisponível — escrita de logs OTA pode falhar"
    return 0
  fi

  log "ACL de escrita para ${OTA_USER} em ${logs_dir}..."
  mkdir -p "$logs_dir"
  chown "${KIOSK_USER}:${KIOSK_USER}" "$logs_dir"
  chmod 755 "$logs_dir"
  setfacl -m "u:${OTA_USER}:rwX" "$logs_dir"
  setfacl -d -m "u:${OTA_USER}:rw" "$logs_dir" 2>/dev/null || true
  # Ficheiros criados pelo agente OTA devem ser legíveis pelo kiosk na debug screen.
  setfacl -m "u:${KIOSK_USER}:r" "$logs_dir"
  setfacl -d -m "u:${KIOSK_USER}:r" "$logs_dir" 2>/dev/null || true
  if [[ -f "${logs_dir}/jukebox_ota_agent.log" ]]; then
    # sign-manifest no builder pode criar o ficheiro como root — repõe dono para ACL do agente.
    chown "${KIOSK_USER}:${KIOSK_USER}" "${logs_dir}/jukebox_ota_agent.log" 2>/dev/null || true
    setfacl -m "u:${OTA_USER}:rw" "${logs_dir}/jukebox_ota_agent.log" 2>/dev/null || true
    setfacl -m "u:${KIOSK_USER}:r" "${logs_dir}/jukebox_ota_agent.log" 2>/dev/null || true
    chmod a+r "${logs_dir}/jukebox_ota_agent.log" 2>/dev/null || true
  fi
}

apply_kiosk_ota_status_write_acl() {
  local kiosk_share="/home/${KIOSK_USER}/.local/share/com.jukeeo.kiosk"
  local status_file="${kiosk_share}/ota_update_status.json"

  ensure_acl_package

  if [[ ! -d "$kiosk_share" ]]; then
    log "AVISO: diretório kiosk ausente (${kiosk_share}) — ACL de ota_update_status.json omitida"
    return 0
  fi

  if ! command -v setfacl >/dev/null 2>&1; then
    log "AVISO: setfacl ainda indisponível — escrita de ota_update_status.json pode falhar"
    return 0
  fi

  log "ACL de escrita para ${OTA_USER} em ${status_file} (estado OTA partilhado com kiosk)..."
  # wx no diretório: ficheiros temporários atómicos (*.tmp) durante Write.
  setfacl -m "u:${OTA_USER}:wx" "$kiosk_share"
  touch "$status_file"
  chown "${KIOSK_USER}:${KIOSK_USER}" "$status_file"
  chmod 664 "$status_file"
  setfacl -m "u:${OTA_USER}:rw" "$status_file"
  setfacl -m "u:${KIOSK_USER}:rw" "$status_file"
}

normalize_ota_config() {
  [[ -f "$CONFIG_PATH" ]] || return 0

  if ! command -v python3 >/dev/null 2>&1; then
    log "AVISO: python3 ausente — normalização de ota-agent.json omitida"
    return 0
  fi

  log "Normalizando ${CONFIG_PATH} (kiosk_service_name, kiosk_data_dir)..."
  CONFIG_PATH="$CONFIG_PATH" KIOSK_USER="$KIOSK_USER" python3 <<'PY'
import json
import os
import sys

path = os.environ["CONFIG_PATH"]
kiosk_user = os.environ["KIOSK_USER"]

with open(path, encoding="utf-8") as f:
    config = json.load(f)

changed = False

name = config.get("kiosk_service_name", "")
if name and not name.endswith(".service"):
    config["kiosk_service_name"] = name + ".service"
    changed = True

data_dir = config.get("kiosk_data_dir", "")
if isinstance(data_dir, str) and data_dir.startswith("~/"):
    config["kiosk_data_dir"] = f"/home/{kiosk_user}/{data_dir[2:]}"
    changed = True

if changed:
    with open(path, "w", encoding="utf-8") as f:
        json.dump(config, f, indent=2, ensure_ascii=False)
        f.write("\n")
PY
}

install_sudoers_fragment() {
  local template="$1"
  local dest="$2"
  local label="$3"

  [[ -f "$template" ]] || die "template sudoers ausente em ${template} — execute deploy_to_pi.ps1"

  log "Instalando sudoers (${label})..."

  local tmp="${dest}.tmp"
  sed "s/@KIOSK_SERVICE@/${KIOSK_SERVICE}/g" "$template" | tr -d '\r' > "$tmp"
  chmod 440 "$tmp"

  if command -v visudo >/dev/null 2>&1; then
    visudo -c -f "$tmp" || die "fragmento sudoers inválido: ${dest}"
  else
    log "AVISO: visudo ausente — validação do sudoers omitida"
  fi

  mv "$tmp" "$dest"
}

install_sudoers() {
  install_sudoers_fragment \
    "${STAGING_DIR}/sudoers/jukebox-ota-systemctl.template" \
    "$SUDOERS_DEST" \
    "${OTA_USER} → systemctl ${KIOSK_SERVICE}"

  local kiosk_template="${STAGING_DIR}/sudoers/jukebox-kiosk-ota-check.template"
  if [[ -f "$kiosk_template" ]]; then
    install_sudoers_fragment \
      "$kiosk_template" \
      "$KIOSK_SUDOERS_DEST" \
      "${KIOSK_USER} → check OTA como ${OTA_USER}"
  else
    log "AVISO: template kiosk sudoers ausente — check manual do kiosk pode falhar em downloads/"
  fi
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
    --kiosk-service)
      KIOSK_SERVICE="${2:?--kiosk-service requer valor}"
      shift 2
      ;;
    --kiosk-user)
      KIOSK_USER="${2:?--kiosk-user requer valor}"
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

if [[ "$KIOSK_SERVICE" != *.service ]]; then
  KIOSK_SERVICE="${KIOSK_SERVICE}.service"
fi

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
ensure_state_downloads_layout

ensure_kiosk_user_in_ota_group

log "Instalando binário em ${INSTALL_DIR}..."
mkdir -p "$INSTALL_DIR"
cp -a "${ARTIFACTS_DIR}/." "${INSTALL_DIR}/"
chmod +x "${INSTALL_DIR}/${BINARY_NAME}"
apply_install_permissions

apply_jukeeo_ota_layout_permissions
apply_kiosk_data_read_acl
apply_kiosk_logs_write_acl
apply_kiosk_ota_status_write_acl
install_sudoers

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

mkdir -p /etc/jukeeo

if [[ -f "$CONFIG_PATH" && "$FORCE_CONFIG" != true ]]; then
  log "Config existente preservada: ${CONFIG_PATH} (use --force-config para sobrescrever)"
elif [[ -f "$CONFIG_TEMPLATE" ]]; then
  log "Criando ${CONFIG_PATH} a partir do template..."
  cp "$CONFIG_TEMPLATE" "$CONFIG_PATH"
else
  log "AVISO: template ${CONFIG_TEMPLATE} ausente — crie ${CONFIG_PATH} manualmente"
fi

if [[ -f "$CONFIG_PATH" ]]; then
  normalize_ota_config
fi

apply_install_permissions
ensure_state_downloads_layout

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
log "Apply (sem sudo integral): sudo -u ${OTA_USER} ${INSTALL_DIR}/${BINARY_NAME} apply --config ${CONFIG_PATH} ..."
log "systemctl kiosk: sudo -u ${OTA_USER} sudo -n /bin/systemctl is-active ${KIOSK_SERVICE}"
