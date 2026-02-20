#!/usr/bin/env bash
set -euo pipefail

if [[ "${EUID}" -ne 0 ]]; then
  echo "Ejecutar con sudo: sudo ./install.sh" >&2
  exit 1
fi

APP_DIR="/opt/lb-electronica"
SERVICE_NAME="lb-electronica"
PORT="${PORT:-5080}"
CUR_DIR="$(cd "$(dirname "${BASH_SOURCE[0]}")" && pwd)"

echo "Instalando en $APP_DIR ..."
mkdir -p "$APP_DIR"
cp -r "$CUR_DIR"/* "$APP_DIR/"
chmod +x "$APP_DIR"/*.sh || true

if id -u lbapp >/dev/null 2>&1; then
  echo "Usuario de servicio lbapp existente."
else
  useradd --system --home "$APP_DIR" --shell /usr/sbin/nologin lbapp
fi

chown -R lbapp:lbapp "$APP_DIR"
mkdir -p "$APP_DIR/logs"
chown -R lbapp:lbapp "$APP_DIR/logs"

if command -v ufw >/dev/null 2>&1; then
  ufw allow "$PORT"/tcp >/dev/null 2>&1 || true
fi

sed "s|{{APP_DIR}}|$APP_DIR|g; s|{{PORT}}|$PORT|g" "$APP_DIR/lb-electronica.service" > "/etc/systemd/system/${SERVICE_NAME}.service"

systemctl daemon-reload
systemctl enable "$SERVICE_NAME" >/dev/null
systemctl restart "$SERVICE_NAME"

echo "Instalacion completa."
echo "Estado: systemctl status $SERVICE_NAME"
echo "URL local: http://localhost:${PORT}"
