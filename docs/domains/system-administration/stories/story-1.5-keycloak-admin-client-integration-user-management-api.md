# Story 1.5 – Keycloak Admin Client Integration (User Management API)

## Summary
- Added a typed Keycloak admin client with Polly-backed retry, bearer token acquisition, and ProblemDetails error translation.
- Exposed user and role CRUD endpoints, password resets, and role assignment APIs through the Admin Service minimal API surface.
- Persisted audit events for all management operations and documented the new control plane capabilities for operators.

## Operational Notes
- Configure `Keycloak__ClientId` / `Keycloak__ClientSecret` with a service account that has realm admin permissions.
- API consumers must provide a correlation ID via `X-Correlation-ID` to improve audit traceability.
- Password reset endpoint requires a temporary password payload that complies with Keycloak password policies.

## Verification Checklist
- ✅ Admin Service `/api/admin/users` responds with paginated Keycloak data.
- ✅ Role mutations via Admin Service are visible immediately in the Keycloak admin console.
- ✅ Audit events recorded in `IntelliFin_AdminService.dbo.AuditEvents` for create/update/delete/reset/assign actions.
