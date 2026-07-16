# PlayHub — Architecture Decisions

## Database: PostgreSQL (Shared Database, Shared Schema)

**Recommendation:** PostgreSQL with a **shared database / shared schema** model, where every tenant-scoped table carries a `TenantId` (and branch-scoped tables also carry `BranchId`).

### Why not separate schema/database per tenant?

| Approach | Pros | Cons |
|---|---|---|
| **Shared schema + TenantId** ✅ | Single migration path, lowest ops cost, EF Core global query filters, easy cross-tenant SaaS analytics | Requires strict query-filter discipline |
| Separate schema per tenant | Stronger logical isolation | Migration × N tenants, connection pooling complexity, harder backups |
| Separate DB per tenant | Maximum isolation | Expensive, complex provisioning, doesn't scale for SaaS |

### Why PostgreSQL over SQL Server?

- **Zero per-core licensing cost** — important for a multi-tenant SaaS product.
- **JSONB columns** — ideal for audit `Details`, pricing snapshots, and future extensibility without schema churn.
- **Partial indexes** — e.g. `CREATE INDEX ... WHERE IsDeleted = false AND TenantId = ...` for hot paths.
- **First-class EF Core + Hangfire support** — both have mature PostgreSQL providers.
- **Azure-ready** — Azure Database for PostgreSQL if you deploy on Azure.

> If your team is exclusively Microsoft-stack with existing SQL Server Enterprise licenses, SQL Server is a viable alternative — the schema below is identical; only provider-specific types differ (`uuid` vs `uniqueidentifier`, `jsonb` vs `nvarchar(max)`).

---

## Frontend: React + TypeScript + Vite

**Recommendation:** React (not Blazor) for the web client.

| Criterion | React | Blazor |
|---|---|---|
| Real-time timer UI (SignalR) | Mature `@microsoft/signalr` JS client, fine-grained re-renders | Works, but heavier for sub-second timer ticks |
| Tailwind CSS (required) | Native, excellent DX | Possible but less common |
| i18n + RTL (Arabic) | `react-i18next` + `tailwindcss-rtl` — battle-tested | Limited RTL ecosystem |
| Mobile/tablet responsiveness | Huge component library (shadcn/ui, Radix) | Good but smaller ecosystem |
| Hiring & community | Larger pool | Smaller |

**Stack:** React 19 · TypeScript · Vite · Tailwind CSS · TanStack Query · Zustand · react-i18next · SignalR client

---

## File Storage: Azure Blob Storage

**Recommendation:** Azure Blob Storage for payment proof images.

- Native .NET SDK (`Azure.Storage.Blobs`) with SAS-token upload/download.
- Cheapest tier for small images (~KB each).
- Tenant-scoped container prefix: `{tenantId}/{branchId}/proofs/{invoiceId}/`.
- Alternative for non-Azure: **Cloudflare R2** (S3-compatible, no egress fees) — swap the storage abstraction interface.

---

## Backend Stack Summary

| Layer | Choice |
|---|---|
| Runtime | .NET 9 (current LTS track) |
| API | ASP.NET Core Minimal APIs or Controllers |
| ORM | EF Core 9 + Global Query Filters |
| Auth | JWT (access 15 min) + Refresh Token rotation |
| Real-time | SignalR (branch-scoped groups: `branch-{branchId}`) |
| Background jobs | Hangfire (PostgreSQL storage) |
| API docs | Swagger / OpenAPI (Swashbuckle) |
| Password hashing | ASP.NET Core Identity hasher (PBKDF2) or BCrypt.Net-Next |

---

## Multi-Tenancy Enforcement (Critical)

```
Every HTTP request → JWT → TenantId claim
Every branch action → BranchId from header/claim/selector
EF Core Global Query Filters on ALL tenant/branch tables
SaveChanges interceptor → auto-stamp TenantId, UserId, Timestamp on audit
```

**Never** accept `TenantId` from the client body — always from the authenticated token.

---

## Soft Delete Policy

All financial and session entities use `IsDeleted`, `DeletedAt`, `DeletedByUserId`. Hard `DELETE` is forbidden at the application layer for:

- Invoices, Payments, Sessions, Expenses, Revenue entries, Audit logs

---

## Indexing Strategy

Every tenant/branch table gets composite indexes:

```sql
-- Example pattern
CREATE INDEX ix_sessions_tenant_branch_status
  ON sessions (tenant_id, branch_id, status)
  WHERE is_deleted = false;

CREATE INDEX ix_audit_logs_tenant_branch_timestamp
  ON audit_logs (tenant_id, branch_id, timestamp DESC);
```

---

## Module Build Order (Post-Schema Approval)

1. Auth & Multi-tenancy (Tenant, User, Permissions, JWT)
2. Branches & Assets (Rooms, Devices, Controllers)
3. Pricing Plans & Sessions (core timer + SignalR)
4. Cafeteria & Inventory
5. Payments & Receivables
6. Accounting (Revenue auto + Expenses manual)
7. Audit Log
8. Reports & Export
9. Notifications (in-app, Master-only for stock alerts)
