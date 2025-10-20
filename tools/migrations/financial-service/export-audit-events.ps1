param(
    [Parameter(Mandatory = $true)]
    [string]$SqlConnectionString,

    [Parameter(Mandatory = $true)]
    [string]$AdminServiceBaseUrl,

    [Parameter(Mandatory = $false)]
    [int]$BatchSize = 500
)

$ErrorActionPreference = 'Stop'

Write-Host "Starting FinancialService audit export..." -ForegroundColor Cyan

$offset = 0
$hasMore = $true

while ($hasMore) {
    $query = @"
SELECT TOP ($BatchSize)
    Id,
    Actor,
    Action,
    EntityType,
    EntityId,
    OccurredAtUtc,
    Data
FROM AuditEvents
WHERE Id NOT IN (SELECT TOP ($offset) Id FROM AuditEvents ORDER BY OccurredAtUtc)
ORDER BY OccurredAtUtc
"@

    $events = Invoke-Sqlcmd -ConnectionString $SqlConnectionString -Query $query

    if (-not $events -or $events.Count -eq 0) {
        $hasMore = $false
        break
    }

    $payload = @{
        events = @()
    }

    foreach ($row in $events) {
        $eventData = $null
        if ($row.Data) {
            try {
                $eventData = $row.Data | ConvertFrom-Json -ErrorAction Stop
            } catch {
                $eventData = @{ raw = $row.Data }
            }
        }

        $payload.events += @{
            timestamp    = [DateTime]::SpecifyKind($row.OccurredAtUtc, [DateTimeKind]::Utc)
            actor        = $row.Actor
            action       = $row.Action
            entityType   = $row.EntityType
            entityId     = $row.EntityId
            eventData    = $eventData
            migrationSource = 'FinancialService.Sql'
        }
    }

    $body = $payload | ConvertTo-Json -Depth 6
    $response = Invoke-RestMethod -Uri "$AdminServiceBaseUrl/api/admin/audit/events/batch" -Method Post -Body $body -ContentType 'application/json'
    Write-Host "Exported batch of $($payload.events.Count) events (offset $offset)" -ForegroundColor Green

    $offset += $BatchSize
    Start-Sleep -Milliseconds 250
}

Write-Host "Audit export completed." -ForegroundColor Cyan
