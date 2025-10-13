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

resource "azurerm_resource_group" "bastion" {
  name     = "rg-intellifin-bastion-${var.environment}"
  location = var.location
  tags     = var.tags
}

resource "azurerm_virtual_network" "bastion" {
  name                = "vnet-intellifin-${var.environment}"
  address_space       = ["10.0.0.0/16"]
  location            = azurerm_resource_group.bastion.location
  resource_group_name = azurerm_resource_group.bastion.name
  tags                = var.tags
}

resource "azurerm_subnet" "bastion" {
  name                 = "subnet-bastion"
  resource_group_name  = azurerm_resource_group.bastion.name
  virtual_network_name = azurerm_virtual_network.bastion.name
  address_prefixes     = ["10.0.1.0/24"]
}

resource "azurerm_subnet" "application" {
  name                 = "subnet-app"
  resource_group_name  = azurerm_resource_group.bastion.name
  virtual_network_name = azurerm_virtual_network.bastion.name
  address_prefixes     = ["10.0.10.0/24"]
}

resource "azurerm_network_security_group" "bastion" {
  name                = "nsg-bastion-${var.environment}"
  resource_group_name = azurerm_resource_group.bastion.name
  location            = azurerm_resource_group.bastion.location

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

  security_rule {
    name                       = "Deny-All"
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

resource "azurerm_subnet_network_security_group_association" "bastion" {
  subnet_id                 = azurerm_subnet.bastion.id
  network_security_group_id = azurerm_network_security_group.bastion.id
}

resource "azurerm_public_ip" "bastion" {
  name                = "pip-bastion-lb-${var.environment}"
  resource_group_name = azurerm_resource_group.bastion.name
  location            = azurerm_resource_group.bastion.location
  allocation_method   = "Static"
  sku                 = "Standard"
  tags                = var.tags
}

resource "azurerm_lb" "bastion" {
  name                = "lb-bastion-${var.environment}"
  location            = azurerm_resource_group.bastion.location
  resource_group_name = azurerm_resource_group.bastion.name
  sku                 = "Standard"

  frontend_ip_configuration {
    name                 = "bastion-frontend"
    public_ip_address_id = azurerm_public_ip.bastion.id
  }
}

resource "azurerm_lb_backend_address_pool" "bastion" {
  loadbalancer_id = azurerm_lb.bastion.id
  name            = "bastion-backend"
}

resource "azurerm_lb_probe" "ssh" {
  loadbalancer_id = azurerm_lb.bastion.id
  name            = "ssh-probe"
  protocol        = "Tcp"
  port            = 22
}

resource "azurerm_lb_rule" "ssh" {
  loadbalancer_id                = azurerm_lb.bastion.id
  name                           = "ssh-rule"
  protocol                       = "Tcp"
  frontend_port                  = 22
  backend_port                   = 22
  frontend_ip_configuration_name = "bastion-frontend"
  backend_address_pool_ids       = [azurerm_lb_backend_address_pool.bastion.id]
  probe_id                       = azurerm_lb_probe.ssh.id
}

resource "azurerm_linux_virtual_machine_scale_set" "bastion" {
  name                = "vmss-bastion-${var.environment}"
  location            = azurerm_resource_group.bastion.location
  resource_group_name = azurerm_resource_group.bastion.name
  sku                 = "Standard_B2s"
  instances           = 2
  admin_username      = "bastionadmin"
  disable_password_authentication = true

  admin_ssh_key {
    username   = "bastionadmin"
    public_key = file(var.admin_public_key_path)
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
      subnet_id                              = azurerm_subnet.bastion.id
      load_balancer_backend_address_pool_ids = [azurerm_lb_backend_address_pool.bastion.id]
    }
  }

  custom_data = base64encode(templatefile("${path.module}/scripts/bastion-init.sh", {
    vault_address = var.vault_address,
    environment   = var.environment
  }))

  tags = var.tags
}

resource "azurerm_monitor_autoscale_setting" "bastion" {
  name                = "bastion-autoscale-${var.environment}"
  resource_group_name = azurerm_resource_group.bastion.name
  location            = azurerm_resource_group.bastion.location
  target_resource_id  = azurerm_linux_virtual_machine_scale_set.bastion.id

  profile {
    name = "default"

    capacity {
      default = 2
      minimum = 2
      maximum = 4
    }

    rule {
      metric_trigger {
        metric_name        = "Percentage CPU"
        metric_resource_id = azurerm_linux_virtual_machine_scale_set.bastion.id
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
        metric_resource_id = azurerm_linux_virtual_machine_scale_set.bastion.id
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
