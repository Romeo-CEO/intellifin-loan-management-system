output "bastion_public_ip" {
  description = "Public IP address assigned to the bastion load balancer."
  value       = azurerm_public_ip.bastion.ip_address
}

output "bastion_fqdn" {
  description = "FQDN for end-user bastion access."
  value       = "bastion.${var.domain}"
}
