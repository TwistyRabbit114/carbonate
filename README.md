# Carbonate

Event coordination and management for **Carbon Events** and Carbon Logistics Management, Cape Town.

Task Force Carbon · INSY7315 Work Integrated Learning · The IIE

> **B owns the content of this file. D owns the "Running it locally" and "Hosting" sections.**
> Everything in angle brackets is a placeholder.

---

## Overview

Carbonate follows an event from the confirmed booking through costing, the recce, load-in, the event
itself, strike, invoicing and reconciliation. It replaces the spreadsheets, chat groups and shared
calendar that Carbon Events and Carbon Logistics Management used to run 40 to 50 costed events a
year, with the busiest stretch in December. It has an events board, an Admin Tasks board, stock planning with
shortfall warnings and consolidated order lists, incident reports from a phone, and a one-way push
to the company's Google Calendar.

About 15 people use it, in six roles: Director, Operations Manager, Event Manager, Accounts, Crew
Lead and Casual Crew. The office roles work at a desk; crew use it on their phones, where it opens
on their next event, their call time and the venue's access details.

The rule the client cares about most is who sees money. Prices, costs and margins reach the
Director, the Event Manager and Accounts only. For everyone else those fields are left out of the
API response, not hidden on screen, and a masking test suite signs in as each role to prove it.
Carbonate arrives set up, with roles, boards and checklist templates seeded, and uses the client's
own words: pack size, recce, load-in, strike, costing.

Built for Task 2 by Christiaan ten Velden (front end, boards and venues), Ethan Algeo (back end,
data and security) and Ulrich Bezuidenhout (cloud, pipeline, lifecycle, stock and incidents).

## Live system

| Environment | URL | Use |
|---|---|---|
| Dev (the demo) | <https://carbonate-api-dev-bgahazhtddayg3c8.southafricanorth-01.azurewebsites.net> | Seeded with the demo data below. Use this one. |
| Production | <https://carbonate-api-prod-ewhfe4a8chdsgnbe.southafricanorth-01.azurewebsites.net> | Deployed from `main` behind a manual approval. Deliberately has **no demo data and no accounts**. |

**Demo logins** (dev only, fictional people, never in production). Every account uses the same password:
`Carbonate-Demo-2026!`

| Role | Email | Sign-in |
|---|---|---|
| Director | `director@carbon.demo` | Password, then a two-step code (see below) |
| Operations Manager | `ops@carbon.demo` | Password only |
| Event Manager | `sarah@carbon.demo` | Password only |
| Accounts | `accounts@carbon.demo` | Password, then a two-step code (see below) |
| Crew Lead | `thabo@carbon.demo` | Password only |
| Casual Crew | `priya@carbon.demo` | Password only |

**Two-step code for the Director and Accounts.** Both are already set up. In any authenticator app (Google
Authenticator, Microsoft Authenticator, Authy), add an account **manually** by entering this setup key, with
"time based" selected:

```
CARBONATEDEMOSECRETFORTOTPAPPS23
```

The app then shows the 6-digit code to type after the password. Demo secret only: it belongs to these two
fictional accounts on the dev environment.

What each role sees is the point of the demo. Sign in as the Event Manager or Accounts to see prices, costs and
margins; as the Operations Manager or crew to see the same event with those fields absent from the response
(not hidden on screen).

## Architecture

A layered monolith, as designed in the Task 1 document (section 6.2), deployed as one unit.

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
- Node.js 24+
- Docker Desktop

### Steps

```bash
# 1. Dependencies: SQL Server and the Blob emulator
docker compose up -d

# 2. Local secrets (never committed). The API will not sign anyone in without all of these.
cd api/src/Carbonate.Api
dotnet user-secrets init
dotnet user-secrets set "ConnectionStrings:Sql" "Server=localhost,1433;Database=Carbonate;User Id=sa;Password=Local_Dev_Only_1!;TrustServerCertificate=True"
dotnet user-secrets set "Storage:BlobServiceUri" "http://127.0.0.1:10000/devstoreaccount1"
dotnet user-secrets set "Jwt:Issuer" "carbonate-local"
dotnet user-secrets set "Jwt:Audience" "carbonate-local"
dotnet user-secrets set "Jwt:SigningKey" "<any random string of 32 or more characters>"
dotnet user-secrets set "Auth:MfaEncryptionKey" "<base64 of 32 random bytes, see below>"
dotnet user-secrets set "Seeding:Demo" "true"      # optional: loads the demo accounts and events above

# 3. Database (the EF tool is pinned in dotnet-tools.json, so no global install is needed)
cd ../../..
dotnet tool restore
dotnet dotnet-ef database update --project api/src/Carbonate.Infrastructure --startup-project api/src/Carbonate.Api

# 4. API (seeds roles, permissions, divisions, the admin board and templates on first run)
cd api/src/Carbonate.Api
dotnet run

# 5. SPA, in a second terminal. Vite proxies /api to the API, so it is same-origin locally too.
cd web
npm install
npm run dev
```

To make `Auth:MfaEncryptionKey`: `openssl rand -base64 32`, or in PowerShell
`[Convert]::ToBase64String((1..32 | % { Get-Random -Maximum 256 }) -as [byte[]])`.

The SPA is on <http://localhost:5173>, the API on <https://localhost:7108>, Swagger at `/swagger`
(development only). `/health` answers without touching the database.

### Running the tests

```bash
dotnet test api/Carbonate.sln          # unit and integration; integration needs Docker running
cd web && npm run test                 # component tests and axe checks
dotnet test api/tests/Carbonate.IntegrationTests --filter "Category=Masking"   #the masking suite on its own (NFR-17, NFR-28)
```

To work on the screens without the API or a database, `npm run dev:mocks` in `web/` runs the SPA
against a mocked API that keeps the real rules: roles, masking and the approval steps.

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
