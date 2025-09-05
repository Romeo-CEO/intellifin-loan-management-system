## IntelliFin Setup Scripts

Commands:
- Windows/macOS/Linux (with PowerShell 7):
  - `pwsh -File tools/scripts/setup-solution.ps1 [-SkipFrontend] [-SkipDocker] [-Build] [-VerboseLogs]`
- macOS/Linux helper:
  - `tools/scripts/setup-solution.sh [-SkipFrontend] [-SkipDocker] [-Build] [-VerboseLogs]`

What it creates:
- IntelliFin.sln with solution folders (apps, libs, tests) and a docs virtual folder
- .NET 9 WebAPI projects for services, classlibs for shared libs, xUnit test projects
- apps/IntelliFin.Frontend Next.js app (TypeScript, Tailwind) with React Query + Zustand
- docker-compose.yml with SQL Server, RabbitMQ, Redis, MinIO, Vault

Idempotent: safe to re-run; creates missing items only.

