# IntelliFin Monorepo

Quick start:

- First-time setup: `pwsh -File tools/scripts/setup-solution.ps1 -Build -ComposeProjectName intf_dev`
- macOS/Linux wrapper: `tools/scripts/setup-solution.sh -Build -ComposeProjectName intf_dev`

Requirements:
- .NET 9 SDK
- Node.js 18+
- Docker (optional for scaffolding, required for compose)
- PowerShell 7+ (pwsh)

Port/stack configuration:
- To avoid collisions with your existing stacks, the script writes a `.env` with defaults:
  - `COMPOSE_PROJECT_NAME=intf_dev`
  - `MSSQL_PORT=31433`, `RABBITMQ_AMQP_PORT=35672`, `RABBITMQ_HTTP_PORT=35673`, `REDIS_PORT=36379`, `MINIO_API_PORT=39000`, `MINIO_CONSOLE_PORT=39001`, `VAULT_PORT=38200`
- Edit `.env` to customize ports/project name as needed.
- Bring up infra with: `docker compose --project-name <name> up -d` (or rely on `.env`).

What the script does:
- Creates IntelliFin.sln and all .NET projects under apps/libs/tests
- Scaffolds Next.js (apps/IntelliFin.Frontend) with Tailwind
- Generates docker-compose.yml for SQL Server, RabbitMQ, Redis, MinIO, Vault (ports via `.env`)
- Adds a virtual docs folder to the solution
- Idempotent; safe to run multiple times

