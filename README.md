# Carbonate

Event coordination and management for **Carbon Events** and Carbon Logistics Management, Cape Town.

Task Force Carbon · INSY7315 Work Integrated Learning · The IIE

> **A owns the content of this file. D owns the "Running it locally" and "Hosting" sections.**
> Everything in angle brackets is a placeholder.

---

## Overview

<!-- A: two or three paragraphs. What Carbonate does, who uses it, which problem from the client
     interviews it solves. Keep the client's vocabulary. -->

## Live system

| Environment | URL |
|---|---|
| Production | <https://…> |
| Dev | <https://…> |

**Demo logins** (demo data only — remove before any real client data is loaded):

| Role | Email | Password |
|---|---|---|
| Director | … | … |
| Operations Manager | … | … |
| Event Manager | … | … |
| Accounts | … | … |
| Crew Lead | … | … |
| Casual Crew | … | … |

Director and Accounts have TOTP enrolled; the demo secret is in the team's shared notes, not here.

## Architecture

<!-- A: short summary, then link the Task 1 document. -->

- **API** — ASP.NET Core Web API (.NET 10), layered Controller → Service → Repository → `CemDbContext`,
  DTOs at the boundary. Background work runs as in-process hosted services.
- **SPA** — React and TypeScript on Vite, served from the API's `wwwroot` so it is same-origin.
- **Data** — Azure SQL Database (serverless), Blob Storage for files, Key Vault for secrets.
- **Region** — South Africa North.

---

## Running it locally

Target: a new developer gets this running in under 30 minutes (NFR-26).

### Prerequisites

- .NET 10 SDK
- Node.js 20+
- Docker Desktop
- The `dotnet-ef` tool: `dotnet tool install --global dotnet-ef`

### Steps

```bash
# 1. Dependencies: SQL Server and the Blob emulator
docker compose up -d

# 2. Local secrets (never committed)
cd api/src/Carbonate.Api
dotnet user-secrets init
dotnet user-secrets set "ConnectionStrings:Sql" "Server=localhost,1433;Database=Carbonate;User Id=sa;Password=Local_Dev_Only_1!;TrustServerCertificate=True"
dotnet user-secrets set "Storage:BlobServiceUri" "http://127.0.0.1:10000/devstoreaccount1"
dotnet user-secrets set "Jwt:SigningKey" "<any 32+ character string for local use>"

# 3. Database
dotnet ef database update --project ../Carbonate.Infrastructure --startup-project .

# 4. API (seeds roles, permissions, boards and templates on first run)
dotnet run

# 5. SPA, in a second terminal. Vite proxies /api to the API, so it is same-origin locally too.
cd ../../../web
npm install
npm run dev
```

The SPA is on <http://localhost:5173>, the API on <https://localhost:7xxx>, Swagger at `/swagger`
(development only).

### Running the tests

```bash
dotnet test api/Carbonate.sln          # unit and integration; integration needs Docker running
cd web && npm run test                 # component tests and axe checks
```

---

## Hosting

Everything runs in one resource group, `rg-carbonate`, in **South Africa North** (NFR-22).

| Service | SKU | Why |
|---|---|---|
| App Service plan | Basic B1, Linux | Cheapest tier with **Always On**, which the background workers require. Standard S1 would exhaust the student credit before the EXPO. |
| App Service × 2 | `carbonate-api-dev`, `carbonate-api-prod` | Apps are billed per plan, so the second environment costs nothing. |
| Azure SQL | General Purpose serverless, 0.5–1 vCore, auto-pause 1 h | Idle most of the time between events; auto-pause is the entire cost case. Entra-only authentication, so no password exists. |
| Blob Storage | Standard LRS, private `uploads` container | Incident photos, documents, card attachments. Served via short-lived SAS links, never public. |
| Key Vault | Standard, RBAC permission model | JWT signing key, Google refresh token, connection details. |
| Application Insights | Free tier | Request traces, failures, availability test on `/health`. |

The SPA is not separately hosted — it is built in CI and published into the API's `wwwroot`, so one
deployment unit serves both and the `SameSite=Strict` refresh cookie works without CORS.

Both App Services authenticate to SQL, Blob Storage and Key Vault with a **system-assigned managed
identity**. There are no credentials in any connection string.

**→ Full rationale, rejected alternatives, cost model and known limitations: [`docs/hosting.md`](docs/hosting.md)**

Decision records for every deviation from Task 1: [`docs/decisions.md`](docs/decisions.md)

**Release policy (NFR-11).** Production deploys Monday–Wednesday, 09:00–15:00 SAST, and never within
48 hours of a load-in milestone on real client data. During the Task 2 build on demo data this does not
bind, but it is the policy at handover.

---

## Contributing

- Branches: `feature/FR-xx-short-name`, `fix/…`, `test/…`, `chore/…`
- Commits name the requirement: `FR-28: consolidate order lines by supplier`
- Small PRs into `dev`, one reviewer, green CI
- Anything touching auth, permissions, a money field or a migration needs C's approval
- Anything touching `.github/` or infrastructure needs D's approval
- Full rules in `docs/PROJECT_PLAN.md` section 13 and `CLAUDE.md`

## Documents

- `docs/PROJECT_PLAN.md` — the build plan, and the source of truth for agents
- `docs/decisions.md` — every deviation from Task 1, with reasoning
- `docs/traceability.md` — requirement → endpoint → screen → test → status
- `docs/api/openapi.json` — the API contract
- Task 1 document: <link>
- Presentation slides: <link>
