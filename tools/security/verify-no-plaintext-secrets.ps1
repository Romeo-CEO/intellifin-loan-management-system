[CmdletBinding()]
param(
    [string[]]$Patterns = @(
        '(?i)password\s*=\s*[^"\s]*',
        '(?i)accesskey\s*=\s*[^"\s]*',
        '(?i)secretkey\s*=\s*[^"\s]*',
        '(?i)signingkey\s*=\s*[^"\s]*',
        '(?i)changeme'
    )
)

$ErrorActionPreference = 'Stop'

$root = git rev-parse --show-toplevel
if (-not $root) {
    Write-Error 'Unable to determine git repository root.'
    exit 1
}

$ignorePatterns = @(
    '\\.git[\\/]',
    'node_modules[\\/]',
    '[\\/]bin[\\/]',
    '[\\/]obj[\\/]',
    '\\.terraform[\\/]',
    'TestResults[\\/]',
    '\\.nx[\\/]',
    'coverage-report[\\/]',
    'dist[\\/]'
)

$findings = @()

Get-ChildItem -Path $root -Recurse -File | ForEach-Object {
    $fullPath = $_.FullName
    $relativePath = $fullPath.Substring($root.Length + 1)

    if ($_.Name -like '*.template.json') {
        return
    }

    foreach ($pattern in $ignorePatterns) {
        if ($relativePath -match $pattern) {
            return
        }
    }

    try {
        $lineNumber = 0
        Get-Content -Path $fullPath | ForEach-Object {
            $lineNumber++
            $line = $_

            if ($line -match 'secret-scan:ignore') {
                return
            }

            foreach ($pattern in $Patterns) {
                if ($line -match $pattern) {
                    $findings += [PSCustomObject]@{
                        File    = $relativePath
                        Line    = $lineNumber
                        Pattern = $pattern
                        Snippet = $line.Trim()
                    }
                    break
                }
            }
        }
    }
    catch {
        Write-Verbose "Skipping unreadable file $relativePath: $_"
    }
}

if ($findings.Count -gt 0) {
    Write-Error "Plaintext secrets detected:`n" -ErrorAction Continue
    $findings | ForEach-Object {
        Write-Error "  $($_.File):$($_.Line) matches pattern '$($_.Pattern)' -> $($_.Snippet)" -ErrorAction Continue
    }
    Write-Error "Add 'secret-scan:ignore' to lines that are intentionally whitelisted." -ErrorAction Continue
    exit 1
}

Write-Host 'Secret scan completed. No plaintext credentials detected.'
