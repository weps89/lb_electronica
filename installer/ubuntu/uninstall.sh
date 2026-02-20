#!/usr/bin/env bash
set -euo pipefail

if [[ "${EUID}" -ne 0 ]]; then
  echo "Ejecutar con sudo: sudo ./uninstall.sh" >&2
  exit 1
fi

SERVICE_NAME="lb-electronica"
APP_DIR="/opt/lb-electronica"

systemctl stop "$SERVICE_NAME" >/dev/null 2>&1 || true
systemctl disable "$SERVICE_NAME" >/dev/null 2>&1 || true
rm -f "/etc/systemd/system/${SERVICE_NAME}.service"
systemctl daemon-reload

rm -rf "$APP_DIR"

echo "LB Electronica removido."
