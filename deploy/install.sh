#!/usr/bin/env bash
set -e

APP_NAME="CodexzierDirectSight"
SERVICE_NAME="codexzierdirectsight"
INSTALL_DIR="/opt/$SERVICE_NAME"
SERVICE_FILE="/etc/systemd/system/$SERVICE_NAME.service"
GITHUB_REPO="Codexzier/CodexzierDirectSight"
ARCH="linux-arm64"
TMP_DIR="/tmp/CodexzierDirectSight-install"

echo "== Installing $APP_NAME =="

# Root-Check
if [ "$EUID" -ne 0 ]; then
  echo "ERROR: Run as root (use sudo)"
  exit 1
fi

# Architektur-Check
if [[ "$(uname -m)" != "aarch64" ]]; then
  echo "ERROR: This installer supports only ARM64"
  exit 1
fi

# Vorbereitung
rm -rf "$TMP_DIR"
mkdir -p "$TMP_DIR"
mkdir -p "$INSTALL_DIR"

# Latest Release URL ermitteln
echo "Fetching latest release info..."
DOWNLOAD_URL=$(curl -s https://api.github.com/repos/$GITHUB_REPO/releases/latest \
  | grep browser_download_url \
  | grep "linux-arm64" \
  | cut -d '"' -f 4)

if [ -z "$DOWNLOAD_URL" ]; then
  echo "ERROR: Could not find release artifact"
  exit 1
fi

# Download
echo "Downloading $DOWNLOAD_URL"
curl -L "$DOWNLOAD_URL" -o "$TMP_DIR/app.tar.gz"

# Stop Service (falls vorhanden)
systemctl stop $SERVICE_NAME 2>/dev/null || true

# Entpacken
tar -xzf "$TMP_DIR/app.tar.gz" -C "$INSTALL_DIR"

chmod +x "$INSTALL_DIR/CodexzierDirectSight"

# systemd Service installieren
cat > "$SERVICE_FILE" <<EOF
[Unit]
Description=CodexzierDirectSight Worker Service
After=network.target

[Service]
ExecStart=$INSTALL_DIR/CodexzierDirectSight
WorkingDirectory=$INSTALL_DIR
Restart=always
RestartSec=5
KillSignal=SIGINT
SyslogIdentifier=CodexzierDirectSight
User=root

[Install]
WantedBy=multi-user.target
EOF

# systemd reload & start
systemctl daemon-reexec
systemctl daemon-reload
systemctl enable $SERVICE_NAME
systemctl start $SERVICE_NAME

echo "== Installation complete =="
systemctl status $SERVICE_NAME --no-pager
