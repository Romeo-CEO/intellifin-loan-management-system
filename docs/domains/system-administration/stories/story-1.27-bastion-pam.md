# Story 1.27: Bastion Host Deployment with PAM

## Story Metadata

| Field | Value |
|-------|-------|
| **Story ID** | 1.27 |
| **Epic** | System Administration Control Plane Enhancement |
| **Phase** | Phase 5: Observability & Infrastructure |
| **Sprint** | Sprint 9-10 |
| **Story Points** | 13 |
| **Estimated Effort** | 8-12 days |
| **Priority** | P0 (Critical - Security) |
| **Status** | 📋 Backlog |
| **Assigned To** | TBD |
| **Dependencies** | Kubernetes cluster, Network infrastructure, Vault (Story 1.23) |
| **Blocks** | Production infrastructure access, SSH access control |

---

## User Story

**As a** Security Engineer,  
**I want** infrastructure access controlled through bastion hosts with Privileged Access Management,  
**so that** all administrative access is centrally managed, audited, and secured with zero-trust principles.

---

## Business Value

Bastion hosts with PAM provide critical security and compliance benefits:

- **Zero Trust Access**: No direct SSH access to production servers; all access via audited bastion
- **Privileged Access Management**: JIT access provisioning with time-limited sessions
- **Session Recording**: Complete audit trail of all administrative actions
- **Network Segmentation**: Bastion as the only entry point to private network segments
- **Compliance**: Meets BoZ requirements for administrative access controls and audit
- **Emergency Access**: Break-glass procedures for critical incidents with full accountability
- **Attack Surface Reduction**: Eliminates SSH port exposure on production servers

This story is **critical** for production security, compliance, and operational integrity.

---

## Acceptance Criteria

### AC1: Bastion Host Infrastructure Deployment
**Given** Production infrastructure requires secure access  
**When** deploying bastion architecture  
**Then**:
- Bastion hosts deployed in dedicated subnet (DMZ)
- High availability: 2 bastion hosts (active-active)
- Load balancer distributes connections across bastions
- Network architecture:
  - Public subnet: Load balancer (internet-facing)
  - Bastion subnet: Bastion hosts (10.0.1.0/24)
  - Private subnets: Application servers (10.0.10.0/24, 10.0.20.0/24)
- Security groups/NSGs configured:
  - LB → Bastions: SSH (22), RDP (3389)
  - Bastions → Private: SSH (22), RDP (3389), WinRM (5985/5986)
  - Private → Bastions: None (no reverse connections)
- OS hardening applied (CIS benchmarks)
- Automatic security updates enabled

### AC2: SSH Certificate Authority with Vault
**Given** SSH access needs certificate-based authentication  
**When** configuring Vault SSH CA  
**Then**:
- Vault SSH secrets engine enabled
- SSH CA certificate generated and configured
- SSH roles defined:
  - `admin-role`: Full sudo access, 8-hour TTL
  - `developer-role`: Limited sudo, 4-hour TTL
  - `read-only-role`: No sudo, 2-hour TTL
- Host key signing enabled (verify bastion authenticity)
- Certificate templates configured with:
  - Valid principals (usernames)
  - Max TTL (8 hours)
  - Extensions (permit-pty, permit-port-forwarding)
  - Critical options (source-address restriction)
- Public CA key distributed to all target servers

### AC3: JIT Access Request Workflow
**Given** Users need temporary infrastructure access  
**When** requesting bastion access  
**Then**:
- Admin UI access request form includes:
  - Target environment (dev, staging, production)
  - Access duration (1-8 hours)
  - Justification (min 50 characters)
  - Target servers (optional, specific hosts)
- Approval workflow triggered:
  - **Dev/Staging**: Auto-approved if user in `developers` group
  - **Production**: Manager approval required
- Upon approval:
  - Vault SSH certificate issued
  - Certificate downloaded via Admin UI
  - SSH connection instructions displayed
  - Access automatically expires after TTL
- Approval workflow tracked in Camunda
- Access request logged in audit trail

### AC4: Session Recording with AsciinemaRec
**Given** All bastion sessions must be recorded  
**When** user connects to bastion  
**Then**:
- `asciinema` installed on bastion hosts
- SSH login triggers automatic session recording
- Recording configuration:
  - Format: AsciinemaRec JSON format
  - Storage: MinIO object storage (bucket: `bastion-sessions`)
  - Metadata: Username, timestamp, target host, session ID
  - Real-time upload: Sessions streamed to MinIO during session
- SSH `ForceCommand` executes recording wrapper
- Session playback available via Admin UI
- Session recordings retention: 3 years
- Recordings indexed in database for search

### AC5: Multi-Factor Authentication for Bastion Access
**Given** Bastion access requires strong authentication  
**When** user authenticates to bastion  
**Then**:
- SSH certificate authentication (first factor)
- MFA via Duo or Google Authenticator (second factor)
- MFA configured in PAM (Pluggable Authentication Modules)
- MFA challenge occurs before SSH session starts
- MFA bypass available for service accounts (with additional logging)
- Failed MFA attempts logged and alerted (3+ failures)
- MFA device registration via Admin UI

### AC6: Break-Glass Emergency Access
**Given** Critical incidents require immediate access  
**When** emergency break-glass access needed  
**Then**:
- Break-glass procedure available for P0 incidents
- Emergency access request requires:
  - Incident ticket number
  - Two-person authorization (2 admins must approve)
  - MFA from both approvers
- Emergency credentials issued via Vault (one-time use)
- Emergency access limited to 1 hour
- All emergency access actions logged with `EMERGENCY` severity
- Post-incident review required (within 24 hours)
- Break-glass usage triggers PagerDuty alert

### AC7: Bastion Access Dashboard and Monitoring
**Given** Access activity needs visibility  
**When** monitoring bastion usage  
**Then**:
- Admin UI dashboard displays:
  - Active sessions (count, users, duration)
  - Session history (last 7 days)
  - Access requests (pending, approved, denied)
  - Failed authentication attempts
  - Emergency access usage
- Grafana dashboard with metrics:
  - Bastion connections per hour
  - Average session duration
  - Failed authentication rate
  - Certificate issuance rate
- Prometheus alerts configured:
  - Failed authentication rate >10/hour (WARNING)
  - Emergency access triggered (CRITICAL)
  - Bastion host down (CRITICAL)

### AC8: Network Security and Logging
**Given** Bastion hosts are critical infrastructure  
**When** securing bastion network  
**Then**:
- Network firewall rules:
  - Source IP whitelisting (corporate VPN, office IPs)
  - Geo-blocking (only allow authorized countries)
  - Rate limiting (max 10 connections/minute per IP)
- DDoS protection enabled on load balancer
- Bastion logs forwarded to Elasticsearch:
  - SSH authentication logs
  - Session recording metadata
  - System logs (syslog)
  - Security events (fail2ban)
- Log retention: 1 year
- Real-time log monitoring with alerting

### AC9: Bastion Health Checks and Auto-Healing
**Given** Bastion availability is critical  
**When** monitoring bastion health  
**Then**:
- Health checks performed every 30 seconds:
  - SSH service responding (port 22)
  - CPU usage <80%
  - Memory usage <85%
  - Disk usage <90%
- Unhealthy bastion removed from load balancer
- Auto-scaling group replaces unhealthy instances (10 minute timeout)
- Health check failures alert operations team
- Bastion uptime target: 99.9%

---

## Technical Implementation Details

### Architecture Reference

**PRD Sections**: Lines 1283-1307 (Story 1.27), Phase 5 Overview  
**Architecture Sections**: Section 12 (Bastion Architecture), Section 10 (Vault Integration), Section 7 (Network Security)  
**Requirements**: NFR17 (All infrastructure access via bastion), NFR18 (Session recording 100%)

### Technology Stack

- **Bastion OS**: Ubuntu 22.04 LTS (hardened)
- **SSH CA**: HashiCorp Vault SSH Secrets Engine
- **Session Recording**: Asciinema, MinIO
- **MFA**: Duo Security or Google Authenticator (PAM integration)
- **Load Balancer**: Azure Load Balancer / AWS NLB
- **Monitoring**: Prometheus, Grafana, Elasticsearch
- **Orchestration**: Terraform, Ansible
- **Access Management**: Camunda workflows

### Network Architecture Diagram

```
┌─────────────────────────────────────────────────────────────┐
│                         Internet                             │
└────────────────────────┬────────────────────────────────────┘
                         │
                         ▼
              ┌──────────────────────┐
              │   Load Balancer      │
              │   (Public IP)        │
              │   SSH: 22, RDP: 3389 │
              └──────────┬───────────┘
                         │
        ┌────────────────┴────────────────┐
        │                                  │
        ▼                                  ▼
┌───────────────┐                  ┌───────────────┐
│  Bastion-01   │                  │  Bastion-02   │
│  10.0.1.10    │                  │  10.0.1.11    │
│  (Active)     │                  │  (Active)     │
└───────┬───────┘                  └───────┬───────┘
        │                                  │
        └────────────────┬─────────────────┘
                         │
        ┌────────────────┴────────────────────────┐
        │                                          │
        ▼                                          ▼
┌─────────────────┐                      ┌─────────────────┐
│ Private Subnet  │                      │ Private Subnet  │
│ App Servers     │                      │ Database Servers│
│ 10.0.10.0/24    │                      │ 10.0.20.0/24    │
│                 │                      │                 │
│ - API Gateway   │                      │ - PostgreSQL    │
│ - Identity Svc  │                      │ - SQL Server    │
│ - Loan Service  │                      │ - Redis         │
└─────────────────┘                      └─────────────────┘
```

### Terraform Infrastructure Code

```hcl
# terraform/bastion/main.tf

terraform {
  required_version = ">= 1.5.0"
  required_providers {
    azurerm = {
      source  = "hashicorp/azurerm"
      version = "~> 3.0"
    }
  }
}

provider "azurerm" {
  features {}
}

# Resource Group
resource "azurerm_resource_group" "bastion_rg" {
  name     = "rg-intellifin-bastion-${var.environment}"
  location = var.location
  tags     = var.tags
}

# Virtual Network
resource "azurerm_virtual_network" "vnet" {
  name                = "vnet-intellifin-${var.environment}"
  address_space       = ["10.0.0.0/16"]
  location            = azurerm_resource_group.bastion_rg.location
  resource_group_name = azurerm_resource_group.bastion_rg.name
  tags                = var.tags
}

# Bastion Subnet
resource "azurerm_subnet" "bastion_subnet" {
  name                 = "subnet-bastion"
  resource_group_name  = azurerm_resource_group.bastion_rg.name
  virtual_network_name = azurerm_virtual_network.vnet.name
  address_prefixes     = ["10.0.1.0/24"]
}

# Private Application Subnet
resource "azurerm_subnet" "app_subnet" {
  name                 = "subnet-app"
  resource_group_name  = azurerm_resource_group.bastion_rg.name
  virtual_network_name = azurerm_virtual_network.vnet.name
  address_prefixes     = ["10.0.10.0/24"]
}

# Network Security Group for Bastion
resource "azurerm_network_security_group" "bastion_nsg" {
  name                = "nsg-bastion-${var.environment}"
  location            = azurerm_resource_group.bastion_rg.location
  resource_group_name = azurerm_resource_group.bastion_rg.name

  # Allow SSH from corporate network
  security_rule {
    name                       = "Allow-SSH-Corporate"
    priority                   = 100
    direction                  = "Inbound"
    access                     = "Allow"
    protocol                   = "Tcp"
    source_port_range          = "*"
    destination_port_range     = "22"
    source_address_prefixes    = var.corporate_ip_ranges
    destination_address_prefix = "*"
  }

  # Allow RDP from corporate network
  security_rule {
    name                       = "Allow-RDP-Corporate"
    priority                   = 110
    direction                  = "Inbound"
    access                     = "Allow"
    protocol                   = "Tcp"
    source_port_range          = "*"
    destination_port_range     = "3389"
    source_address_prefixes    = var.corporate_ip_ranges
    destination_address_prefix = "*"
  }

  # Deny all other inbound
  security_rule {
    name                       = "Deny-All-Inbound"
    priority                   = 4096
    direction                  = "Inbound"
    access                     = "Deny"
    protocol                   = "*"
    source_port_range          = "*"
    destination_port_range     = "*"
    source_address_prefix      = "*"
    destination_address_prefix = "*"
  }

  tags = var.tags
}

# Public IP for Load Balancer
resource "azurerm_public_ip" "bastion_lb_ip" {
  name                = "pip-bastion-lb-${var.environment}"
  location            = azurerm_resource_group.bastion_rg.location
  resource_group_name = azurerm_resource_group.bastion_rg.name
  allocation_method   = "Static"
  sku                 = "Standard"
  tags                = var.tags
}

# Load Balancer
resource "azurerm_lb" "bastion_lb" {
  name                = "lb-bastion-${var.environment}"
  location            = azurerm_resource_group.bastion_rg.location
  resource_group_name = azurerm_resource_group.bastion_rg.name
  sku                 = "Standard"

  frontend_ip_configuration {
    name                 = "bastion-frontend"
    public_ip_address_id = azurerm_public_ip.bastion_lb_ip.id
  }

  tags = var.tags
}

# Backend Pool
resource "azurerm_lb_backend_address_pool" "bastion_pool" {
  loadbalancer_id = azurerm_lb.bastion_lb.id
  name            = "bastion-backend-pool"
}

# Health Probe
resource "azurerm_lb_probe" "ssh_probe" {
  loadbalancer_id = azurerm_lb.bastion_lb.id
  name            = "ssh-health-probe"
  protocol        = "Tcp"
  port            = 22
  interval_in_seconds = 5
  number_of_probes    = 2
}

# Load Balancing Rule (SSH)
resource "azurerm_lb_rule" "ssh_rule" {
  loadbalancer_id                = azurerm_lb.bastion_lb.id
  name                           = "ssh-lb-rule"
  protocol                       = "Tcp"
  frontend_port                  = 22
  backend_port                   = 22
  frontend_ip_configuration_name = "bastion-frontend"
  backend_address_pool_ids       = [azurerm_lb_backend_address_pool.bastion_pool.id]
  probe_id                       = azurerm_lb_probe.ssh_probe.id
  enable_floating_ip             = false
  idle_timeout_in_minutes        = 10
}

# Bastion Virtual Machines (Scale Set)
resource "azurerm_linux_virtual_machine_scale_set" "bastion_vmss" {
  name                = "vmss-bastion-${var.environment}"
  resource_group_name = azurerm_resource_group.bastion_rg.name
  location            = azurerm_resource_group.bastion_rg.location
  sku                 = "Standard_B2s"
  instances           = 2
  admin_username      = "bastionadmin"
  
  disable_password_authentication = true
  
  admin_ssh_key {
    username   = "bastionadmin"
    public_key = file("~/.ssh/bastion_host_key.pub")
  }

  source_image_reference {
    publisher = "Canonical"
    offer     = "0001-com-ubuntu-server-jammy"
    sku       = "22_04-lts-gen2"
    version   = "latest"
  }

  os_disk {
    caching              = "ReadWrite"
    storage_account_type = "Premium_LRS"
  }

  network_interface {
    name    = "bastion-nic"
    primary = true

    ip_configuration {
      name                                   = "internal"
      primary                                = true
      subnet_id                              = azurerm_subnet.bastion_subnet.id
      load_balancer_backend_address_pool_ids = [azurerm_lb_backend_address_pool.bastion_pool.id]
    }
  }

  # User data script for initialization
  custom_data = base64encode(templatefile("${path.module}/scripts/bastion-init.sh", {
    vault_address = var.vault_address
    environment   = var.environment
  }))

  tags = var.tags
}

# Auto-scaling rules
resource "azurerm_monitor_autoscale_setting" "bastion_autoscale" {
  name                = "bastion-autoscale"
  resource_group_name = azurerm_resource_group.bastion_rg.name
  location            = azurerm_resource_group.bastion_rg.location
  target_resource_id  = azurerm_linux_virtual_machine_scale_set.bastion_vmss.id

  profile {
    name = "default-profile"

    capacity {
      default = 2
      minimum = 2
      maximum = 4
    }

    rule {
      metric_trigger {
        metric_name        = "Percentage CPU"
        metric_resource_id = azurerm_linux_virtual_machine_scale_set.bastion_vmss.id
        time_grain         = "PT1M"
        statistic          = "Average"
        time_window        = "PT5M"
        time_aggregation   = "Average"
        operator           = "GreaterThan"
        threshold          = 75
      }

      scale_action {
        direction = "Increase"
        type      = "ChangeCount"
        value     = "1"
        cooldown  = "PT5M"
      }
    }

    rule {
      metric_trigger {
        metric_name        = "Percentage CPU"
        metric_resource_id = azurerm_linux_virtual_machine_scale_set.bastion_vmss.id
        time_grain         = "PT1M"
        statistic          = "Average"
        time_window        = "PT5M"
        time_aggregation   = "Average"
        operator           = "LessThan"
        threshold          = 25
      }

      scale_action {
        direction = "Decrease"
        type      = "ChangeCount"
        value     = "1"
        cooldown  = "PT5M"
      }
    }
  }
}

output "bastion_public_ip" {
  value = azurerm_public_ip.bastion_lb_ip.ip_address
}

output "bastion_fqdn" {
  value = "bastion.${var.domain}"
}
```

### Bastion Initialization Script

```bash
#!/bin/bash
# scripts/bastion-init.sh

set -euo pipefail

VAULT_ADDR="${vault_address}"
ENVIRONMENT="${environment}"

echo "Initializing bastion host for environment: $ENVIRONMENT"

# Update system packages
apt-get update
apt-get upgrade -y

# Install required packages
apt-get install -y \
    curl \
    wget \
    jq \
    fail2ban \
    asciinema \
    libpam-google-authenticator \
    python3-pip \
    unattended-upgrades

# Configure automatic security updates
dpkg-reconfigure -plow unattended-upgrades

# Install Vault CLI
curl -fsSL https://apt.releases.hashicorp.com/gpg | apt-key add -
apt-add-repository "deb [arch=amd64] https://apt.releases.hashicorp.com $(lsb_release -cs) main"
apt-get update
apt-get install -y vault

# Configure SSH hardening
cat > /etc/ssh/sshd_config.d/99-hardening.conf <<EOF
# SSH Hardening Configuration

# Disable root login
PermitRootLogin no

# Disable password authentication
PasswordAuthentication no
ChallengeResponseAuthentication yes

# Enable certificate authentication
TrustedUserCAKeys /etc/ssh/ca.pub

# Logging
SyslogFacility AUTH
LogLevel VERBOSE

# Session settings
ClientAliveInterval 300
ClientAliveCountMax 2
MaxAuthTries 3
MaxSessions 10

# Force command for session recording
ForceCommand /usr/local/bin/session-wrapper.sh

# Allowed authentication methods
AuthenticationMethods publickey,keyboard-interactive:pam

# Restrict ciphers and MACs to strong algorithms
Ciphers chacha20-poly1305@openssh.com,aes256-gcm@openssh.com
MACs hmac-sha2-512-etm@openssh.com,hmac-sha2-256-etm@openssh.com
KexAlgorithms curve25519-sha256,curve25519-sha256@libssh.org

# Disable X11 forwarding
X11Forwarding no

# Allow TCP forwarding (required for tunneling)
AllowTcpForwarding yes
EOF

# Fetch Vault SSH CA public key
vault read -field=public_key ssh/config/ca > /etc/ssh/ca.pub

# Create session recording wrapper
cat > /usr/local/bin/session-wrapper.sh <<'WRAPPER'
#!/bin/bash

SESSION_ID=$(uuidgen)
TIMESTAMP=$(date -u +%Y%m%d_%H%M%S)
USERNAME=$(whoami)
CLIENT_IP=${SSH_CLIENT%% *}
SESSION_FILE="/tmp/session_${SESSION_ID}.cast"

# Start asciinema recording
asciinema rec \
    --quiet \
    --title "Bastion Session: $USERNAME from $CLIENT_IP" \
    --command "${SSH_ORIGINAL_COMMAND:-$SHELL}" \
    "$SESSION_FILE"

# Upload recording to MinIO
aws s3 cp "$SESSION_FILE" \
    "s3://bastion-sessions/${ENVIRONMENT}/${USERNAME}/${TIMESTAMP}_${SESSION_ID}.cast" \
    --endpoint-url "$MINIO_ENDPOINT"

# Store session metadata
curl -X POST "$ADMIN_SERVICE_URL/api/admin/bastion/sessions" \
    -H "Content-Type: application/json" \
    -d "{
        \"sessionId\": \"$SESSION_ID\",
        \"username\": \"$USERNAME\",
        \"clientIp\": \"$CLIENT_IP\",
        \"startTime\": \"$TIMESTAMP\",
        \"recordingPath\": \"${ENVIRONMENT}/${USERNAME}/${TIMESTAMP}_${SESSION_ID}.cast\"
    }"

# Cleanup
rm -f "$SESSION_FILE"
WRAPPER

chmod +x /usr/local/bin/session-wrapper.sh

# Configure fail2ban
cat > /etc/fail2ban/jail.local <<EOF
[sshd]
enabled = true
port = ssh
filter = sshd
logpath = /var/log/auth.log
maxretry = 3
bantime = 3600
findtime = 600
EOF

systemctl enable fail2ban
systemctl restart fail2ban

# Configure PAM for MFA
cat > /etc/pam.d/sshd <<EOF
# PAM configuration for SSH with MFA

# Standard Un*x authentication
@include common-auth

# Google Authenticator MFA
auth required pam_google_authenticator.so nullok

# Account and session management
@include common-account
@include common-session

# Password management
@include common-password

# Session recording
session required pam_exec.so /usr/local/bin/pam-session-notify.sh
EOF

# Create PAM session notification script
cat > /usr/local/bin/pam-session-notify.sh <<'PAM_SCRIPT'
#!/bin/bash
if [ "$PAM_TYPE" = "open_session" ]; then
    logger -t bastion-pam "Session opened for $PAM_USER from $PAM_RHOST"
fi
PAM_SCRIPT

chmod +x /usr/local/bin/pam-session-notify.sh

# Configure rsyslog for centralized logging
cat > /etc/rsyslog.d/50-bastion.conf <<EOF
# Forward auth logs to Elasticsearch
auth,authpriv.* @@elasticsearch.intellifin.local:514
EOF

systemctl restart rsyslog

# Configure Prometheus node exporter
wget https://github.com/prometheus/node_exporter/releases/download/v1.7.0/node_exporter-1.7.0.linux-amd64.tar.gz
tar xvfz node_exporter-1.7.0.linux-amd64.tar.gz
cp node_exporter-1.7.0.linux-amd64/node_exporter /usr/local/bin/
rm -rf node_exporter-1.7.0.linux-amd64*

cat > /etc/systemd/system/node_exporter.service <<EOF
[Unit]
Description=Prometheus Node Exporter
After=network.target

[Service]
Type=simple
User=node_exporter
ExecStart=/usr/local/bin/node_exporter

[Install]
WantedBy=multi-user.target
EOF

useradd --no-create-home --shell /bin/false node_exporter
systemctl enable node_exporter
systemctl start node_exporter

# Restart SSH
systemctl restart sshd

echo "Bastion initialization complete"
```

### Vault SSH Secrets Engine Configuration

```hcl
# vault-ssh-config.hcl

# Enable SSH secrets engine
vault secrets enable -path=ssh ssh

# Configure SSH CA
vault write ssh/config/ca generate_signing_key=true

# Create SSH role for administrators
vault write ssh/roles/admin-role -<<EOF
{
  "allow_user_certificates": true,
  "allowed_users": "*",
  "allowed_extensions": "permit-pty,permit-port-forwarding",
  "default_extensions": {
    "permit-pty": "",
    "permit-port-forwarding": ""
  },
  "key_type": "ca",
  "default_user": "ubuntu",
  "ttl": "8h",
  "max_ttl": "24h",
  "allowed_critical_options": "source-address",
  "algorithm_signer": "rsa-sha2-512"
}
EOF

# Create SSH role for developers
vault write ssh/roles/developer-role -<<EOF
{
  "allow_user_certificates": true,
  "allowed_users": "*",
  "allowed_extensions": "permit-pty",
  "default_extensions": {
    "permit-pty": ""
  },
  "key_type": "ca",
  "default_user": "ubuntu",
  "ttl": "4h",
  "max_ttl": "8h",
  "algorithm_signer": "rsa-sha2-512"
}
EOF

# Create SSH role for read-only access
vault write ssh/roles/read-only-role -<<EOF
{
  "allow_user_certificates": true,
  "allowed_users": "*",
  "allowed_extensions": "",
  "key_type": "ca",
  "default_user": "ubuntu",
  "ttl": "2h",
  "max_ttl": "4h",
  "algorithm_signer": "rsa-sha2-512"
}
EOF

# Create policy for SSH certificate issuance
vault policy write ssh-user-policy -<<EOF
path "ssh/sign/admin-role" {
  capabilities = ["create", "update"]
}

path "ssh/sign/developer-role" {
  capabilities = ["create", "update"]
}

path "ssh/sign/read-only-role" {
  capabilities = ["create", "update"]
}
EOF
```

### Admin Service API - Bastion Access Management

```csharp
// Controllers/BastionAccessController.cs
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Authorization;
using IntelliFin.Admin.Services;
using IntelliFin.Admin.Models;

namespace IntelliFin.Admin.Controllers
{
    [ApiController]
    [Route("api/admin/bastion")]
    [Authorize]
    public class BastionAccessController : ControllerBase
    {
        private readonly IBastionAccessService _bastionService;
        private readonly ILogger<BastionAccessController> _logger;

        public BastionAccessController(
            IBastionAccessService bastionService,
            ILogger<BastionAccessController> logger)
        {
            _bastionService = bastionService;
            _logger = logger;
        }

        /// <summary>
        /// Request bastion access
        /// </summary>
        [HttpPost("access-requests")]
        [ProducesResponseType(typeof(BastionAccessRequestDto), StatusCodes.Status202Accepted)]
        [ProducesResponseType(StatusCodes.Status400BadRequest)]
        public async Task<IActionResult> RequestAccess(
            [FromBody] BastionAccessRequest request,
            CancellationToken cancellationToken)
        {
            var userId = User.FindFirstValue(ClaimTypes.NameIdentifier);
            var userName = User.FindFirstValue(ClaimTypes.Name);

            if (string.IsNullOrWhiteSpace(request.Justification) || request.Justification.Length < 50)
                return BadRequest(new { error = "Justification must be at least 50 characters" });

            _logger.LogInformation(
                "Bastion access requested: User={UserId}, Environment={Environment}",
                userId, request.Environment);

            try
            {
                var accessRequest = await _bastionService.RequestAccessAsync(
                    request,
                    userId,
                    userName,
                    cancellationToken);

                if (accessRequest.RequiresApproval)
                {
                    return AcceptedAtAction(
                        nameof(GetAccessRequestStatus),
                        new { requestId = accessRequest.RequestId },
                        accessRequest);
                }
                else
                {
                    return Ok(accessRequest);
                }
            }
            catch (ValidationException ex)
            {
                return BadRequest(new { error = ex.Message });
            }
        }

        /// <summary>
        /// Get access request status
        /// </summary>
        [HttpGet("access-requests/{requestId}")]
        [ProducesResponseType(typeof(BastionAccessRequestStatusDto), StatusCodes.Status200OK)]
        public async Task<IActionResult> GetAccessRequestStatus(
            Guid requestId,
            CancellationToken cancellationToken)
        {
            var status = await _bastionService.GetAccessRequestStatusAsync(requestId, cancellationToken);
            
            if (status == null)
                return NotFound();

            return Ok(status);
        }

        /// <summary>
        /// Download SSH certificate
        /// </summary>
        [HttpGet("access-requests/{requestId}/certificate")]
        [ProducesResponseType(typeof(FileResult), StatusCodes.Status200OK)]
        public async Task<IActionResult> DownloadCertificate(
            Guid requestId,
            CancellationToken cancellationToken)
        {
            var certificate = await _bastionService.GetSSHCertificateAsync(requestId, cancellationToken);

            if (certificate == null)
                return NotFound();

            return File(
                System.Text.Encoding.UTF8.GetBytes(certificate.CertificateContent),
                "application/x-pem-file",
                $"bastion-cert-{requestId}.pub");
        }

        /// <summary>
        /// Get active bastion sessions
        /// </summary>
        [HttpGet("sessions")]
        [Authorize(Roles = "System Administrator,Security Engineer")]
        [ProducesResponseType(typeof(List<BastionSessionDto>), StatusCodes.Status200OK)]
        public async Task<IActionResult> GetActiveSessions(
            CancellationToken cancellationToken)
        {
            var sessions = await _bastionService.GetActiveSessionsAsync(cancellationToken);
            return Ok(sessions);
        }

        /// <summary>
        /// Get session recording
        /// </summary>
        [HttpGet("sessions/{sessionId}/recording")]
        [Authorize(Roles = "System Administrator,Security Engineer,Auditor")]
        [ProducesResponseType(typeof(SessionRecordingDto), StatusCodes.Status200OK)]
        public async Task<IActionResult> GetSessionRecording(
            string sessionId,
            CancellationToken cancellationToken)
        {
            var recording = await _bastionService.GetSessionRecordingAsync(sessionId, cancellationToken);

            if (recording == null)
                return NotFound();

            return Ok(recording);
        }

        /// <summary>
        /// Emergency break-glass access
        /// </summary>
        [HttpPost("emergency-access")]
        [Authorize(Roles = "System Administrator")]
        [RequiresMfa(TimeoutMinutes = 15)]
        [ProducesResponseType(typeof(EmergencyAccessDto), StatusCodes.Status200OK)]
        public async Task<IActionResult> RequestEmergencyAccess(
            [FromBody] EmergencyAccessRequest request,
            CancellationToken cancellationToken)
        {
            var userId = User.FindFirstValue(ClaimTypes.NameIdentifier);

            _logger.LogWarning(
                "Emergency bastion access requested: User={UserId}, Incident={IncidentId}",
                userId, request.IncidentTicketId);

            var emergencyAccess = await _bastionService.RequestEmergencyAccessAsync(
                request,
                userId,
                cancellationToken);

            return Ok(emergencyAccess);
        }
    }
}
```

### Database Schema

```sql
-- Admin Service Database

CREATE TABLE BastionAccessRequests (
    Id BIGINT IDENTITY(1,1) PRIMARY KEY,
    RequestId UNIQUEIDENTIFIER NOT NULL DEFAULT NEWID() UNIQUE,
    
    UserId NVARCHAR(100) NOT NULL,
    UserName NVARCHAR(200) NOT NULL,
    UserEmail NVARCHAR(200) NOT NULL,
    
    Environment NVARCHAR(50) NOT NULL,  -- dev, staging, production
    TargetHosts NVARCHAR(MAX),  -- JSON array of specific hosts (optional)
    AccessDurationHours INT NOT NULL,
    Justification NVARCHAR(1000) NOT NULL,
    
    Status NVARCHAR(50) NOT NULL,  -- Pending, Approved, Denied, Expired, Active
    RequiresApproval BIT NOT NULL,
    
    RequestedAt DATETIME2 NOT NULL DEFAULT GETUTCDATE(),
    ApprovedBy NVARCHAR(100),
    ApprovedAt DATETIME2,
    DeniedBy NVARCHAR(100),
    DeniedAt DATETIME2,
    DenialReason NVARCHAR(500),
    
    SSHCertificateIssued BIT NOT NULL DEFAULT 0,
    VaultCertificatePath NVARCHAR(500),
    CertificateSerialNumber NVARCHAR(100),
    CertificateExpiresAt DATETIME2,
    
    CamundaProcessInstanceId NVARCHAR(100),
    
    INDEX IX_RequestId (RequestId),
    INDEX IX_UserId (UserId),
    INDEX IX_Status (Status),
    INDEX IX_RequestedAt (RequestedAt DESC)
);

CREATE TABLE BastionSessions (
    Id BIGINT IDENTITY(1,1) PRIMARY KEY,
    SessionId UNIQUEIDENTIFIER NOT NULL DEFAULT NEWID() UNIQUE,
    AccessRequestId UNIQUEIDENTIFIER,
    
    Username NVARCHAR(100) NOT NULL,
    ClientIp NVARCHAR(50) NOT NULL,
    BastionHost NVARCHAR(100) NOT NULL,
    TargetHost NVARCHAR(100),
    
    StartTime DATETIME2 NOT NULL DEFAULT GETUTCDATE(),
    EndTime DATETIME2,
    DurationSeconds INT,
    
    RecordingPath NVARCHAR(500),  -- MinIO path
    RecordingSize BIGINT,  -- Bytes
    
    Status NVARCHAR(50) NOT NULL,  -- Active, Completed, Terminated
    TerminationReason NVARCHAR(200),
    
    CommandCount INT NOT NULL DEFAULT 0,  -- Number of commands executed
    
    FOREIGN KEY (AccessRequestId) REFERENCES BastionAccessRequests(RequestId),
    INDEX IX_SessionId (SessionId),
    INDEX IX_Username (Username),
    INDEX IX_StartTime (StartTime DESC),
    INDEX IX_Status (Status)
);

CREATE TABLE EmergencyAccessLogs (
    Id BIGINT IDENTITY(1,1) PRIMARY KEY,
    EmergencyId UNIQUEIDENTIFIER NOT NULL DEFAULT NEWID() UNIQUE,
    
    RequestedBy NVARCHAR(100) NOT NULL,
    ApprovedBy1 NVARCHAR(100) NOT NULL,  -- First approver
    ApprovedBy2 NVARCHAR(100) NOT NULL,  -- Second approver
    
    IncidentTicketId NVARCHAR(100) NOT NULL,
    Justification NVARCHAR(1000) NOT NULL,
    
    RequestedAt DATETIME2 NOT NULL DEFAULT GETUTCDATE(),
    GrantedAt DATETIME2 NOT NULL,
    ExpiresAt DATETIME2 NOT NULL,
    
    VaultOneTimeToken NVARCHAR(200),  -- One-time use token
    TokenUsed BIT NOT NULL DEFAULT 0,
    TokenUsedAt DATETIME2,
    
    PostIncidentReviewCompleted BIT NOT NULL DEFAULT 0,
    ReviewCompletedAt DATETIME2,
    ReviewNotes NVARCHAR(MAX),
    
    INDEX IX_EmergencyId (EmergencyId),
    INDEX IX_RequestedAt (RequestedAt DESC),
    INDEX IX_IncidentTicketId (IncidentTicketId)
);

-- View for active bastion sessions
CREATE VIEW vw_ActiveBastionSessions AS
SELECT 
    s.SessionId,
    s.Username,
    s.ClientIp,
    s.BastionHost,
    s.TargetHost,
    s.StartTime,
    DATEDIFF(MINUTE, s.StartTime, GETUTCDATE()) AS DurationMinutes,
    ar.Environment,
    ar.Justification
FROM BastionSessions s
LEFT JOIN BastionAccessRequests ar ON s.AccessRequestId = ar.RequestId
WHERE s.Status = 'Active'
ORDER BY s.StartTime DESC;
GO
```

---

## Integration Verification

### IV1: Bastion Infrastructure Deployment
**Verification Steps**:
1. Deploy Terraform infrastructure
2. Verify 2 bastion hosts running
3. Check load balancer health probes passing
4. Test SSH connection to bastion public IP
5. Verify network security groups blocking unauthorized IPs
6. Check auto-scaling rules configured

**Success Criteria**:
- Bastion hosts accessible via load balancer
- Health checks passing
- Network isolation verified
- Auto-scaling functional

### IV2: SSH Certificate Authentication
**Verification Steps**:
1. Request bastion access via Admin UI
2. Approve access request (if required)
3. Download SSH certificate
4. Connect to bastion using certificate
5. Verify certificate expires after TTL
6. Check Vault audit log for certificate issuance

**Success Criteria**:
- SSH certificate issued successfully
- Certificate-based authentication works
- Certificate expires at correct time
- Audit trail complete

### IV3: Session Recording and Playback
**Verification Steps**:
1. Connect to bastion host
2. Execute several commands
3. Disconnect from bastion
4. Check session recording uploaded to MinIO
5. Access session playback via Admin UI
6. Verify recording shows all commands

**Success Criteria**:
- Session recorded automatically
- Recording uploaded to MinIO
- Playback accessible via UI
- All commands visible in recording

---

## Testing Strategy

### Unit Tests

```csharp
[Fact]
public async Task RequestAccess_ProductionEnvironment_RequiresApproval()
{
    // Arrange
    var service = CreateBastionService();
    var request = new BastionAccessRequest
    {
        Environment = "production",
        AccessDurationHours = 4,
        Justification = "Need to investigate production database performance issue reported in ticket INC-12345"
    };

    // Act
    var result = await service.RequestAccessAsync(request, "USER001", "John Doe", CancellationToken.None);

    // Assert
    Assert.True(result.RequiresApproval);
    Assert.Equal("Pending", result.Status);
}
```

### Integration Tests

```bash
#!/bin/bash
# test-bastion-access.sh

echo "Testing bastion access workflow..."

# Test 1: Request access
echo "Test 1: Request bastion access"
REQUEST_ID=$(curl -s -X POST "$ADMIN_API/bastion/access-requests" \
  -H "Authorization: Bearer $TOKEN" \
  -H "Content-Type: application/json" \
  -d '{
    "environment": "staging",
    "accessDurationHours": 2,
    "justification": "Testing bastion access workflow for automated testing purposes"
  }' | jq -r '.requestId')

if [ -n "$REQUEST_ID" ]; then
  echo "✅ Access request created: $REQUEST_ID"
else
  echo "❌ Failed to create access request"
  exit 1
fi

# Test 2: Download certificate
echo "Test 2: Download SSH certificate"
curl -s -X GET "$ADMIN_API/bastion/access-requests/$REQUEST_ID/certificate" \
  -H "Authorization: Bearer $TOKEN" \
  -o bastion-cert.pub

if [ -f bastion-cert.pub ]; then
  echo "✅ Certificate downloaded"
else
  echo "❌ Certificate download failed"
  exit 1
fi

# Test 3: Connect to bastion
echo "Test 3: Test SSH connection"
ssh -i ~/.ssh/id_rsa \
    -o CertificateFile=bastion-cert.pub \
    -o UserKnownHostsFile=/dev/null \
    -o StrictHostKeyChecking=no \
    ubuntu@bastion.intellifin.com \
    "echo 'Bastion connection successful'"

if [ $? -eq 0 ]; then
  echo "✅ SSH connection successful"
else
  echo "❌ SSH connection failed"
  exit 1
fi

echo "All tests passed! ✅"
```

---

## Risks and Mitigation

| Risk | Impact | Probability | Mitigation |
|------|---------|-------------|------------|
| Bastion host compromise | Lateral movement to private network | Low | Network segmentation. MFA required. Session recording. Regular security updates. Intrusion detection. |
| SSH CA key compromise | Unauthorized certificate issuance | Low | Store CA key in Vault with strict access controls. Rotate keys quarterly. Monitor certificate issuance. |
| Session recording failure | Loss of audit trail | Low | Real-time upload to MinIO. Backup recordings locally. Alert on upload failures. |
| DDoS attack on bastion | Access unavailable | Medium | DDoS protection on load balancer. Rate limiting. Geo-blocking. Auto-scaling. |
| Emergency access abuse | Unauthorized production access | Low | Two-person authorization. Post-incident review mandatory. PagerDuty alerts. Complete audit trail. |

---

## Definition of Done

- [ ] Terraform infrastructure deployed (bastion hosts, load balancer, networking)
- [ ] Vault SSH secrets engine configured
- [ ] SSH certificate authentication tested
- [ ] Session recording with Asciinema configured
- [ ] MFA integration with PAM completed
- [ ] Admin Service API endpoints implemented
- [ ] Database schema created
- [ ] Bastion dashboard in Admin UI
- [ ] Grafana monitoring dashboards configured
- [ ] Emergency break-glass procedure documented and tested
- [ ] Integration tests: Access request, certificate auth, session recording
- [ ] Security hardening (CIS benchmarks) applied
- [ ] Network segmentation verified
- [ ] Documentation: Access procedures, troubleshooting
- [ ] Runbooks: Bastion recovery, emergency access

---

## Related Documentation

### PRD References
- **Lines 1283-1307**: Story 1.27 detailed requirements
- **Lines 1244-1408**: Phase 5 (Observability & Infrastructure) overview
- **NFR17**: All infrastructure access via bastion
- **NFR18**: Session recording 100%

### Architecture References
- **Section 12**: Bastion Architecture
- **Section 10**: Vault Integration
- **Section 7**: Network Security

### External Documentation
- [Vault SSH Secrets Engine](https://developer.hashicorp.com/vault/docs/secrets/ssh)
- [Asciinema Documentation](https://asciinema.org/)
- [CIS Ubuntu Benchmarks](https://www.cisecurity.org/benchmark/ubuntu_linux)
- [SSH Certificate Authentication](https://man.openbsd.org/ssh-keygen#CERTIFICATES)

---

## Notes for Development Team

### Pre-Implementation Checklist
- [ ] Plan network architecture with network team
- [ ] Reserve IP address ranges for bastion subnet
- [ ] Configure corporate VPN/firewall to allow bastion access
- [ ] Generate Vault SSH CA keys
- [ ] Set up MinIO bucket for session recordings
- [ ] Create DNS records for bastion.intellifin.com
- [ ] Prepare emergency break-glass procedures
- [ ] Document bastion access workflow for users

### Post-Implementation Handoff
- [ ] Train operations team on bastion management
- [ ] Train developers on SSH certificate workflow
- [ ] Create user guide for accessing bastion
- [ ] Set up 24/7 monitoring for bastion availability
- [ ] Schedule quarterly security audits
- [ ] Establish SLA for bastion access requests (< 1 hour for staging, < 4 hours for production)
- [ ] Create incident response plan for bastion compromise
- [ ] Document disaster recovery procedures

### Technical Debt / Future Enhancements
- [ ] Implement Just-In-Time VM creation (ephemeral bastions)
- [ ] Add support for Windows RDP session recording
- [ ] Integrate with privileged access management platform (CyberArk, BeyondTrust)
- [ ] Implement session analytics (detect suspicious commands)
- [ ] Add geolocation-based access controls
- [ ] Create bastion access mobile app
- [ ] Implement passwordless authentication (FIDO2)
- [ ] Add AI-powered anomaly detection for sessions

---

**Story Created**: 2025-10-11  
**Last Updated**: 2025-10-11  
**Next Story**: [Story 1.28: JIT Infrastructure Access with Vault](./story-1.28-jit-vault-ssh.md)
