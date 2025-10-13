#!/bin/bash
# Bastion host initialization script - configures SSH CA, MFA, session recording, and monitoring.
set -euo pipefail

VAULT_ADDR="${vault_address}"
ENVIRONMENT="${environment}"
export ENVIRONMENT

log() {
  echo "[\$(date -Is)] $1"
}

log "Updating system packages"
apt-get update -y
apt-get upgrade -y

log "Installing required packages"
apt-get install -y \
  curl \
  wget \
  jq \
  fail2ban \
  asciinema \
  awscli \
  libpam-google-authenticator \
  python3-pip \
  unattended-upgrades

log "Enabling unattended security upgrades"
dpkg-reconfigure -plow unattended-upgrades

if ! command -v vault >/dev/null 2>&1; then
  curl -fsSL https://apt.releases.hashicorp.com/gpg | apt-key add -
  apt-add-repository "deb [arch=amd64] https://apt.releases.hashicorp.com \$(lsb_release -cs) main"
  apt-get update -y
  apt-get install -y vault
fi

log "Hardening sshd configuration"
cat > /etc/ssh/sshd_config.d/99-bastion-hardening.conf <<'CONFIG'
PermitRootLogin no
PasswordAuthentication no
ChallengeResponseAuthentication yes
AuthenticationMethods publickey,keyboard-interactive:pam
TrustedUserCAKeys /etc/ssh/ca.pub
ForceCommand /usr/local/bin/session-wrapper.sh
ClientAliveInterval 300
ClientAliveCountMax 2
MaxAuthTries 3
MaxSessions 10
Ciphers chacha20-poly1305@openssh.com,aes256-gcm@openssh.com
MACs hmac-sha2-512-etm@openssh.com,hmac-sha2-256-etm@openssh.com
KexAlgorithms curve25519-sha256,curve25519-sha256@libssh.org
X11Forwarding no
AllowTcpForwarding yes
CONFIG

log "Retrieving Vault SSH CA public key"
vault read -field=public_key ssh/config/ca > /etc/ssh/ca.pub || log "Vault CA fetch failed - ensure provisioning step"

cat > /usr/local/bin/session-wrapper.sh <<'WRAPPER'
#!/bin/bash
SESSION_ID=$(uuidgen)
TIMESTAMP=$(date -u +%Y%m%d_%H%M%S)
USERNAME=$(whoami)
CLIENT_IP=${SSH_CLIENT%% *}
SESSION_FILE="/tmp/session_${SESSION_ID}.cast"

asciinema rec --quiet --command "${SSH_ORIGINAL_COMMAND:-$SHELL}" "$SESSION_FILE"

if [ -n "$MINIO_ENDPOINT" ]; then
  aws s3 cp "$SESSION_FILE" "s3://bastion-sessions/${ENVIRONMENT}/${USERNAME}/${TIMESTAMP}_${SESSION_ID}.cast" \
    --endpoint-url "$MINIO_ENDPOINT" || true
fi

if [ -n "$ADMIN_SERVICE_URL" ]; then
  curl -s -X POST "$ADMIN_SERVICE_URL/api/admin/bastion/sessions" \
    -H "Content-Type: application/json" \
    -d "{\"sessionId\":\"$SESSION_ID\",\"username\":\"$USERNAME\",\"clientIp\":\"$CLIENT_IP\",\"bastionHost\":\"$(hostname -f)\",\"recordingPath\":\"${ENVIRONMENT}/${USERNAME}/${TIMESTAMP}_${SESSION_ID}.cast\"}" || true
fi

rm -f "$SESSION_FILE"
WRAPPER
chmod +x /usr/local/bin/session-wrapper.sh

log "Configuring MFA via PAM"
cat > /etc/pam.d/sshd <<'PAM'
auth required pam_google_authenticator.so nullok
auth include common-auth
account include common-account
password include common-password
session include common-session
PAM

log "Configuring fail2ban"
cat > /etc/fail2ban/jail.local <<'JAIL'
[sshd]
enabled  = true
port     = ssh
filter   = sshd
logpath  = /var/log/auth.log
maxretry = 3
bantime  = 3600
findtime = 600
JAIL
systemctl restart fail2ban

log "Installing node exporter for Prometheus"
wget -qO- https://github.com/prometheus/node_exporter/releases/download/v1.7.0/node_exporter-1.7.0.linux-amd64.tar.gz | tar xz -C /tmp
cp /tmp/node_exporter-1.7.0.linux-amd64/node_exporter /usr/local/bin/
useradd --no-create-home --shell /bin/false node_exporter || true
cat > /etc/systemd/system/node_exporter.service <<'SERVICE'
[Unit]
Description=Prometheus Node Exporter
After=network.target

[Service]
Type=simple
User=node_exporter
ExecStart=/usr/local/bin/node_exporter

[Install]
WantedBy=multi-user.target
SERVICE
systemctl enable node_exporter
systemctl start node_exporter

log "Restarting SSH service"
systemctl restart sshd

log "Bastion initialization completed"
