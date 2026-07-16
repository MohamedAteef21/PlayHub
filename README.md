# PlayHub

Multi-tenant SaaS system for PlayStation gaming shop & cafeteria management.

## Stack

- **Backend:** .NET 10, ASP.NET Core, EF Core, PostgreSQL
- **Auth:** JWT + Refresh Tokens
- **Jobs:** Hangfire
- **Real-time:** SignalR (upcoming)
- **Frontend:** React + Vite + Tailwind (upcoming)

## Prerequisites

- [.NET 10 SDK](https://dotnet.microsoft.com/download)
- [PostgreSQL 15+](https://www.postgresql.org/download/)

## Quick Start

1. Update the connection string in `src/PlayHub.Api/appsettings.Development.json`
2. Create the database:
   ```bash
   createdb playhub_dev
   ```
3. Run the API:
   ```bash
   dotnet run --project src/PlayHub.Api
   ```
4. Open Swagger UI: `https://localhost:7xxx/swagger`

## API Endpoints

### Auth
| Method | Endpoint | Description |
|---|---|---|
| POST | `/api/auth/register` | Register tenant + master user + first branch |
| POST | `/api/auth/login` | Login |
| POST | `/api/auth/refresh` | Refresh access token |
| POST | `/api/auth/logout` | Revoke refresh token |
| POST | `/api/auth/select-branch` | Select active branch (returns new JWT) |
| GET | `/api/auth/me` | Current user profile |

### Branches
| Method | Endpoint | Description |
|---|---|---|
| GET | `/api/branches` | List all branches |
| GET | `/api/branches/{id}` | Get branch details |
| POST | `/api/branches` | Create branch (Master only) |
| PUT | `/api/branches/{id}` | Update branch (Master only) |

### Assets (requires active branch)
| Method | Endpoint | Permission | Description |
|---|---|---|---|
| GET | `/api/assets/dashboard` | Sessions.View | Room/device map with live status |
| GET/POST/PUT | `/api/assets/rooms` | View / Assets.Manage | Room CRUD |
| GET/POST/PUT | `/api/assets/devices` | View / Assets.Manage | Device CRUD |
| GET/POST/PUT | `/api/assets/controller-types` | Assets.Manage | Controller type CRUD |

### Pricing (requires active branch)
| Method | Endpoint | Permission | Description |
|---|---|---|---|
| GET | `/api/pricing/plans?mode=Gaming` | Sessions.View | List pricing plans |
| POST | `/api/pricing/plans` | Settings.Manage | Create plan with rate tiers |
| PUT | `/api/pricing/plans/{id}` | Settings.Manage | Update plan |

### Sessions (requires active branch)
| Method | Endpoint | Permission | Description |
|---|---|---|---|
| GET | `/api/sessions/active` | Sessions.View | Live open/paused sessions |
| POST | `/api/sessions/open` | Sessions.Create | Open gaming/watching session |
| POST | `/api/sessions/{id}/pause` | Sessions.Pause | Pause timer |
| POST | `/api/sessions/{id}/resume` | Sessions.Pause | Resume timer |
| POST | `/api/sessions/{id}/close` | Sessions.Close | Close + invoice + revenue |
| POST | `/api/sessions/{id}/cafeteria` | Cafeteria.Sell | Add item to open session |

### SignalR
- Hub: `/hubs/sessions?access_token={jwt}`
- Events: `SessionUpdated`, `SessionClosed`
- Methods: `JoinBranch(branchId)`, `LeaveBranch(branchId)`

### Cafeteria & Inventory (requires active branch)
| Method | Endpoint | Permission | Description |
|---|---|---|---|
| GET/POST/PUT | `/api/cafeteria/items` | View / Settings.Manage | Item catalog CRUD |
| GET/POST | `/api/cafeteria/sales` | View / Cafeteria.Sell | Standalone counter sales |
| POST | `/api/cafeteria/sales/{id}/returns` | Cafeteria.Return | Return with revenue reversal |
| GET | `/api/inventory/movements` | Inventory.View | Stock movement ledger |
| POST | `/api/inventory/items/{id}/adjust` | Inventory.Adjust | Manual stock adjustment |
| GET/POST | `/api/purchase-orders` | Inventory / PO.Create | Create & list POs |
| POST | `/api/purchase-orders/{id}/receive` | PO.Receive | Receive stock + auto expense |

### Accounting & Finance
| Method | Endpoint | Permission | Description |
|---|---|---|---|
| GET/POST | `/api/accounting/categories` | Expenses.View / Settings | Expense categories |
| GET/POST | `/api/accounting/expenses` | Expenses.View / Add | Manual expenses |
| GET | `/api/accounting/dashboard` | Reports.View | Revenue, expenses, net profit |
| GET | `/api/receivables` | Master only | Outstanding deferred payments |
| POST | `/api/receivables/{id}/collect` | Master only | Mark receivable collected |
| GET | `/api/notifications` | All users | In-app notifications |
| GET | `/api/audit` | Master only | Security activity log (paginated) |
| GET | `/api/reports/revenue` | Reports.View | Revenue breakdown |
| GET | `/api/reports/best-sellers` | Reports.View | Top cafeteria items |
| GET | `/api/reports/device-usage` | Reports.View | Device hours report |

## Project Structure

```
src/
  PlayHub.Api/           → HTTP layer, controllers, middleware
  PlayHub.Application/   → DTOs, interfaces, business contracts
  PlayHub.Domain/        → Entities, enums, domain rules
  PlayHub.Infrastructure/→ EF Core, auth services, external integrations
docs/
  01-architecture-decisions.md
  02-database-schema.md
```

## Migrations

```bash
dotnet ef migrations add MigrationName --project src/PlayHub.Infrastructure --startup-project src/PlayHub.Api
dotnet ef database update --project src/PlayHub.Infrastructure --startup-project src/PlayHub.Api
```

## Frontend (React)

```bash
cd web
npm install
npm run dev
```

Open `http://localhost:5173` — API requests proxy to `http://localhost:5052`.

### Frontend modules
- Auth (login, register) + branch selector
- Live session dashboard with SignalR + local timers
- EN/AR i18n with RTL support
- Placeholder pages for cafeteria, inventory, accounting, reports


- Inventory tracked **per branch**
- Time billing **rounds up** (configurable per tenant via `billing_round_up`)
- Multiple pricing plans — selected at session open
- Deferred payments: full collection only (v1)
- Gaming price by total controller count only (v1)
- Invoice numbering per branch with prefix
