# Pester tests for setup-solution.ps1
# Run with: pwsh -c "Invoke-Pester -Path tools/scripts/tests -Output Detailed"

BeforeAll {
  Set-Location (Resolve-Path '../../..')
}

Describe 'IntelliFin Setup Script' {
  It 'runs without error from clean state and is idempotent' -Tag 'integration' {
    $script = 'tools/scripts/setup-solution.ps1'
    if (Test-Path 'IntelliFin.sln') { Remove-Item 'IntelliFin.sln' -Force }
    if (Test-Path 'apps') { Remove-Item 'apps' -Recurse -Force }
    if (Test-Path 'libs') { Remove-Item 'libs' -Recurse -Force }
    if (Test-Path 'tests') { Remove-Item 'tests' -Recurse -Force }
    if (Test-Path 'docs') { Remove-Item 'docs' -Recurse -Force }
    if (Test-Path 'docker-compose.yml') { Remove-Item 'docker-compose.yml' -Force }
    
    & pwsh -File $script -VerboseLogs | Should -Not -Throw
    Test-Path 'IntelliFin.sln' | Should -BeTrue

    # Idempotency: run again
    & pwsh -File $script -VerboseLogs | Should -Not -Throw
  }
}

