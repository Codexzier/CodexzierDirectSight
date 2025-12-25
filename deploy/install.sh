#!/usr/bin/env bash
set -e

APP_NAME="CodexzierDirectSight"
INSTALL_DIR="/opt/myapp"
SERVICE_FILE="/etc/systemd/system/CodexzierDirectSight.service"
GITHUB_REPO="ORG/REPO"
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
DOWNLOAD_URL=$(curl -s https://github.com/Codexzier/CodexzierDirectSight/commits/latest \
  | grep browser_download_url \
  | grep "$ARCH" \
  | cut -d '"' -f 4)

if [ -z "$DOWNLOAD_URL" ]; then
  echo "ERROR: Could not find release artifact"
  exit 1
fi

# Download
echo "Downloading $DOWNLOAD_URL"
curl -L "$DOWNLOAD_URL" -o "$TMP_DIR/CodexzierDirectSight-linux-arm64.tar.gz"

# Stop Service (falls vorhanden)
systemctl stop myapp 2>/dev/null || true

# Entpacken
tar -xzf "$TMP_DIR/CodexzierDirectSight.tar.gz" -C "$INSTALL_DIR"

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
SyslogIdentifier=myapp
User=root

[Install]
WantedBy=multi-user.target
EOF

# systemd reload & start
systemctl daemon-reexec
systemctl daemon-reload
systemctl enable myapp
systemctl start myapp

echo "== Installation complete =="
echo "Status:"
systemctl status myapp --no-pager
