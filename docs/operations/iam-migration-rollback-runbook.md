# IAM Migration Rollback Runbook

This runbook documents rollback procedures for Story 6.1.

## Phase 2 Rollback (Dual-Token Activation)
1. Update API Gateway auth config to disable Keycloak JWT validation and keep custom JWT enabled.
2. Verify /health is healthy and both legacy login and token validation succeed.
3. Notify stakeholders via the communications channel.

## Phase 3 Rollback (Keycloak Primary)
1. Update IdentityService config to set useKeycloakForNewLogins=false.
2. Confirm new logins flow to legacy login while existing sessions remain valid.
3. Monitor success rate >99.5% and error rates normal.
4. Notify stakeholders.

## Phase 4 Rollback (Session Migration)
1. Extend legacy JWT expiry in configuration by +7 days.
2. Suspend forced re-auth prompts.
3. Announce extension window to users.

## Database Restoration
- Validate last backup timestamp.
- Restore to a staging environment and validate.
- Follow change-control for production restore.

## Keycloak User Cleanup (if reverting to pre-Phase 1)
- Remove provisioned users created by migration batch (tagged with migration label).
- Re-run verification sampling to ensure cleanup.
