#!/usr/bin/env bash
set -euo pipefail

VERSION="${1:-1.0.0}"
RUNTIME="${RUNTIME:-linux-x64}"
ROOT="$(cd "$(dirname "${BASH_SOURCE[0]}")" && pwd)"
CLIENT_DIR="$ROOT/client"
SERVER_DIR="$ROOT/server"
OUT_ROOT="$ROOT/artifacts/ubuntu-installer"
APP_OUT="$OUT_ROOT/app"
PKG_NAME="lb-electronica-${VERSION}-ubuntu-${RUNTIME}"
PKG_DIR="$OUT_ROOT/$PKG_NAME"

require_cmd() {
  command -v "$1" >/dev/null 2>&1 || { echo "Falta comando requerido: $1" >&2; exit 1; }
}

require_cmd dotnet
require_cmd npm
require_cmd tar

echo "[1/6] Limpiando salida anterior..."
rm -rf "$OUT_ROOT"
mkdir -p "$APP_OUT"

echo "[2/6] Compilando frontend..."
pushd "$CLIENT_DIR" >/dev/null
if [[ -f package-lock.json ]]; then
  npm ci
else
  npm install
fi
npm run build
popd >/dev/null

echo "[3/6] Copiando frontend a server/wwwroot..."
rm -rf "$SERVER_DIR/wwwroot"
mkdir -p "$SERVER_DIR/wwwroot"
cp -r "$CLIENT_DIR/dist/"* "$SERVER_DIR/wwwroot/"

echo "[4/6] Publicando backend self-contained ($RUNTIME)..."
pushd "$SERVER_DIR" >/dev/null
dotnet restore
dotnet publish -c Release -r "$RUNTIME" --self-contained true \
  /p:PublishSingleFile=true \
  /p:IncludeNativeLibrariesForSelfExtract=true \
  /p:PublishTrimmed=false \
  -o "$APP_OUT"
popd >/dev/null

echo "[5/6] Copiando scripts de instalacion y soporte..."
cp -r "$ROOT/scripts" "$APP_OUT/scripts"
cp "$ROOT/installer/ubuntu/install.sh" "$APP_OUT/install.sh"
cp "$ROOT/installer/ubuntu/uninstall.sh" "$APP_OUT/uninstall.sh"
cp "$ROOT/installer/ubuntu/start-lb-electronica.sh" "$APP_OUT/start-lb-electronica.sh"
cp "$ROOT/installer/ubuntu/stop-lb-electronica.sh" "$APP_OUT/stop-lb-electronica.sh"
cp "$ROOT/installer/ubuntu/diagnose-lb-electronica.sh" "$APP_OUT/diagnose-lb-electronica.sh"
cp "$ROOT/installer/ubuntu/lb-electronica.service" "$APP_OUT/lb-electronica.service"
chmod +x "$APP_OUT"/*.sh

if [[ ! -f "$APP_OUT/LBElectronica.Server" ]]; then
  echo "Publicacion incompleta: falta LBElectronica.Server" >&2
  exit 1
fi
if [[ ! -f "$APP_OUT/wwwroot/index.html" ]]; then
  echo "Publicacion incompleta: falta wwwroot/index.html" >&2
  exit 1
fi

echo "[6/6] Generando paquete tar.gz..."
mkdir -p "$PKG_DIR"
cp -r "$APP_OUT"/* "$PKG_DIR/"
cat > "$PKG_DIR/README-INSTALL-UBUNTU.txt" <<TXT
LB Electronica - Instalacion Ubuntu
===================================

1) Extraer paquete
2) Ejecutar:
   sudo ./install.sh

Iniciar:
   sudo systemctl start lb-electronica
Ver estado:
   sudo systemctl status lb-electronica
Abrir:
   http://localhost:5080

Login inicial:
   usuario: admin
   clave: admin123!
TXT

pushd "$OUT_ROOT" >/dev/null
tar -czf "${PKG_NAME}.tar.gz" "$PKG_NAME"
popd >/dev/null

echo "Paquete generado: $OUT_ROOT/${PKG_NAME}.tar.gz"
