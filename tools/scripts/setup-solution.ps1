#!/usr/bin/env pwsh
#requires -Version 7.2
<#!
IntelliFin Automated Monorepo Setup Script
- Creates Nx workspace scaffolding
- Creates IntelliFin.sln and all .NET 9 projects organized into solution folders
- Scaffolds Next.js (TypeScript + Tailwind) frontend app
- Generates docker-compose.yml for local dev infrastructure
- Idempotent: safe to re-run; creates only what is missing

Run:
  pwsh -File tools/scripts/setup-solution.ps1
or on Windows PowerShell 7+:
  ./tools/scripts/setup-solution.ps1
!#>

[CmdletBinding(SupportsShouldProcess=$true)]
param(
  [switch]$SkipFrontend,
  [switch]$SkipDocker,
  [switch]$Build,
  [string]$ComposeProjectName = 'intellifin',
  [switch]$VerboseLogs
)

Set-StrictMode -Version Latest
$ErrorActionPreference = 'Stop'

function Write-Info($msg) { Write-Host "[INFO ] $msg" -ForegroundColor Cyan }
function Write-Ok($msg)   { Write-Host "[ OK  ] $msg" -ForegroundColor Green }
function Write-Warn($msg) { Write-Warning $msg }
function Write-Err($msg)  { Write-Host "[FAIL ] $msg" -ForegroundColor Red }

$Root = (Resolve-Path .).Path
$SolutionName = 'IntelliFin'
$SlnPath = Join-Path $Root "$SolutionName.sln"
$AppsDir = Join-Path $Root 'apps'
$LibsDir = Join-Path $Root 'libs'
$TestsDir = Join-Path $Root 'tests'
$ToolsDir = Join-Path $Root 'tools'
$DocsDir = Join-Path $Root 'docs'
$DockerComposePath = Join-Path $Root 'docker-compose.yml'
$FrontendDir = Join-Path $AppsDir 'IntelliFin.Frontend'

function Ensure-Directory([string]$Path) {
  if (-not (Test-Path $Path)) { New-Item -ItemType Directory -Path $Path | Out-Null }
}

function Test-CommandExists([string]$cmd) {
  $null -ne (Get-Command $cmd -ErrorAction SilentlyContinue)
}

function Ensure-Tools() {
  $ok = $true
  foreach ($c in @('dotnet','node','npm','npx')) {
    if (-not (Test-CommandExists $c)) { Write-Err "$c is required but not found in PATH"; $ok = $false } else { Write-Ok "$c found: $((Get-Command $c).Source)" }
  }
  if (-not (Test-CommandExists 'docker')) { Write-Warn 'docker not found; you can still scaffold, but docker-compose up will fail.' }
  return $ok
}

function Run([string]$exe, [string[]]$cmdArgs, [string]$cwd=$Root, [int]$timeoutSec=3600) {
  # Quote any arg that contains whitespace or special characters
  function QuoteArg([string]$a) {
    if ($a -match '^[A-Za-z0-9_\-\./:]+$') { return $a }
    return '"' + ($a -replace '"','\"') + '"'
  }
  $qArgs = @($cmdArgs | ForEach-Object { QuoteArg $_ })
  if ($VerboseLogs) { Write-Info ("$exe " + ($qArgs -join ' ')) }

  # Detect Windows
  $onWindows = [System.Runtime.InteropServices.RuntimeInformation]::IsOSPlatform([System.Runtime.InteropServices.OSPlatform]::Windows)
  $resolved = Get-Command $exe -ErrorAction SilentlyContinue

  # Build process start info based on platform/command
  $psi = $null
  if ($onWindows -and ($exe -in @('npm','npx'))) {
    # Use cmd.exe to reliably run npm/npx on Windows regardless of ps1/cmd shims
    $cmdStr = "/c $exe " + ($qArgs -join ' ')
    $psi = [System.Diagnostics.ProcessStartInfo]::new('cmd.exe', $cmdStr)
  } elseif ($resolved -and $resolved.Source -like '*.ps1') {
    # If the command is a PowerShell script, invoke via pwsh/powershell
    $pwsh = (Get-Command 'pwsh' -ErrorAction SilentlyContinue)?.Source
    if (-not $pwsh) { $pwsh = (Get-Command 'powershell' -ErrorAction SilentlyContinue).Source }
    $quotedScript = '"' + $resolved.Source + '"'
    $pwshArgs = "-NoLogo -NoProfile -ExecutionPolicy Bypass -File $quotedScript " + ($qArgs -join ' ')
    $psi = [System.Diagnostics.ProcessStartInfo]::new($pwsh, $pwshArgs)
  } else {
    $psi = [System.Diagnostics.ProcessStartInfo]::new($exe, ($qArgs -join ' '))
  }

  $psi.RedirectStandardOutput = $true
  $psi.RedirectStandardError = $true
  $psi.UseShellExecute = $false
  $psi.WorkingDirectory = $cwd

  $p = New-Object System.Diagnostics.Process
  $p.StartInfo = $psi
  $global:lastErr = ''
  $p.EnableRaisingEvents = $true
  $handlerOut = [System.Diagnostics.DataReceivedEventHandler] { param($s,$e) if ($e.Data) { if ($VerboseLogs) { Write-Host $e.Data } } }
  $handlerErr = [System.Diagnostics.DataReceivedEventHandler] { param($s,$e) if ($e.Data) { $script:lastErr = $e.Data; Write-Host $e.Data -ForegroundColor DarkRed } }

  [void]$p.Start()
  $p.BeginOutputReadLine()
  $p.BeginErrorReadLine()

  $exited = $p.WaitForExit($timeoutSec * 1000)
  if (-not $exited) {
    try { $p.Kill($true) } catch {}
    throw "Command timed out after $timeoutSec seconds: $exe"
  }
  if ($p.ExitCode -ne 0) { throw "Command failed ($exe) with exit code $($p.ExitCode): $script:lastErr" }
  return $true
}

function Ensure-NxWorkspace() {
  # Create package.json and nx.json at repo root via package manager commands to avoid manual edits
  if (-not (Test-Path (Join-Path $Root 'package.json'))) {
    Write-Info 'Initializing npm workspace (package.json)'
    Run -exe 'npm' -cmdArgs @('init','-y') -cwd $Root | Out-Null
    Run -exe 'npm' -cmdArgs @('pkg','set','name=intellifin','private=true') -cwd $Root | Out-Null
    Run -exe 'npm' -cmdArgs @('pkg','set','"workspaces[0]"=apps/*') -cwd $Root | Out-Null
    Run -exe 'npm' -cmdArgs @('pkg','set','"workspaces[1]"=libs/*') -cwd $Root | Out-Null
  }
  # Install Nx and initialize only if nx.json is missing
  $nxJson = Join-Path $Root 'nx.json'
  if (-not (Test-Path $nxJson)) {
    Write-Info 'Ensuring Nx workspace (nx.json)'
    try {
      Run -exe 'npx' -cmdArgs @('nx@latest','init','-y','--yes') -cwd $Root | Out-Null
    } catch {
      Write-Warn "nx init may have already been applied: $($_.Exception.Message)"
    }
  } else {
    Write-Ok 'nx.json already present; skipping nx init'
  }
  # Ensure .nvmrc for Node suggestion (non-binding)
  $nvmrc = Join-Path $Root '.nvmrc'
  if (-not (Test-Path $nvmrc)) { Set-Content -Path $nvmrc -Value "18" }
}

function Ensure-DotnetSolution() {
  if (-not (Test-Path $SlnPath)) {
    Write-Info "Creating solution $SolutionName.sln"
    Run 'dotnet' @('new','sln','-n',$SolutionName) $Root | Out-Null
  } else { Write-Ok "$SolutionName.sln already exists" }
}

function Get-ProjectsInSolution() {
  try {
    $list = Run 'dotnet' @('sln',$SlnPath,'list') $Root
    return ($list -split "`n") | Where-Object { $_ -match '\.csproj' } | ForEach-Object { $_.Trim() }
  } catch { return @() }
}

function Add-ProjectToSolution([string]$csprojPath, [string]$solutionFolder) {
  $existing = Get-ProjectsInSolution | ForEach-Object { $_.Replace('/','\') }
  $rel = [System.IO.Path]::GetRelativePath($Root, (Resolve-Path $csprojPath)).Replace('/','\\')
  if ($existing -contains $rel) { Write-Ok "Project already in solution: $rel"; return }
  $args = @('sln',$SlnPath,'add',$csprojPath)
  if ($solutionFolder) { $args += @('--solution-folder', $solutionFolder) }
  Run 'dotnet' $args $Root | Out-Null
}

function Ensure-DotnetProject([string]$projName, [string]$template, [string]$parentDir, [string]$solutionFolder, [string[]]$extraArgs=@()) {
  Ensure-Directory $parentDir
  $projDir = Join-Path $parentDir $projName
  $csproj = Join-Path $projDir "$projName.csproj"
  if (-not (Test-Path $csproj)) {
    Write-Info "Creating $template project: $projName"
    Ensure-Directory $projDir
    $args = @('new',$template,'-n',$projName,'-o',$projDir,'--framework','net9.0') + $extraArgs
    # Use full path to dotnet to avoid PATH/shim confusion
    $dotnet = (Get-Command 'dotnet').Source
    Run -exe $dotnet -cmdArgs $args -cwd $parentDir | Out-Null
  } else { Write-Ok "$projName exists" }
  Add-ProjectToSolution $csproj $solutionFolder
}

function Ensure-Frontend() {
  if ($SkipFrontend) { Write-Warn 'Skipping frontend scaffolding as requested.'; return }
  if (-not (Test-Path $FrontendDir)) {
    Ensure-Directory $AppsDir
    Write-Info 'Scaffolding Next.js app (TypeScript + Tailwind)'
    Run -exe 'npx' -cmdArgs @('create-next-app@latest',$FrontendDir,'--ts','--eslint','--tailwind','--app','--use-npm','--no-src-dir') -cwd $Root | Out-Null
    # Install React Query and Zustand in app workspace
    Write-Info 'Installing React Query and Zustand in frontend app'
    Run -exe 'npm' -cmdArgs @('install','@tanstack/react-query@latest','zustand@latest') -cwd $FrontendDir | Out-Null
  } else { Write-Ok 'Frontend app already exists' }
  # Ensure tablet-optimized Tailwind screens
  $cfgCandidates = @('tailwind.config.ts','tailwind.config.js') | ForEach-Object { Join-Path $FrontendDir $_ }
  $cfgPath = $cfgCandidates | Where-Object { Test-Path $_ } | Select-Object -First 1
  if ($cfgPath) {
    $text = Get-Content -Path $cfgPath -Raw
    if ($text -notmatch 'screens\s*:') {
      Write-Info 'Adding Tailwind custom screens (tablet/laptop/desktop)'
      $insert = "screens: {`n        tablet: '768px',`n        laptop: '1024px',`n        desktop: '1280px',`n      },`n      "
      $text = $text -replace 'extend:\s*{', ("extend: {`n      " + $insert)
      Set-Content -Path $cfgPath -Value $text -NoNewline:$false
    }
  }
}

function Ensure-DockerComposeEnv() {
  $envPath = Join-Path $Root '.env'
  if (-not (Test-Path $envPath)) {
    Write-Info 'Creating default .env for docker-compose ports and credentials'
    @(
      "COMPOSE_PROJECT_NAME=$ComposeProjectName",
      'MSSQL_SA_PASSWORD=Your_password123',
      'MSSQL_PORT=31433',
      'RABBITMQ_AMQP_PORT=35672',
      'RABBITMQ_HTTP_PORT=35673',
      'REDIS_PORT=36379',
      'MINIO_API_PORT=39000',
      'MINIO_CONSOLE_PORT=39001',
      'VAULT_PORT=38200'
    ) | Set-Content -Path $envPath -NoNewline:$false
  }
}

function Ensure-DockerCompose() {
  if ($SkipDocker) { Write-Warn 'Skipping docker-compose scaffolding as requested.'; return }
  Ensure-DockerComposeEnv
  Write-Info 'Creating or updating docker-compose.yml with env-driven ports and project name'
  $content = @"
version: '3.9'
name: \\${COMPOSE_PROJECT_NAME}
services:
  mssql:
    image: mcr.microsoft.com/mssql/server:2022-latest
    container_name: \\${COMPOSE_PROJECT_NAME}-mssql
    environment:
      - ACCEPT_EULA=Y
      - MSSQL_SA_PASSWORD=\\${MSSQL_SA_PASSWORD}
    ports:
      - '\\${MSSQL_PORT}:1433'
    volumes:
      - mssql_data:/var/opt/mssql
    healthcheck:
      test: [ 'CMD', '/opt/mssql-tools/bin/sqlcmd', '-S', 'localhost', '-U', 'sa', '-P', '\\${MSSQL_SA_PASSWORD}', '-Q', 'select 1' ]
      interval: 10s
      timeout: 5s
      retries: 10

  rabbitmq:
    image: rabbitmq:3.12-management
    container_name: \\${COMPOSE_PROJECT_NAME}-rabbitmq
    ports:
      - '\\${RABBITMQ_AMQP_PORT}:5672'
      - '\\${RABBITMQ_HTTP_PORT}:15672'
    volumes:
      - rabbitmq_data:/var/lib/rabbitmq

  redis:
    image: redis:7.2-alpine
    container_name: \\${COMPOSE_PROJECT_NAME}-redis
    ports:
      - '\\${REDIS_PORT}:6379'
    command: [ 'redis-server', '--appendonly', 'yes' ]
    volumes:
      - redis_data:/data

  minio:
    image: minio/minio:latest
    container_name: \\${COMPOSE_PROJECT_NAME}-minio
    environment:
      - MINIO_ROOT_USER=minio
      - MINIO_ROOT_PASSWORD=minio123
    command: server /data --console-address ":9001"
    ports:
      - '\\${MINIO_API_PORT}:9000'
      - '\\${MINIO_CONSOLE_PORT}:9001'
    volumes:
      - minio_data:/data

  vault:
    image: hashicorp/vault:1.15
    container_name: \\${COMPOSE_PROJECT_NAME}-vault
    cap_add:
      - IPC_LOCK
    environment:
      - VAULT_DEV_ROOT_TOKEN_ID=root
      - VAULT_DEV_LISTEN_ADDRESS=0.0.0.0:8200
    ports:
      - '\\${VAULT_PORT}:8200'

volumes:
  mssql_data: {}
  rabbitmq_data: {}
  redis_data: {}
  minio_data: {}
"@
  Set-Content -Path $DockerComposePath -Value $content -NoNewline:$false
}

function Ensure-Docs() {
  Ensure-Directory $DocsDir
  $readme = Join-Path $DocsDir 'README.md'
  if (-not (Test-Path $readme)) {
    @(
      '# IntelliFin Documentation',
      '',
      'This folder contains architecture and project documentation.'
    ) | Set-Content -Path $readme -NoNewline:$false
  }
}

function Ensure-SolutionDocsFolder() {
  Ensure-Docs
  if (-not (Test-Path $SlnPath)) { return }
  $sln = Get-Content $SlnPath -Raw
  if ($sln -match 'SolutionItems' -and $sln -match 'docs\\README.md') { Write-Ok 'docs solution folder already configured'; return }
  $folderGuid = [guid]::NewGuid().ToString().ToUpper()
  $block = @"
Project("{2150E333-8FDC-42A3-9474-1A3956D46DE8}") = "docs", "docs", "{$folderGuid}"
	ProjectSection(SolutionItems) = preProject
		docs\README.md = docs\README.md
	EndProjectSection
EndProject
"@
  if ($sln -match "(?ms)^Global\s*") {
    $replacement = ("$block`n" + '$1')
    $updated = $sln -replace "(?ms)^(Global\s*)", $replacement
    Set-Content -Path $SlnPath -Value $updated -NoNewline:$false
    Write-Ok 'Added docs solution folder to .sln'
  } else {
    # Fallback append
    Add-Content -Path $SlnPath -Value $block
  }
}

function Ensure-GlobalJson() {
  $globalJsonPath = Join-Path $Root 'global.json'
  if (-not (Test-Path $globalJsonPath)) {
    $obj = @{ sdk = @{ version = '9.0.100'; rollForward = 'latestFeature' } } | ConvertTo-Json -Depth 5
    Set-Content -Path $globalJsonPath -Value $obj -NoNewline:$false
    Write-Ok 'Created global.json to pin .NET SDK 9'
  }
}

function Main() {
  Write-Info 'Starting IntelliFin automated setup'
  if (-not (Ensure-Tools)) { throw 'Missing required tools. Please install .NET 9 SDK, Node.js 18+, and Docker.' }

  Ensure-Directory $AppsDir
  Ensure-Directory $LibsDir
  Ensure-Directory $TestsDir
  Ensure-Directory $ToolsDir
  Ensure-Directory (Join-Path $ToolsDir 'docker')
  Ensure-Directory (Join-Path $ToolsDir 'database')

  Ensure-NxWorkspace
  Ensure-GlobalJson
  Ensure-DotnetSolution

  # Applications (webapi)
  $apps = @(
    'IntelliFin.ApiGateway',
    'IntelliFin.IdentityService',
    'IntelliFin.ClientManagement',
    'IntelliFin.LoanOrigination',
    'IntelliFin.CreditBureau',
    'IntelliFin.GeneralLedger',
    'IntelliFin.PmecService',
    'IntelliFin.Collections',
    'IntelliFin.Communications',
    'IntelliFin.Reporting',
    'IntelliFin.OfflineSync'
  )
  foreach ($a in $apps) { Ensure-DotnetProject -projName $a -template 'webapi' -parentDir $AppsDir -solutionFolder 'apps' }

  # Shared libraries (classlib)
  $libs = @(
    'IntelliFin.Shared.DomainModels',
    'IntelliFin.Shared.Infrastructure',
    'IntelliFin.Shared.Authentication',
    'IntelliFin.Shared.Logging',
    'IntelliFin.Shared.Validation',
    'IntelliFin.Shared.UI'
  )
  foreach ($l in $libs) { Ensure-DotnetProject -projName $l -template 'classlib' -parentDir $LibsDir -solutionFolder 'libs' }

  # Tests
  Ensure-DotnetProject -projName 'IntelliFin.Tests.Unit' -template 'xunit' -parentDir $TestsDir -solutionFolder 'tests'
  Ensure-DotnetProject -projName 'IntelliFin.Tests.Integration' -template 'xunit' -parentDir $TestsDir -solutionFolder 'tests'
  Ensure-DotnetProject -projName 'IntelliFin.Tests.E2E' -template 'xunit' -parentDir $TestsDir -solutionFolder 'tests'

  Ensure-Frontend
  Ensure-DockerCompose
  Ensure-SolutionDocsFolder

  if ($Build) {
    Write-Info 'Building solution for verification'
    Run 'dotnet' @('build',$SlnPath,'-c','Debug') $Root | Out-Null
    Write-Ok 'Build succeeded'
  }

  Write-Ok 'Setup complete.'
  Write-Host "Next steps:" -ForegroundColor Yellow
  Write-Host "1) Open IntelliFin.sln in JetBrains Rider" -ForegroundColor Yellow
  Write-Host "2) Build the solution (should succeed)" -ForegroundColor Yellow
  Write-Host "3) From repo root: docker compose --project-name $ComposeProjectName up -d" -ForegroundColor Yellow
  Write-Host "   or set COMPOSE_PROJECT_NAME in .env first to avoid stack collisions" -ForegroundColor Yellow
}

Main

