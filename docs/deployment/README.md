# Deployment Guide

## Prerequisites

- .NET 9 SDK
- Docker Desktop
- Node.js 20+ (for frontend)
- PowerShell 7+ (Windows) or Bash (Linux/macOS)

## Quick Start

### 1. Clone and Setup

```bash
git clone <repository-url>
cd "IntelliFin Loan Management System"
```

### 2. Start Infrastructure Services

```bash
# Start Docker services
docker-compose up -d

# Verify services are running
docker-compose ps
```

Expected services:
- SQL Server (port 31433)
- RabbitMQ (ports 35672, 15672)
- Redis (port 36379)
- MinIO (ports 39000, 39001)
- Vault (port 38200)

### 3. Database Setup

```bash
# Apply migrations
dotnet ef database update -p libs/IntelliFin.Shared.DomainModels -s libs/IntelliFin.Shared.DomainModels
```

### 4. Build and Run Services

```bash
# Build solution
dotnet build IntelliFin.sln -c Release

# Start services (in separate terminals)
dotnet run --project apps/IntelliFin.IdentityService -c Release
dotnet run --project apps/IntelliFin.ApiGateway -c Release
dotnet run --project apps/IntelliFin.ClientManagement -c Release
dotnet run --project apps/IntelliFin.LoanOrigination -c Release
dotnet run --project apps/IntelliFin.Communications -c Release
```

### 5. Verify Deployment

```bash
# Check health endpoints
curl http://localhost:5235/health  # IdentityService
curl http://localhost:5033/health  # API Gateway
curl http://localhost:5224/health  # ClientManagement
curl http://localhost:5193/health  # LoanOrigination
curl http://localhost:5218/health  # Communications
```

## Service Ports

| Service | Port | Purpose |
|---------|------|---------|
| IdentityService | 5235 | Authentication |
| API Gateway | 5033 | Unified API access |
| ClientManagement | 5224 | Client operations |
| LoanOrigination | 5193 | Loan processing |
| Communications | 5218 | Notifications |

## Infrastructure Ports

| Service | Port | Purpose |
|---------|------|---------|
| SQL Server | 31433 | Database |
| RabbitMQ | 35672 | Message broker |
| RabbitMQ Management | 15672 | Web UI |
| Redis | 36379 | Caching |
| MinIO | 39000 | Object storage |
| MinIO Console | 39001 | Web UI |
| Vault | 38200 | Secrets management |

## Configuration

### Environment Variables

```bash
# Database
CONNECTIONSTRINGS__DEFAULTCONNECTION="Server=localhost,31433;Database=IntelliFinLms;User Id=sa;Password=Your_password123;TrustServerCertificate=true"

# RabbitMQ
RABBITMQ__HOST="localhost"
RABBITMQ__PORT="35672"
RABBITMQ__USERNAME="guest"
RABBITMQ__PASSWORD="guest"

# JWT
JWT__ISSUER="IntelliFin.Identity"
JWT__AUDIENCE="intellifin-api"
JWT__SIGNINGKEY="dev-super-secret-signing-key-change-me-please-1234567890"
```

### Docker Compose Override

Create `docker-compose.override.yml` for local customizations:

```yaml
version: '3.8'
services:
  sqlserver:
    ports:
      - "1433:1433"  # Use standard port locally
```

## Testing the Deployment

### 1. Get Authentication Token

```bash
curl -X POST http://localhost:5235/auth/dev-token \
  -H "Content-Type: application/json" \
  -d '{"username":"dev","roles":["Admin"]}'
```

### 2. Test API Gateway

```bash
# Use token from step 1
curl -H "Authorization: Bearer <token>" \
  http://localhost:5033/api/clients/
```

### 3. Test Message Flow

```bash
# Create loan application
curl -X POST http://localhost:5033/api/origination/loan-applications \
  -H "Authorization: Bearer <token>" \
  -H "Content-Type: application/json" \
  -d '{
    "clientId": "11111111-1111-1111-1111-111111111111",
    "amount": 50000,
    "termMonths": 12,
    "productCode": "PAYROLL"
  }'
```

Check Communications service logs for message consumption.

## Troubleshooting

### Common Issues

1. **Port conflicts**: Check if ports are already in use
2. **Docker not running**: Ensure Docker Desktop is started
3. **Database connection**: Verify SQL Server container is healthy
4. **Message broker**: Check RabbitMQ management UI

### Logs

```bash
# View service logs
docker-compose logs sqlserver
docker-compose logs rabbitmq

# View application logs
dotnet run --project apps/IntelliFin.ApiGateway --verbosity detailed
```

### Reset Environment

```bash
# Stop all services
docker-compose down -v

# Remove all containers and volumes
docker system prune -a --volumes

# Restart
docker-compose up -d
```
