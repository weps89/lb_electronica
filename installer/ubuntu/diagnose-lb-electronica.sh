#!/usr/bin/env bash
set -euo pipefail
PORT="${1:-5080}"
APP_DIR="/opt/lb-electronica"

echo "=== Diagnostico LB Electronica (Ubuntu) ==="
[[ -f "$APP_DIR/LBElectronica.Server" ]] && echo "[OK] Binario presente" || echo "[ERROR] Falta binario"
[[ -f "$APP_DIR/lb_electronica.db" ]] && echo "[OK] DB presente" || echo "[WARN] DB aun no creada"

if systemctl is-active --quiet lb-electronica; then
  echo "[OK] Servicio activo"
else
  echo "[WARN] Servicio inactivo"
fi

if command -v curl >/dev/null 2>&1; then
  if curl -fsS "http://localhost:${PORT}/api/health" >/dev/null; then
    echo "[OK] /api/health responde"
  else
    echo "[WARN] /api/health no responde"
  fi
else
  echo "[WARN] curl no instalado"
fi

echo "=== Fin diagnostico ==="
