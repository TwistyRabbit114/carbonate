<div align="center">

<img src="docs/assets/carbonate-logo.svg" alt="Carbonate logo" width="132" />

# Carbonate

**Event coordination for Carbon Events and Carbon Logistics Management, Cape Town.**

From the confirmed booking to the final invoice, in one place, with money seen only by the people who should see it.

[![CI](https://github.com/TwistyRabbit114/carbonate/actions/workflows/ci.yml/badge.svg?branch=dev)](https://github.com/TwistyRabbit114/carbonate/actions/workflows/ci.yml)
![.NET 10](https://img.shields.io/badge/.NET-10-512BD4)
![React](https://img.shields.io/badge/React-TypeScript-3178C6)
![Azure](https://img.shields.io/badge/Azure-South%20Africa%20North-0078D4)

[Demo video](#demo-video) · [Try it live](#live-system) · [What is built](#what-is-built) · [Security](#security-in-brief) · [Run it locally](#running-it-locally) · [Hosting](#hosting)

</div>

Task Force Carbon · INSY7315 Work Integrated Learning · Task 2 · The IIE

---

## Overview

Carbon Events and Carbon Logistics Management run 40 to 50 costed events a year, with the busiest stretch in
December, on spreadsheets, chat groups and a shared calendar. Carbonate replaces that. It follows an event from
the confirmed booking through costing, the recce, load-in, the event itself, strike, invoicing and reconciliation.

It has an events board, an Admin Tasks board, stock planning with shortfall warnings and consolidated order
lists, incident reports from a phone, and a one-way push to the company's Google Calendar. About 15 people use
it, in six roles: Director, Operations Manager, Event Manager, Accounts, Crew Lead and Casual Crew. Office roles
work at a desk; crew use it on their phones, where it opens on their next event, their call time and the venue's
access details. It uses the client's own words: pack size, recce, load-in, strike, costing.

**The rule the client cares about most is who sees money.** Prices, costs and margins reach the Director, the
Event Manager and Accounts only. For everyone else those fields are left out of the API response, not hidden on
screen, and a masking test suite signs in as each role to prove it.

Built for Task 2 by **Christiaan ten Velden** (front end, boards and venues), **Ethan Algeo** (back end, data and
security) and **Ulrich Bezuidenhout** (cloud, pipeline, lifecycle, stock and incidents).

## Demo video

Our walkthrough of Carbonate, role by role, as presented for Task 2. Click the picture to watch it on YouTube.

[![Carbonate demo video](https://img.youtube.com/vi/M2amO2uqkUs/hqdefault.jpg)](https://youtu.be/M2amO2uqkUs)

**Watch it here:** <https://youtu.be/M2amO2uqkUs>

## Live system

| Environment | URL | Use |
|---|---|---|
| **Dev (the demo)** | <https://carbonate-api-dev-bgahazhtddayg3c8.southafricanorth-01.azurewebsites.net> | Seeded with the demo data below. **Use this one.** |
| Production | <https://carbonate-api-prod-ewhfe4a8chdsgnbe.southafricanorth-01.azurewebsites.net> | Deployed from `main` behind a manual approval. Deliberately has **no demo data and no accounts**. |

> The database sleeps when idle to save cost, so the **first sign-in after a quiet spell can take up to a minute**.
> Later requests answer in a few seconds.

**Demo logins** (dev only, fictional people, never in production). Every account uses the same password:
`Carbonate-Demo-2026!`

| Role | Email | Sign-in |
|---|---|---|
| Director | `director@carbon.demo` | Password, then a two-step code (below) |
| Operations Manager | `ops@carbon.demo` | Password only |
| Event Manager | `sarah@carbon.demo` | Password only |
| Accounts | `accounts@carbon.demo` | Password, then a two-step code (below) |
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

**A good tour.** Sign in as the Event Manager (`sarah@`) and open the events board, then an event: you see its
prices, costs and margins, its costings and its crew. Sign in as the Operations Manager or crew and open the same
event: the money is absent from the response, not just hidden. Sign in as the Director for user administration
and the audit trail, and as Accounts for invoices.

## What is built

All the **Must** requirements are built, tested and deployed. The full requirement-by-requirement table, with the
endpoint, screen, tests and status for each, is in [`docs/traceability.md`](docs/traceability.md).

| Area | What works |
|---|---|
| **Events** | Booking record with every field, stages (Enquired to Finished) with automatic moves, the events board, milestones that cascade when one moves, crew assignment, cost comparison with earlier events, pack size, conflict detection on every edit |
| **Costing and invoices** | Costing lines with cost and price, margin bands, Director approval above a threshold, copy a previous costing, client confirmation by PO or deposit, invoices that carry the confirmation reference, list of finished events not yet invoiced |
| **Boards** | Admin Tasks board where only the assigning manager can complete or return a task, per-event task boards, crew see only their own cards, WIP limits |
| **Stock and orders** | Catalogue, requirements per event with shortfall and lead-time warnings, consolidated order list approved by a different person |
| **Incidents, venues** | Incident reports with an asset link and photo, persistent venue records, site visits |
| **People and security** | Sign-in with two-step codes for Director and Accounts, six roles with a permission matrix, user administration with safeguards, casual crew accounts that expire after the event, an insert-only audit trail |
| **Calendar** | Outbound push with retry, de-duplication and redaction is built and tested; the Google connection itself is not (see below) |
| **Front end** | One app for all six roles. Office roles get a sidebar; crew get a phone layout with a bottom tab bar that works at 360 px. Every card can be moved by drag and drop or from a "Move to…" menu with the keyboard. Every screen has loading, empty and error states, and an edit someone else got to first shows a message with a reload instead of overwriting. Money columns and rows only appear when the API sends them. axe accessibility checks run in the component tests |

**Not built, and why** (also listed in the traceability file): the native calendar view (a Should, front end only),
card attachments, the intra-day service schedule, event documents and post-event reconciliation (Shoulds), and the
Google connect flow, which needs a Google Cloud project we do not have. Row-Level Security was a stretch goal and
is not built; object-level access is enforced in the queries instead.

## Security in brief

- **Sign-in:** short-lived access token (15 minutes) plus a single-use refresh cookie that is HttpOnly,
  SameSite=Strict and scoped to `/api/auth`. Replaying a used cookie revokes the whole chain. Passwords are hashed
  with PBKDF2, checked against known breaches, and the account locks for 15 minutes after five wrong attempts.
- **Two-step codes** (TOTP) for the Director and Accounts, with the secret encrypted at rest.
- **Authorisation:** deny by default. Every endpoint declares the permission it needs, and a test fails if one
  does not. An event you cannot see answers 404, never 403.
- **Financial masking:** done on the server, on every response, and again as a safety net. Staff hourly rates show
  only on your own row unless you are the Director or Accounts. The
  [masking acceptance suite](api/tests/Carbonate.IntegrationTests/Masking/MaskingTests.cs) signs in as each of the six
  roles, calls every endpoint that carries money, and fails if a money field reaches a role that shouldn't see it,
  even as an empty value. It also fails if someone adds a money field without adding it to the suite's list. It runs
  as its own required check in CI, so nothing reaches production if it fails.
- **Other:** rate limiting, security headers and a strict content security policy, RFC 7807 problem details,
  input validation, and an audit trail the database refuses to alter.

Measured results for each non-functional requirement are in [`docs/nfr-results.md`](docs/nfr-results.md).

## Architecture

A layered monolith, as designed in the Task 1 document (section 6.2), deployed as one unit.

```
 Browser  ──►  React + TypeScript SPA  (served from the API's wwwroot, same origin)
                     │
                     ▼
        ASP.NET Core API (.NET 10)   Controller → Service → Repository → EF Core
                     │                 Domain rules are pure and unit tested
                     ▼
     Azure SQL (serverless) · Blob Storage · Key Vault        South Africa North
```

- **API:** DTOs at the boundary, background work as in-process hosted services (stage moves, crew expiry,
  calendar outbox).
- **SPA:** React and TypeScript on Vite, so the refresh cookie works without CORS.
- **Data:** Azure SQL Database (serverless), Blob Storage for files, Key Vault for secrets.

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
# Azurite is published on 10010-10012, not its usual 10000-10002: Windows reserves parts of
# 9914-10213 for Hyper-V and the default ports often cannot be bound. The key below is Azurite's
# well-known emulator key, identical on every machine — it is not a secret and is never used in Azure.

# 2. Local secrets (never committed). The API will not sign anyone in without all of these.
cd api/src/Carbonate.Api
dotnet user-secrets init
dotnet user-secrets set "ConnectionStrings:Sql" "Server=localhost,1433;Database=Carbonate;User Id=sa;Password=Local_Dev_Only_1!;TrustServerCertificate=True"
dotnet user-secrets set "Storage:ConnectionString" "DefaultEndpointsProtocol=http;AccountName=devstoreaccount1;AccountKey=Eby8vdM02xNOcqFlqUwJPLlmEtlCDXJ1OUzFT50uSRZ6IFsuFq2UVErCz4I6tq/K1SZFPTOtr/KBHBeksoGMGw==;BlobEndpoint=http://127.0.0.1:10010/devstoreaccount1;"
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

## Documents

- [`docs/traceability.md`](docs/traceability.md): requirement to endpoint to screen to test to status
- [`docs/nfr-results.md`](docs/nfr-results.md): measured results for the non-functional requirements
- [`docs/decisions.md`](docs/decisions.md): every deviation from Task 1, with reasoning
- [`docs/hosting.md`](docs/hosting.md): hosting rationale, cost model and known limitations
- [`docs/api/openapi.json`](docs/api/openapi.json): the API contract
- [`docs/attendance.md`](docs/attendance.md): who was at each team meeting, and what we covered
- [`api/tests/Carbonate.IntegrationTests/Masking`](api/tests/Carbonate.IntegrationTests/Masking): the masking acceptance suite
- Demo video: <https://youtu.be/M2amO2uqkUs>
