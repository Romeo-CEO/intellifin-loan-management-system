# Bastion Host Infrastructure

Terraform configuration for deploying the IntelliFin bastion host architecture with high-availability VM scale sets, load-balancing, and network segmentation. The module provisions the network resources, load balancer, bastion scale set, and autoscaling policies required to satisfy Story 1.27.

## Usage

```
terraform init
terraform plan -var "environment=production" -var "location=eastus" -var "domain=intellifin.local"
terraform apply
```

### Required Variables

- `environment` – Deployment environment identifier (dev/staging/production).
- `location` – Azure region for the bastion infrastructure.
- `domain` – Base DNS domain (used to output the bastion FQDN).
- `vault_address` – HashiCorp Vault endpoint for SSH certificate enrolment.
- `corporate_ip_ranges` – List of CIDR blocks allowed to reach the bastion load balancer.

### Outputs

- `bastion_public_ip` – Public IP address assigned to the bastion load balancer.
- `bastion_fqdn` – Resolved FQDN for end-user connectivity.

> **Note:** The initialization script expects environment variables for `MINIO_ENDPOINT` and `ADMIN_SERVICE_URL` to be provided via cloud-init or VMSS custom data secrets.
