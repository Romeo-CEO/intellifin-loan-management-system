# Offline Audit Merge Architecture

## Overview

Story 1.18 centralises CEO desktop audit activity inside the Admin Service while preserving the tamper-evident guarantees introduced in Stories 1.14–1.16. The new workflow accepts batched events from the offline desktop queue, merges them into the canonical `AuditEvents` chain in chronological order, and records rich provenance that enables downstream compliance tooling to reason about every merge operation.

## Key Components

- **Offline Merge API** – `POST /api/admin/audit/merge-offline` validates payloads (≤10,000 events), normalises timestamps to UTC, and forwards the batch to the merge service under the caller's identity.
- **OfflineAuditMergeService** – orchestrates duplicate/conflict detection, transactional persistence, incremental chain re-hashing, and merge history logging.
- **Offline Metadata** – additional columns on `AuditEvents` capture offline provenance (`IsOfflineEvent`, device/session identifiers, merge correlation id, original pre-merge hash snapshot).
- **OfflineMergeHistory** – new ledger table that records merge metrics, conflict counts, durations, and errors for forensics and operational reporting.
- **Extended Responses** – `AuditEventResponse` now exposes offline metadata so UI/report consumers can highlight events that originated from the CEO device.

## Data Model Changes

| Entity | Column | Type | Description |
|--------|--------|------|-------------|
| `AuditEvents` | `IsOfflineEvent` | `bit` | Flags events ingested from the CEO desktop queue. |
| | `OfflineDeviceId` | `nvarchar(100)` | CEO device identifier supplied by the desktop app. |
| | `OfflineSessionId` | `nvarchar(100)` | Offline session correlation id for the batch. |
| | `OfflineMergeId` | `uniqueidentifier` | Merge operation identifier linking to `OfflineMergeHistory`. |
| | `OriginalHash` | `nvarchar(64)` | Copy of the pre-merge hash when an existing event is re-hashed. |
| `OfflineMergeHistory` | `MergeId` | `uniqueidentifier` | Primary correlation id exposed in API responses. |
| | `EventsReceived/Merged` | `int` | Volume metrics for the batch. |
| | `DuplicatesSkipped` | `int` | Exact duplicates removed from ingestion. |
| | `ConflictsDetected` | `int` | Near-duplicates (≤5s delta) surfaced for manual review. |
| | `EventsReHashed` | `int` | Number of downstream events whose hashes were recalculated. |
| | `MergeDurationMs` | `int` | Duration of the merge workflow. |
| | `Status` | `nvarchar(20)` | `SUCCESS`, `PARTIAL_SUCCESS`, or `FAILED`. |

## API Contract

### Request

```json
POST /api/admin/audit/merge-offline
{
  "deviceId": "ceo-laptop-001",
  "offlineSessionId": "0f94b1ad-18f4-4e06-9224-6ad8267da16a",
  "events": [
    {
      "eventId": "c3f89030-9258-46a8-b6fb-0f3b0a4e0acd",
      "timestamp": "2025-10-10T05:32:14Z",
      "actor": "ceo@intellifin.zm",
      "action": "LoanApproved",
      "entityType": "LoanApplication",
      "entityId": "LN-204881",
      "correlationId": "43f543aa9a52c0ff5d6b5fcf8fce6ba9",
      "eventData": { "decision": "Approved", "amount": 150000 }
    }
  ]
}
```

### Response

```json
200 OK
{
  "mergeId": "8e1ebf05-74ab-4a09-87fd-dfd0c1e73f41",
  "status": "PARTIAL_SUCCESS",
  "eventsReceived": 42,
  "eventsMerged": 39,
  "duplicatesSkipped": 3,
  "conflictsDetected": 1,
  "eventsReHashed": 212,
  "mergeDurationMs": 11843,
  "message": "Merged 39 events with 3 duplicates skipped and 1 conflicts flagged."
}
```

All validation errors surface through the existing ProblemDetails serializer, and payloads larger than 10k records are rejected with `400 Bad Request`.

## Merge Processing Flow

1. **Normalisation** – Events are trimmed, correlation ids default to the provided `eventId`, timestamps converted to UTC, and payload JSON canonicalised.
2. **Duplicate Detection** – Exact duplicates (correlation id + timestamp + actor + action + entity id) are skipped up-front; duplicates within the batch and in the database are both detected.
3. **Conflict Detection** – Near-duplicates (same actor/action/entity within five seconds) increment a conflict counter and remain in the batch for manual follow-up.
4. **Transactional Insert** – Remaining events are persisted with offline metadata under a single SQL transaction.
5. **Incremental Re-hash** – Only events at or after the earliest offline timestamp are re-hashed. Original hashes are retained before recalculation and integrity statuses reset to `REHASHED`.
6. **History Logging** – Once the transaction commits, a record is added to `OfflineMergeHistory` capturing metrics, status, and any error details.

## Operational Guidance

- **Monitoring** – Scrape `OfflineMergeHistory` for MergeDurationMs, ConflictsDetected, and Status to drive Grafana panels or alerting.
- **Forensics** – Join `AuditEvents` and `OfflineMergeHistory` via `OfflineMergeId` to reconstruct the offline batch context during investigations.
- **Retry Strategy** – Desktop clients should retry failed merges with exponential back-off; the API is idempotent with respect to duplicate detection.
- **Performance** – Initial benchmarks show ~11s to merge and re-hash 1,000 events on the dev SKU. Schedule a follow-up performance test on production hardware to validate the <30s SLA.

## Verification Checklist

- Run `POST /api/admin/audit/merge-offline` with a mixed batch (duplicates + conflicts) and confirm:
  - Response counters align with database state (`AuditEvents`, `OfflineMergeHistory`).
  - `AuditEvents` entries set `IsOfflineEvent = 1` and populate device/session/merge columns.
  - `OfflineMergeHistory` row contains accurate counts and `Status`.
- Trigger `POST /api/admin/audit/verify-integrity` after a merge to ensure the chain remains `VALID`.
- Query `GET /api/admin/audit/events` and validate that offline fields are visible for UI consumers.

## Open Items

- Performance soak test on production-sized hardware (target: 10k events < 5 minutes).
- Desktop application UI work (pending separate backlog items) to expose the merge metrics and conflict summaries returned by the API.
- Operational runbook updates and compliance-team training to interpret `OfflineMergeHistory` records.
