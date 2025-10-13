variable "environment" {
  description = "Deployment environment (dev/staging/production)."
  type        = string
}

variable "location" {
  description = "Azure region for bastion deployment."
  type        = string
}

variable "domain" {
  description = "Base DNS domain for IntelliFin."
  type        = string
}

variable "vault_address" {
  description = "HashiCorp Vault endpoint used for SSH CA operations."
  type        = string
}

variable "corporate_ip_ranges" {
  description = "IP ranges permitted to access the bastion load balancer."
  type        = list(string)
}

variable "admin_public_key_path" {
  description = "Path to the administrative SSH public key."
  type        = string
}

variable "tags" {
  description = "Common resource tags."
  type        = map(string)
  default     = {}
}
