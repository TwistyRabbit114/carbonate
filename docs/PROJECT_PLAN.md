# Carbonate — Task 2 Project Plan

**Context for the team's AI coding agents and the developers using them.**

| | |
|---|---|
| Product | Carbonate: event coordination and management system |
| Client | Carbon Events and its sister division Carbon Logistics Management (Cape Town) |
| Team | Task Force Carbon, INSY7315 Work Integrated Learning, The IIE |
| Phase | Task 2: build, host and present the working system (300 marks, due Week 9, date to confirm) |
| Plan owner | Christiaan ten Velden (role B, acting team lead) |
| Sources | Task 1 final document (submitted 17 Aug 2026), Task 2 role split (Oct 2026), module manual (MM §9.4–9.6), project plan checklist |
| Last updated | 3 Oct 2026: team change. Jack Evans (role A) is in hospital and out for Task 2; role A's work is split between B, C and D (§2). |

---

## 0. How to use this file

**For people.** Save this file as `docs/PROJECT_PLAN.md` in the repo, then point each agent at it:

- Claude Code: add the line `@docs/PROJECT_PLAN.md` to `CLAUDE.md`.
- Codex, Cursor and Copilot: reference it from `AGENTS.md` or `.github/copilot-instructions.md`.

When you start a session, tell the agent which module and FR IDs it is working on.

The whole file is about 20k tokens. If an agent's context is tight, give it §0, §5, §7, §14 and the §8 section for its module.

**For agents.** Read sections 0, 2, 5, 7 and 14 first, then the section for your module in §8. Sections and requirements are referenced like this:

- `T1 §7.6` means section 7.6 of the Task 1 document.
- `FR-28`, `US-07` and `NFR-17` are requirement IDs from Task 1 (catalogued in Appendix A and B).

**Order of authority** when sources disagree:

1. This plan.
2. Task 1 §7.6 (RBAC) and §2.3 (FRs).
3. Other parts of Task 1.
4. The prototype.

If the plan itself is silent or contradictory, **do not invent behaviour**. Leave a `// TODO(plan): …` comment, raise it in the PR description, and add it to §15.

**Markers used below**

- `[+]` = added by this plan, not in Task 1. The Task 3 report must be updated to match.
- `[Δ]` = deviates from Task 1. The reason is stated beside it.
- `$price`, `$cost`, `$margin`, `$staff` = financial field tiers (§7.3). These fields must never reach a role that lacks the permission.

---

## 1. Project snapshot

- **What it does.** Carbonate tracks an event from a confirmed booking through costing, site visit (recce), load-in, the event itself, strike, invoicing and post-event reconciliation. It has:
  - two kanban boards (events and admin tasks);
  - stock planning with a generated order list and shortfall warnings;
  - incident logging;
  - outbound-only sync to the company's Google Calendar.
- **Scale.** About 15 named users and 40–50 costed events a year, peaking in December. Records are kept indefinitely. Treat this as a small internal system and do not over-engineer it (NFR-27, NFR-30).
- **Six roles.** Director, Operations Manager, Event Manager, Accounts, Crew Lead and Casual Crew (T1 §2.2). A user account may hold more than one role; permissions are the union of its roles.
- **The requirement the client cares about most.** Financial figures are seen by three roles only: Director, Event Manager and Accounts. For every other role, cost fields are **absent from the API response**, not hidden in the UI (FR-35, NFR-17).
- **Why the previous tools failed.** The client abandoned Asana and Notion because of setup effort and poor buy-in. Carbonate must therefore:
  - arrive pre-configured (NFR-02);
  - be usable without training (NFR-01);
  - speak the client's vocabulary (NFR-05, Appendix C).
- **Task 2 marking (MM Annexure C):**

  | Area | Marks |
  |---|---|
  | Front End | 35 |
  | Back End | 105 |
  | Hosting | 35 |
  | GitHub/CI-CD | 35 |
  | Presentation | 60 |
  | Attendance | 30 |

  Submission is the GitHub repo link only. Setup instructions and slides go in the README.

---

## 2. Team and ownership

**The team is three people for Task 2.** Jack Evans (role A) is in hospital and can't contribute. His work has been split by fit between the other three (table below). In Task 1, Christiaan held role A: requirements, interviews and documentation. The Task 1 prototype is hosted on Ethan's GitHub Pages.

| Role | Member | Owns in Task 2 |
|---|---|---|
| A: Lead / BA / Docs | Jack Evans: **in hospital, out for Task 2** | Reassigned; see the next table |
| B: UI/UX & Front-End, **acting team lead** | Christiaan ten Velden | The whole SPA; boards and admin task API; **venues and site visits API**; **masking acceptance suite**; **traceability**; **README content**; **presentation storyline and requirements alignment**; **attendance log**; **UAT and client questions** |
| C: Back-End & Data/Security | Ethan Algeo | Schema and migrations; auth, RBAC and masking; events and milestones; commercial cycle; users; audit; review of every cost-bearing or permission change |
| D: DevOps / Cloud / Architecture | Ulrich Bezuidenhout | Repo and CI/CD; Azure hosting; event lifecycle (State and Observer patterns, transition worker); incidents and file storage; Google Calendar sync; **stock, templates and order lists**; **seed and demo data** |

### Role A's work: who picked it up

| A's item | Now owned by | Why |
|---|---|---|
| Venues and site visits API (FR-32, 33) | **B** | Small CRUD; B builds those screens anyway |
| Stock catalogue, templates, requirements, order lists API (FR-24–30) | **D** | D's infrastructure work is front-loaded in Phase 1, and incidents and equipment already link to stock |
| Seed data, first-run seeding content, demo data (Appendix D) | **D** | Goes with the stock catalogue and templates |
| Masking acceptance suite (NFR-28 gate) | **B** (C reviews; D wires it as a required check) | B renders the absent fields, and it stays independent of C, who builds the masking |
| `docs/traceability.md` | **B** | Goes with requirements alignment |
| README content | **B** (D writes the setup section) | |
| Presentation storyline and the requirements-alignment segment (5 marks) | **B** | B already leads the demo and designs the slides |
| Attendance and progress log, weekly coordination, Task 1 feedback tracker | **B** | Acting team lead |
| UAT with Carbon staff; client questions (approval threshold, margin bands, default quantities per 100 guests) | **B** | Christiaan works at Carbon Logistics Management and ran the Task 1 interviews |
| NFR verification runs | **B** (UI: NFR-01, 03, 04, 06, 07, 23–25) · **D** (NFR-08, 10, 13/14, 26) · **C** (NFR-09, 15–21) | Each owner verifies what they built |
| Task 3: final report compilation and user guide | **To decide after Task 2** (§15) | |

### Module ownership

"API" means endpoints, services and rules. B builds every screen.

| Module | Must FRs | Should / Could FRs | API owner |
|---|---|---|---|
| Platform: auth, users, roles, masking, audit | FR-34, 35, 36, 38 | FR-37 | C |
| Events, milestones, crew assignment | FR-01, 03, 04, 07, 09 | FR-05, 06, 08, 10 | C |
| Commercial: costings, confirmation, invoices | FR-11, 12, 13, 14, 15 | FR-16, 17 | C |
| Boards and tasks (event task boards and admin board) | FR-19, 21 | FR-20, 22, 23 | B |
| Events board (FR-18) | FR-18 | — | UI: B · data: C's event list · moves: D's transitions |
| Venues and site visits | FR-32, 33 | — | B |
| Stock catalogue, templates, requirements, order lists | FR-24, 25, 26, 27, 28 | FR-29, 30 | D |
| Event lifecycle and automatic transitions | FR-02 | — | D |
| Incidents, equipment-linked reports, shared file storage | FR-31 | — | D |
| Google Calendar sync | FR-40, 41, 42 | FR-39 (native calendar view: B, UI and API) | D |

**Fallback.** There is no spare member any more, so cut scope before moving work. If C is behind at Gate 2 (§12):

1. C's Shoulds (FR-10, FR-37) wait.
2. Invoices (FR-13) move to D, whose Phase 3 is lightest once hosting is stable.
3. C keeps quotes, confirmation and the masking review.

If D is behind on stock at mid-Phase 2, B takes order-list generation (FR-28). The template seeder (FR-25) stays with D, because it's on the critical path.

**Review routing.** A PR that touches any of the following needs C's approval:

- authentication or authorisation code;
- a permission code;
- a `$` field;
- a migration.

A PR that touches `.github/` or infrastructure needs D's approval.

---

## 3. Scope for Task 2

- **Target:** all **29 Must** FRs working on the hosted system by Gate 2. A Must requirement missing at Task 2 is marked down again in Task 3.
- **Shoulds in scope** (trimmed for a three-person team; all cheap and mark-bearing):
  - FR-10 (concurrency warning);
  - FR-20 (return for rework);
  - FR-30 (second-person approval);
  - FR-37 (audit view).
- **Only if time allows, in this order:**
  1. FR-29 (lead-time warning).
  2. FR-39 (native calendar).
  3. FR-08, FR-05, FR-06, FR-22, FR-16, FR-17.
  4. FR-23 (Could).

  Anything not built carries into Task 3, where completeness is marked again.

**Out of scope** (agreed with the client, T1 Table 6). Do not build any of the following:

- Xero or accounting integration.
- Live point-of-sale stock feeds.
- Migration of historical spreadsheets.
- Placing orders with or paying suppliers.
- WhatsApp integration.
- Payment processing.
- A sales pipeline for enquiries.
- **Inbound** calendar sync.
- Multi-tenancy.
- A web application firewall or private database endpoint.

---

## 4. Architecture and stack

**Shape.** A layered monolith (T1 §6.2), deployed as two units:

- **API:** ASP.NET Core Web API in C#, layered Controller → Service → Repository → `CemDbContext`, with DTOs at the boundary. Background work runs as **in-process hosted services**, not as separate functions.
- **SPA:** React single-page app.
- **Data:** Azure SQL Database for relational data. Blob Storage for files. Key Vault for secrets, including the Google refresh token.
- **Region:** every Azure resource in **South Africa North** (NFR-22).

### Versions and libraries

C and B may substitute a library, but must record the change in the PR.

| Layer | Choice |
|---|---|
| Runtime | `[Δ]` **.NET 10 (LTS) and EF Core 10.** Task 1 says .NET 8, but [.NET 8 reaches end of support on 10 Nov 2026](https://devblogs.microsoft.com/dotnet/dotnet-8-9-end-of-support/), weeks before handover. Microsoft recommends .NET 10, supported to Nov 2028. If the team stays on .NET 8, change this line. |
| API libraries | `Microsoft.AspNetCore.Authentication.JwtBearer`; `PasswordHasher<AppUser>` (PBKDF2, from `Microsoft.Extensions.Identity.Core`); `Otp.NET` (TOTP); `FluentValidation`; `NJsonSchema` (T1 §5.3); `HtmlSanitizer` (Ganss.Xss); built-in rate limiting and `AddProblemDetails`; `Azure.Identity`, `Azure.Storage.Blobs`, `Azure.Security.KeyVault.Secrets`; `Google.Apis.Calendar.v3`; Application Insights |
| API tests | xUnit, NSubstitute, `Microsoft.AspNetCore.Mvc.Testing`, `Testcontainers.MsSql`. Do not use FluentAssertions v8+, which has a commercial licence; use xUnit asserts or Shouldly. |
| SPA | React and TypeScript (strict) on Vite; React Router; TanStack Query (server state and optimistic updates); dnd-kit (drag and drop with a keyboard sensor); react-hook-form with zod; DOMPurify (the single sanitised-description renderer); `openapi-typescript` (types generated from the API contract); self-hosted Manrope and Inter via `@fontsource` |
| SPA tests | Vitest, Testing Library, MSW (mock the API before endpoints exist), axe-core |
| Local dev | `docker compose`: SQL Server 2022 container and Azurite (Blob emulator). On Apple Silicon, run SQL Server under Rosetta. |

### Hosting topology

**`[+]` Decision for D: the SPA and API must be served same-site.** The reasons:

- Refresh tokens live in `HttpOnly; Secure; SameSite=Strict` cookies (T1 §7.3).
- Those cookies are not sent on cross-site requests.
- Safari blocks third-party cookies, and NFR-25 requires Safari.

The options, in order of preference:

1. **Static Web App on the Standard plan, with the App Service linked as its backend.**
   - SWA proxies `/api/*` to the App Service, so the SPA calls a relative `/api` and there is no CORS in production.
   - Linked backends [require the SWA Standard plan](https://learn.microsoft.com/azure/static-web-apps/apis-app-service).
   - The App Service then only accepts requests proxied through the SWA.
   - Linked backends are not available in SWA pull-request preview environments.
2. **Custom domains on one registrable domain**, for example `app.<domain>` and `api.<domain>`. These are same-site, so a CORS allowlist is still needed.
3. **Fallback:** serve the built SPA from the API's `wwwroot` with a fallback to `index.html`. `[Δ]` This drops the separate Static Web App and must be recorded in the Task 3 report.

In local development, the Vite dev server proxies `/api` to the API, so it is same-origin there too.

**Environments** (T1 §4.2). One App Service plan, Standard tier or above, with slots:

| Environment | Deploys from | Database |
|---|---|---|
| `dev` | merges to `dev` | dev database |
| `staging` | merges to `main` | staging database |
| `production` | slot swap from staging after a smoke test and manual approval | production database |

- Mark connection strings and feature flags as **slot settings**, so they stay with their slot when slots swap.
- Hosted workers run in every slot. Workers with external side effects therefore read a feature flag, for example `Features:CalendarSync:Enabled=false` outside production.
- Enable "Always On" so the hosted services keep running.

**Serverless SQL cost trap.** Any query every few minutes stops a serverless database from auto-pausing, which defeats the cost case in T1 §5. Workers should compute the next due time and sleep until it, not poll on a fixed short interval (§8.3).

---

## 5. Repository layout and conventions

The team works in one public monorepo and submits that repo's link on ARC. If the repo is private, the lecturer must be added as a collaborator.

```
carbonate/
├─ api/
│  ├─ Carbonate.sln
│  ├─ src/
│  │  ├─ Carbonate.Domain/          # entities, enums, state machines, ScheduleCalculator — no EF/ASP.NET refs
│  │  ├─ Carbonate.Application/     # services, DTOs, validators, interfaces (IEventService, IEventRepository…), masking
│  │  ├─ Carbonate.Infrastructure/  # CemDbContext, entity configurations, migrations, repositories,
│  │  │                             #   blob storage, Key Vault, Google Calendar client, seeding
│  │  └─ Carbonate.Api/             # Program.cs, controllers, auth handlers, FinancialMaskingFilter,
│  │                                #   middleware (headers, problem details), hosted services
│  └─ tests/
│     ├─ Carbonate.UnitTests/
│     └─ Carbonate.IntegrationTests/   # WebApplicationFactory + Testcontainers; masking acceptance suite
├─ web/                              # React + TS + Vite SPA
│  └─ src/{app,api,features/<module>,components,styles,test}
├─ docs/
│  ├─ PROJECT_PLAN.md                # this file
│  ├─ api/openapi.json               # committed contract, regenerated by C on API change
│  ├─ traceability.md                # FR → endpoint → screen → test → status (B)
│  └─ decisions.md                   # short log of any [Δ] decision
├─ .github/workflows/                # ci.yml, cd-dev.yml, cd-prod.yml (D)
├─ .github/dependabot.yml
├─ docker-compose.yml
└─ README.md
```

**Module folders.** Inside each project, code is organised by module, matching §8:

- `Features/Events`, `Features/Lifecycle`, `Features/Commercial`, `Features/Boards`, `Features/Venues`, `Features/Stock`, `Features/Incidents`, `Features/Calendar`;
- `Platform/Auth`, `Platform/Users`, `Platform/Audit`, `Platform/Files`.

Work inside your own module's folders. Shared code lives under `Platform/` or `Common/`, and changes to it need the owner's review.

### API conventions

- **Routes:**
  - Base `/api/…`, plural nouns, kebab-case.
  - Sub-resources are nested where ownership is clear, for example `/api/events/{id}/milestones`.
  - Actions that are state transitions use `POST /api/<resource>/{id}/<verb>`, for example `/approve` or `/return`.
- **JSON:**
  - camelCase.
  - Enums as strings.
  - Financial fields are **omitted, not null**, when masked (§7.3).
- **Times:**
  - Stored in UTC (`datetime2`).
  - Sent as ISO 8601 with `Z`.
  - Calendar dates are `DateOnly` (`yyyy-MM-dd`).
  - The SPA renders everything in `Africa/Johannesburg` (SAST, UTC+2, no daylight saving) as day-month-year (NFR-32).
- **Money:**
  - `decimal(18,2)`, ZAR.
  - VAT rate in config `Finance:VatRate` (currently `0.15`), never hard-coded. Task 1 Appendix B5 found the old workbook's bare VAT multiplier was a defect.
- **IDs:**
  - GUIDs generated in application code (T1 §5.2.1).
  - Exception: `AUDIT_ENTRY.AuditId` is a `bigint` identity.
  - Human-facing codes such as `EventCode`, `InvoiceNumber` and `Sku` are unique.
- **Errors:** RFC 7807 problem details. Use these status codes:

  | Status | When |
  |---|---|
  | `400` | Validation error, with field errors |
  | `401` | Not authenticated |
  | `403` | Lacks permission |
  | `404` | Not found, **or not visible to this user**. Do not reveal that a resource exists. |
  | `409` | Concurrency conflict or invalid state transition |
  | `422` | Business-rule violation |

  Never include stack traces outside development.
- **Concurrency:**
  - Updatable aggregates (`Event`, `TaskCard`, `Quote`, `OrderList`) carry `rowVersion`, as base64, in their DTOs.
  - Updates must send it back.
  - A mismatch returns `409` with `type: "/problems/concurrency-conflict"` (FR-10, NFR-15).
- **Lists:**
  - Query parameters `page`, `pageSize` (default 50, max 200), `sort`, and filters.
  - Response shape `{ items, page, pageSize, total }`.
- **Validation:**
  - FluentValidation on every request DTO, using allowlists.
  - Example: `EventCode` is 4–20 characters of `[A-Za-z0-9-]` (T1 §7.4).
  - JSON columns are validated against their schemas before writing.
- **Contract-first:**
  - C stubs every Phase 2 endpoint in Swagger by Gate 1 and commits `docs/api/openapi.json`.
  - B generates TypeScript types from that file.
  - Changing a published DTO shape needs the consumer's review.
- **Swagger:** development only (T1 §7.3, A05).

### Configuration

Use `IOptions<T>`. Secrets come from Key Vault in Azure and from user-secrets locally, never from source.

| Key | Default | Notes |
|---|---|---|
| `ConnectionStrings:Sql` | — | Managed identity in Azure |
| `Storage:BlobServiceUri`, `Storage:Container` | — | Azurite locally |
| `KeyVault:Uri` | — | |
| `Jwt:Issuer`, `Jwt:Audience`, `Jwt:SigningKey` | — | The signing key lives in Key Vault |
| `Auth:AccessTokenMinutes` | 15 | NFR-19 |
| `Auth:SessionHours` | 8 | NFR-19 |
| `Auth:LockoutThreshold` | 5 | NFR-20 |
| `Auth:LockoutMinutes` | 15 | NFR-20 |
| `Finance:VatRate` | 0.15 | |
| `Quotes:ApprovalThresholdZar` | client to confirm | FR-14 |
| `Quotes:MarginBands` | client to confirm | FR-11: list of `{ minPax, maxPax, targetMinPct, targetMaxPct }` |
| `Crew:DeactivateAfterDays` | 7 | FR-36, NFR-21 |
| `Features:CalendarSync:Enabled` | false except in production | |
| `Google:ClientId`, `Google:ClientSecret` | — | Key Vault |
| `Cors:AllowedOrigins` | — | Only if hosting option 2 is used |

### Git conventions

- **Branches:**
  - `main` is production-ready.
  - `dev` is the integration branch.
  - Work branches are `feature/FR-28-order-list`, `fix/…`, `test/…`, `chore/…`.
- **Commits** reference the FR or US they serve, as promised in T1 §8. Example: `FR-28: consolidate order lines by supplier`.
- **Authorship.** Commit from **your own GitHub account**. The commit history is read as evidence of individual contribution (Task 2 attendance, Task 3 overall contribution).
- **Pull requests:**
  - Small PRs into `dev`, with one reviewer and green CI.
  - `main` is updated only by PR from `dev`.
- **Migrations:**
  - Only one schema-changing PR merges at a time.
  - Rebase onto `dev`, then regenerate the migration, so the snapshot never conflicts.
  - Never edit a merged migration.
  - Name migrations `YYYYMMDD_<Module>_<Change>`.

---

## 6. Data model

This is the ERD from T1 §5.2: Figures 19–23, plus the uncaptioned reconciliation diagram on page 82. **C creates Schema v1 in Phase 1 for all modules**, including the `[+]` additions, so that B and D build against tables that already exist.

Rules that apply throughout:

- Primary keys are GUIDs; `AUDIT_ENTRY` uses `bigint`.
- Clustered indexes go on `CreatedAt` or `OccurredAt`, not on the GUIDs.
- Records are deactivated with `IsActive`, not deleted.
- Parent-to-child chains cascade on delete; references to `APP_USER` and `CLIENT` restrict deletion.
- Dynamic Data Masking is applied to the `$cost` columns (§7.3).

```
── Event core (C) ─────────────────────────────────────────────────────────────
CLIENT            ClientId · Name · TradingName · VatNumber · PaymentTermsDays · ConfidentialityRequired · IsActive
CLIENT_CONTACT    ContactId · ClientId→ · FullName · Position · Email* · Phone* · IsPrimary      (*masked w/o client.view_contacts)
DIVISION          DivisionId · Code UK · Name                                 (seed: CE = Carbon Events, CLM = Carbon Logistics Management)
VENUE (B)         VenueId · Name · Address · AccessRoute · LoadingBayDetails · OperatingHoursStart · OperatingHoursEnd
                  RequiresSecurityClearance · RequiresHealthSafetyFile · PpeRequirements · [+] IsActive
EVENT             EventId · EventCode UK · ClientId→ · VenueId→ · DivisionId→ · CreatedByUserId→ · Name · EventType · Status
                  EventDate(date) · HeadcountExpected · HeadcountConfirmed · PackSizeEstimated · PackSizeActual
                  BudgetAmount $price · PaymentMode · InfrastructureMode · StaffRequired · ServiceScheduleJson · IsConfidential
                  CreatedAt · RowVersion · [+] StartsAt · [+] EndsAt        (live window; drives FR-02 automatic transitions)
SITE_VISIT (B)    SiteVisitId · EventId→ · ConductedByUserId→ · VisitDate · VehicleType · SignInProcedure · SecurityCheckpoint
                  RequiredDriverDetails · Notes · [+] LicencePlate · [+] DriverName · [+] CrewNames · [+] HealthSafetyFileRef  (FR-33)
EVENT_MILESTONE   MilestoneId · EventId→ · MilestoneType · ScheduledStart · ScheduledEnd · ActualStart · ActualEnd · Status
MILESTONE_DEPENDENCY  DependencyId · PredecessorMilestoneId→ · SuccessorMilestoneId→ · DependencyType('FS') · LagHours
CREW_ASSIGNMENT   AssignmentId · EventId→ · UserId→ · CrewRole · ShiftStart · ShiftEnd · Confirmed · [+] HourlyRate $staff

── Commercial (C) ─────────────────────────────────────────────────────────────
QUOTE             QuoteId · EventId→ · CopiedFromQuoteId→ · Version · Status · SubtotalExVat $price · VatAmount $price
                  TotalIncVat $price · ValidUntil · IssuedAt · AcceptedAt · [+] ApprovedByUserId→ · [+] ApprovedAt · [+] RowVersion
QUOTE_LINE        QuoteLineId · QuoteId→ · Description · Quantity · UnitCostToUs $cost · UnitPriceToClient $price · LineTotal $price
                  [+] Category   (SetUpAndStrike|Infrastructure|Transportation|Crew|Ice|Stock|BarKit|Glassware|Other — T1 App. B5)
EVENT_CONFIRMATION ConfirmationId · EventId→ · DocumentId→ · ConfirmationType(PurchaseOrder|Deposit) · ClientPoNumber · PoReceivedDate
                  PoAmount $price · DepositAmount $price · DepositPaidDate · DepositReference · ConfirmedAt
INVOICE           InvoiceId · EventId→ · ConfirmationId→ · InvoiceNumber UK · IssuedDate · DueDate · AmountIncVat $price · Status · PaidDate
RECONCILIATION    ReconciliationId · EventId→ · CompletedByUserId→ · Status · ReconciledOn · RevenueFromClient $price
                  DirectCostToUs $cost · ProfitRetained $margin · MarginPercent $margin · StockVarianceValue $cost · Notes · CompletedAt
RECONCILIATION_LINE LineId · ReconciliationId→ · StockItemId→ · QuantityIssued · QuantityReturned · QuantityConsumed · QuantityWasted
                  UnitCostToUs $cost · UnitPriceToClient $price · LineRevenue $price · LineProfit $margin

── Boards (B) ─────────────────────────────────────────────────────────────────
CHECKLIST_TEMPLATE (D) TemplateId · DivisionId→ · Name · BoardType · DefinitionJson · IsActive
BOARD             BoardId · EventId→(nullable) · TemplateId→ · BoardType('Event'|'Admin') · Name · CreatedAt
                  CHECK: (BoardType='Event' AND EventId IS NOT NULL) OR (BoardType='Admin' AND EventId IS NULL)
BOARD_COLUMN      ColumnId · BoardId→ · Name · Position · WipLimit · IsDoneColumn
TASK_CARD         CardId · ColumnId→ · MilestoneId→ · Subject · Description(sanitised HTML) · Priority · DueAt · Position · Status
                  ReviewNotes · ReturnedByUserId→ · ReturnedAt · CompletedAt · [+] CreatedByUserId→ (the assigning manager, FR-20) · [+] RowVersion
TASK_ASSIGNMENT   AssignmentId · CardId→ · UserId→ · AssignedAt
CARD_ATTACHMENT   AttachmentId · CardId→ · UploadedByUserId→ · FileName · BlobUri · Sha256Hash · UploadedAt

── Stock, equipment & incidents (D) ───────────────────────────────────────────
STOCK_CATEGORY    CategoryId · ParentCategoryId→ · Name · [+] DivisionId→   (FR-24 "by division")
STOCK_ITEM        StockItemId · CategoryId→ · DefaultSupplierId→ · Sku UK · Name · Unit · IsConsumable · IsAsset · ReorderLevel
                  ConsumptionPerHundredGuests · StandardUnitCost $cost · [+] IsActive
SUPPLIER          SupplierId · Name · ContactName · Email · Phone · LeadTimeDays · IsLiquorSupplier · IsActive
STOCK_LOCATION    LocationId · Name · Address
INVENTORY_LEVEL   InventoryLevelId · StockItemId→ · LocationId→ · QuantityOnHand · QuantityReserved · LastCountedAt
EVENT_STOCK_REQUIREMENT RequirementId · EventId→ · StockItemId→ · QuantityRequired · QuantityAllocated · RequiredByDate
                  SourceMode(Stock|Order|Rent) · Notes
ORDER_LIST        OrderListId · EventId→([Δ] nullable — consolidated lists span events, FR-28) · SupplierId→ · GeneratedByUserId→
                  Status(Draft|PendingApproval|Approved|Placed) · RequiredByDate · GeneratedAt
                  [+] PeriodStart · [+] PeriodEnd · [+] ApprovedByUserId→ · [+] ApprovedAt · [+] PlacedAt · [+] RowVersion
ORDER_LIST_LINE   LineId · OrderListId→ · StockItemId→ · QuantityOrdered · EstimatedUnitCost $cost · Notes
EQUIPMENT_ASSET   AssetId · StockItemId→ · SerialNumber UK · Condition · Status · PurchaseDate
INCIDENT_REPORT (D) IncidentId · EventId→ · AssetId→ · StockItemId→ · ReportedByUserId→ · IncidentType(Breakage|EquipmentFailure|StockShortfall)
                  Quantity · ReportedAt · Description · ResolutionNotes · ReplacementCost $cost · [+] PhotoBlobUri · [+] PhotoSha256
                  CHECK: AssetId IS NOT NULL OR StockItemId IS NOT NULL

── People & access (C) ────────────────────────────────────────────────────────
APP_USER          UserId · EmployeeNumber UK · Email UK · FullName · PasswordHash · SecurityStamp · MfaEnabled · MfaSecretEncrypted
                  EmploymentType(Permanent|Casual) · IsActive · LastLoginAt · AccessFailedCount · LockoutEnd
APP_ROLE          RoleId · Name UK · Description           (Director, OperationsManager, EventManager, Accounts, CrewLead, CasualCrew)
PERMISSION        PermissionId · Code UK · Description
USER_ROLE         UserId · RoleId (composite PK) · GrantedAt
ROLE_PERMISSION   RoleId · PermissionId (composite PK)
REFRESH_TOKEN     TokenId · UserId→ · TokenHash · ExpiresAt · RevokedAt · CreatedByIp · [+] ReplacedByTokenId  (rotation, reuse detection)
DOCUMENT          DocumentId · EventId→ · UploadedByUserId→ · DocumentType · FileName · BlobUri · Sha256Hash · SensitivityLevel · UploadedAt
AUDIT_ENTRY       AuditId(bigint) · UserId→ · Action · EntityName · EntityId · OccurredAt · IpAddress · BeforeJson · AfterJson
                  (insert-only: app identity has no UPDATE/DELETE; partition monthly; retain 24 months)

── Calendar (D) ───────────────────────────────────────────────────────────────
CALENDAR_ACCOUNT  AccountId · ConnectedByUserId→ · GoogleAccountEmail · GoogleCalendarId · GrantedScope · TokenSecretName · ConnectedAt · RevokedAt
CALENDAR_LINK     LinkId · AccountId→ · SourceEntityType(Event|Milestone|TaskCard) · SourceEntityId · GoogleEventId · LastPushedAt
                  SyncStatus · LastError
[+] CALENDAR_OUTBOX  OutboxId · SourceEntityType · SourceEntityId · Operation(Upsert|Delete) · EnqueuedAt · Attempts · NextAttemptAt · LastError
```

**Indexes** (T1 §5.1.4):

- `EVENT (EventDate, Status)` and `EVENT (ClientId)`.
- `EVENT_MILESTONE (EventId, ScheduledStart)`.
- `TASK_CARD (ColumnId, Position)`.
- `AUDIT_ENTRY (OccurredAt DESC)`.
- `[+]` `CREW_ASSIGNMENT (UserId, EventId)` for crew scoping.
- `[+]` `EVENT_STOCK_REQUIREMENT (RequiredByDate)` for order-list generation.

### Enums

Store enums as strings, using `nvarchar` and an EF value converter.

| Enum | Values |
|---|---|
| `EventStatus` | `Enquired`, `ConfirmedInPlanning`, `InProgress`, `Finished`, `Cancelled` |
| `EventType` | `Activation`, `Corporate`, `Wedding`, `Festival`, `YearEnd`, `Private` |
| `MilestoneType` | `SiteVisit`, `LoadIn`, `Rehearsal`, `Doors`, `Strike`, `LoadOut`, `Debrief`, `Invoice`, `Reconciliation`. FR-04 lists Reconciliation, but the T1 §5.3.3 schema enum omits it; add it there. |
| `MilestoneStatus` | `Planned`, `InProgress`, `Done` |
| Admin board columns (FR-19) | `Assigned`, `InProgressOrNeedsReview`, `Complete` |
| `CardPriority` | `Low`, `Normal`, `High`, `Critical` |
| `QuoteStatus` | `Draft`, `PendingApproval`, `Approved`, `Issued`, `Accepted`, `Superseded` |
| `InvoiceStatus` | `Draft`, `Issued`, `Paid`, `Void` |
| `PaymentMode` | `PurchaseOrder`, `Deposit` |
| `InfrastructureMode` | `Owned`, `Rented` |
| `SourceMode` | `Stock`, `Order`, `Rent` |
| `IncidentType` | `Breakage`, `EquipmentFailure`, `StockShortfall` |
| `SensitivityLevel` | `Normal`, `Confidential` |

**JSON columns.** `EVENT.ServiceScheduleJson` and `CHECKLIST_TEMPLATE.DefinitionJson` use the JSON Schemas in T1 §5.3, which are Draft 2020-12 and validated with NJsonSchema. Copy both schemas into `api/src/Carbonate.Application/Schemas/`.

---

## 7. Security and access control (non-negotiable)

### 7.1 Authentication (C, FR-34, NFR-18–20)

**Login:**

1. `POST /api/auth/login` with `{ email, password }`.
2. If the account has the Director or Accounts role, the response is `{ mfaRequired: true, mfaToken }`. The client then calls `POST /api/auth/mfa/verify` with `{ mfaToken, code }`.
3. First login for those roles goes through MFA enrolment: `POST /api/auth/mfa/enrol` returns an `otpauth://` URI, and `POST /api/auth/mfa/confirm` confirms it.

**Tokens:**

- The access token is a JWT that expires in 15 minutes. It carries the `sub`, role and permission claims.
- The SPA holds the access token **in memory only**, never in localStorage.
- The refresh token sits in a cookie: `HttpOnly; Secure; SameSite=Strict; Path=/api/auth`. It is single-use and rotated on every `POST /api/auth/refresh`.
- Reusing a revoked refresh token revokes the whole chain.
- The session is capped at 8 hours from login.

**Passwords and lockout:**

- Passwords have a 12-character minimum and are checked against the Pwned Passwords k-anonymity range API. If that API is unreachable, fail open and log a warning.
- After five failures the account locks for 15 minutes.
- The login endpoint has a tighter rate limit than the global 100 requests per minute per IP.

**Revocation.** Changing a user's roles, deactivating them or resetting their password bumps `SecurityStamp`. The next refresh checks the stamp and fails, so any access token already issued lapses within 15 minutes.

**Other endpoints.** `GET /api/me` returns `{ user, roles, permissions }`.

> **Note for B.** The SPA uses `permissions` **only** to decide navigation and which buttons to show. It never relies on them to hide data; data hiding is the server's job.

### 7.2 Authorisation (C)

- **Deny by default.** A global fallback policy requires an authenticated user. Every endpoint declares `[HasPermission("<code>")]`, which `PermissionAuthorizationHandler` evaluates (T1 Figure 12).
- **Anonymous endpoints.** `[AllowAnonymous]` is allowed only on login, MFA verification, refresh, `/health` and `/api/csp-report`.
- **Checks go in the service layer.** A controller attribute alone is not enough; services re-check, because services are reused.
- **Object-level checks on every read.** A user may read an event only if they hold `event.view_all` **or** have a `CREW_ASSIGNMENT` to that event. Everything event-scoped (milestones, boards, cards, stock, incidents, documents) inherits this rule. Return `404` for an invisible object, not `403`.
- **Crew scoping:**
  - Crew roles see only cards they are assigned via `TASK_ASSIGNMENT` (FR-21).
  - Admin tasks: crew see only their own.
- **Row-Level Security in Azure SQL** (T1 §7.3, independent second line). Stretch, Phase 3 (C): set `SESSION_CONTEXT('UserId')` and the user's permissions per connection with an EF interceptor, and filter `EVENT` for crew roles. If RLS isn't built, the Task 3 report must say so.

### 7.3 Financial masking (FR-14, FR-35, NFR-17, NFR-28)

There are four tiers. **Any money field belongs to a tier.** If you add a money property, tag it.

| Tier | Permission | Roles that hold it | Fields |
|---|---|---|---|
| `$price` | `finance.view_client_price` | Director, Event Manager, Accounts | Client prices, quote and invoice totals, PO and deposit amounts, `BudgetAmount`, revenue |
| `$cost` | `finance.view_internal_cost` | Director, Event Manager, Accounts | `UnitCostToUs`, `StandardUnitCost`, `EstimatedUnitCost`, `ReplacementCost`, `DirectCostToUs`, `StockVarianceValue`, computed `InternalCostTotal` |
| `$margin` | `finance.view_margin` | Director, Event Manager, Accounts | Margin %, profit, target margin band |
| `$staff` | `finance.view_staff_cost` | Director and Accounts see everyone's; every other role sees **only its own** record | `CrewAssignment.HourlyRate`, computed shift cost |

The first three tiers always move together: no role holds one without the others (T1 §7.6).

**Implementation recipe** (C builds this in Phase 1; everyone uses it):

1. **Do not compute what you cannot show.** Services check permissions before aggregating cost data.
2. **Tag DTO properties.** Each money field is `decimal?` and carries `[FinancialField(Tier.Cost)]` (or `Price`, `Margin`, `Staff`) **and** `[JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]`. A null field is then **omitted** from the JSON, not sent as `null`.
3. **Mask in the service.** Services call `IFinancialMasker.Mask(dto, ICurrentUser)`, which nulls any tagged property the user lacks the permission for, recursively through nested collections. `Staff` tier uses the self rule: keep the value if `dto.UserId == currentUser.UserId`.
4. **Filter as a safety net.** `FinancialMaskingFilter`, a global result filter, re-applies the masker to every `ObjectResult`, so an endpoint that forgets step 3 still cannot leak.
5. **Dynamic Data Masking on `$cost` columns.** The app's managed identity is granted `UNMASK`. DDM protects against direct database access by other principals (developers, report users); it does **not** distinguish app roles. Describe it this way in the Task 3 report.
6. **Acceptance tests (B).** For each cost-bearing endpoint and each of the six roles, assert that the masked keys are **absent from the raw JSON**. These tests run in CI as a required check before any deploy to production (NFR-28).

**Calendar payloads** (FR-42) carry no client names, contact details or money values at all.

### 7.4 Permission catalogue and role matrix

Codes come from T1 §7.6. `[+]` codes complete the matrix for modules Task 1 didn't cover. **C confirms the `[+]` codes at Gate 1.** After that, this table and the seed data are the source of truth. Abbreviations: Dir = Director, Ops = Operations Manager, EM = Event Manager, Acc = Accounts, CL = Crew Lead, CC = Casual Crew; "asg" = assigned only.

| Permission | Dir | Ops | EM | Acc | CL | CC |
|---|---|---|---|---|---|---|
| `event.view_all` | ✓ | ✓ | ✓ | ✓ | | |
| `event.view_assigned` | ✓ | ✓ | ✓ | ✓ | ✓ | ✓ |
| `event.create`, `event.edit` | ✓ | ✓ | ✓ | | | |
| `[+]` `event.delete` | ✓ | | | | | |
| `[+]` `event.transition` (manual stage move, cancel) | ✓ | ✓ | ✓ | | | |
| `[+]` `crew.assign` | ✓ | ✓ | ✓ | | | |
| `finance.view_client_price`, `finance.view_internal_cost`, `finance.view_margin` | ✓ | | ✓ | ✓ | | |
| `finance.view_staff_cost` | ✓ | self | self | ✓ | self | self |
| `[+]` `quote.view`, `quote.edit` (create, edit, copy costings) | ✓ | | ✓ | ✓ | | |
| `quote.approve` | ✓ | | | | | |
| `[+]` `confirmation.record` (PO or deposit) | ✓ | | ✓ | ✓ | | |
| `[+]` `invoice.view` | ✓ | | ✓ | ✓ | | |
| `invoice.manage` | ✓ | | | ✓ | | |
| `[+]` `reconciliation.edit` | ✓ | | | ✓ | | |
| `client.view_contacts` | ✓ | ✓ | ✓ | ✓ | | |
| `task.edit`, `task.move` (event task boards) | ✓ | ✓ | ✓ | | ✓ | asg |
| `admin_task.view`, `admin_task.edit` | ✓ | ✓ | ✓ | ✓ | asg | asg |
| `[+]` `admin_task.assign` | ✓ | ✓ | | | | |
| `admin_task.review` (complete or return) | ✓ | ✓ | | | | |
| `[+]` `venue.edit` (includes site visits) | ✓ | ✓ | ✓ | | | |
| `[+]` `stock.view` | ✓ | ✓ | ✓ | ✓ | ✓ | |
| `[+]` `stock.manage` (catalogue, suppliers, equipment, templates) | ✓ | ✓ | | | | |
| `[+]` `stock.plan` (event stock requirements) | ✓ | ✓ | ✓ | | | |
| `order.generate` | ✓ | ✓ | ✓ | | | |
| `order.approve` | ✓ | | | ✓ | | |
| `[+]` `order.place` | ✓ | | | ✓ | | |
| `[+]` `incident.create` | ✓ | ✓ | ✓ | | ✓ | ✓ |
| `[+]` `incident.view` | ✓ | ✓ | ✓ | | asg | own |
| `[+]` `document.upload` | ✓ | ✓ | ✓ | | | |
| `document.view_confidential` | ✓ | ✓ | ✓ | ✓ | | |
| `[+]` `calendar.view` | ✓ | ✓ | ✓ | ✓ | asg | asg |
| `calendar.connect` | ✓ | ✓ | | | | |
| `user.manage` | ✓ | ✓ | | | | |
| `audit.view` | ✓ | | | | | |

**Rules that sit on top of the matrix:**

- **Separation of duties** (FR-30, T1 §7.5):
  - The user who generated an order list cannot approve it.
  - A quote above the threshold needs the Director's approval. A quote the Director wrote counts as approved.
  - An admin task is completed or returned only by the user who created it, so `TASK_CARD.CreatedByUserId` must match.
- **Accounts on the events board.** Accounts sees the board but has no `task.move` and no `event.transition`, so its board view is read-only.

### 7.5 Web hardening (C for the API, B for the SPA, D for hosting)

- **Transport and headers:**
  - HTTPS only, HSTS, TLS 1.2 minimum.
  - Headers: `X-Content-Type-Options: nosniff`, `X-Frame-Options: DENY`, `Referrer-Policy`, `Permissions-Policy`.
- **Content Security Policy.** `[Δ]` A static host cannot emit a per-request nonce. Instead, the SPA ships **no inline scripts or styles** and uses this policy:

  ```
  default-src 'self'; script-src 'self'; style-src 'self';
  img-src 'self' data: <blob host>; connect-src 'self'; object-src 'none';
  base-uri 'self'; frame-ancestors 'none'; form-action 'self'; report-uri /api/csp-report
  ```

  - Set it in `staticwebapp.config.json` `globalHeaders`, or in API middleware if using hosting option 3.
  - Do not use runtime CSS-in-JS libraries: they inject `<style>` tags that this policy blocks. React `style={}` props are fine, because they set styles through the CSSOM.
  - Self-host the fonts.
- **XSS** (T1 §7.4):
  - Only `TaskCard.Description` permits formatting. It is sanitised server-side on write with HtmlSanitizer against an allowlist (forcing `rel="noopener noreferrer"` on links), and again on render with DOMPurify.
  - `dangerouslySetInnerHTML` appears **once** in the codebase, in `SanitisedDescription.tsx`.
  - ESLint enforces `react/no-danger` (with that file exempted) and `no-unsanitized`.
  - Never use `innerHTML`, `eval` or `new Function`.
- **Uploads:**
  - Size cap of 10 MB.
  - Allowlist of types: images (jpeg, png, webp, heic) and pdf for documents.
  - SHA-256 hash stored and verified.
  - Files are served from Blob Storage with `Content-Disposition: attachment` and an explicit content type, through short-lived SAS links or an API stream. Never use public containers.
- **Audit:**
  - `IAuditService.RecordAsync(action, entityName, entityId, before, after, userId)` is called on **every write path**.
  - It records the actor, timestamp and IP address.
  - It never records secrets, passwords or MFA secrets.
- **Logs.** No personal data or secrets in application logs.

---

## 8. Modules: rules, endpoints, tests

The endpoint lists below are the proposed contract; C finalises them in Swagger by Gate 1. Every module also writes:

- unit tests for its business rules;
- integration tests per endpoint covering happy path, 403/404 by role, and validation;
- masking acceptance rows wherever a `$` field appears.

### 8.1 Platform (C)

**Endpoints:**

- `POST /api/auth/login`, `/mfa/verify`, `/mfa/enrol`, `/mfa/confirm`, `/refresh`, `/logout`
- `GET /api/me`
- `GET|POST /api/users` and `PATCH /api/users/{id}` (roles, `isActive`), with `user.manage`
- `GET /api/audit?entity=&from=&to=` with `audit.view` (FR-37, Should)
- `GET /health`

**Rules:**

- **FR-38, NFR-29.** The Director and Operations Manager can create users, deactivate them and change their roles. The system **must keep at least one active Director**.
- **FR-36, NFR-21.** `CrewAccountExpiryWorker` runs daily. It deactivates a `CasualCrew`-only account when all of the following hold:
  - every event they are assigned to has a `Debrief` milestone whose end (`ActualEnd`, falling back to `ScheduledEnd`) is more than 7 days ago;
  - they have no future assignment.

  Each deactivation is audited.
- **First-run seeding** (NFR-02, together with D's seed data): roles, permissions, divisions, the admin board with its three columns, checklist templates and default stock quantities. Seeding must be idempotent.

### 8.2 Events and milestones (C)

**Endpoints:**

- `GET /api/events?status=&from=&to=&clientId=&q=`. Visibility filtering applies. For the board view, omit `Enquired` (FR-03).
- `GET /api/events/{id}`, `POST /api/events`, `PUT /api/events/{id}` (with `rowVersion`), `DELETE /api/events/{id}` (`event.delete`)
- `GET /api/events/{id}/milestones`
- `POST /api/events/{id}/milestones/{milestoneId}/reschedule` with `{ newStart, newEnd, rowVersion }`. Returns `ScheduleResultDto` (the moved milestones).
- `GET|POST|DELETE /api/events/{id}/crew` (`crew.assign`) (FR-07)
- `GET /api/events/{id}/cost-history`, which compares this event with previous events for the same client (FR-09). Requires the finance permissions.
- Should: `PUT /api/events/{id}/service-schedule` (FR-05), `GET|POST /api/events/{id}/documents` (FR-06), `PATCH /api/events/{id}/pack-size` (FR-08)

**Rules:**

- **FR-01 (create):**
  - Required fields: client, venue, division, `EventDate`, `StartsAt`/`EndsAt`, `EventType`, `PackSizeEstimated`, `PaymentMode`, `InfrastructureMode`, `StaffRequired`. `BudgetAmount` is optional and `$price`.
  - On create, call **D's** `IEventTemplateSeeder.SeedAsync(eventId, eventType, divisionId, packSize)` in the same transaction. It creates the event's task board and stock requirements (FR-25, NFR-03).
  - Also create the default milestone chain: `SiteVisit → LoadIn → Rehearsal → Doors → Strike → LoadOut → Debrief → Invoice → Reconciliation`, finish-to-start, lag 0. Doors is seeded from `StartsAt` and Strike from `EndsAt`.
  - New events start in `Enquired`.
- **FR-04 (milestone cascade)** lives in `ScheduleCalculator.Recalculate`, in Domain, with no database access. Rescheduling milestone M by delta d works as follows:
  1. Every transitive successor shifts by d, keeping its duration.
  2. In topological order, each milestone is then pushed later if needed so that `start ≥ max(predecessor.ScheduledEnd + lag)`.
  3. Milestones with `ActualStart` set never move. If a reschedule would move one, it is rejected with `422`.
  4. A dependency that would create a cycle is rejected with `422`.

  Unit-test these cases: a linear chain; a diamond; a multi-predecessor push; a pull earlier; a frozen actual; a cycle.
- **FR-10 / NFR-15.** A stale `rowVersion` returns `409` with the current server version in the problem's `extensions.current`.
- **FR-09.** Records are never hard-deleted except by the Director's `event.delete`, which soft-deletes.
- **Confidential events** (`IsConfidential`):
  - A banner shows on the card and detail view.
  - Documents flagged `Confidential` need `document.view_confidential`.

### 8.3 Event lifecycle (D) (FR-02, State and Observer patterns, T1 §6.1)

**Endpoints:**

- `POST /api/events/{id}/transitions` with `{ to: "ConfirmedInPlanning" | "InProgress" | "Finished" | "Cancelled", rowVersion }`. Requires `event.transition`.
- `GET /api/events/{id}/allowed-transitions`, which the SPA uses to enable drag targets.

**State machine.** In `Domain/Lifecycle`, each state is a class implementing `IEventState`, which owns its allowed transitions:

| From | To | How |
|---|---|---|
| `Enquired` | `ConfirmedInPlanning` | Only when an `EVENT_CONFIRMATION` exists (US-12). C's confirmation endpoint calls `IEventLifecycleService.ConfirmAsync`. |
| `ConfirmedInPlanning` | `InProgress` | Automatic at `StartsAt`, or manual (early start) |
| `ConfirmedInPlanning` | `Cancelled` | Manual. Cancelled is reachable **only** from here (FR-02). |
| `InProgress` | `Finished` | Automatic at `EndsAt`, or manual |

Any other transition returns `409` with `type: "/problems/invalid-transition"`. `Finished` and `Cancelled` are terminal.

**Transition worker.** `EventTransitionWorker` is a `BackgroundService` in the API. Each cycle it:

1. Queries for events that are due.
2. Applies the transitions through the state machine, with system actor `system:scheduler`.
3. Computes the next due `StartsAt`/`EndsAt` and **sleeps until then**, capped at 30 minutes. Do not poll every minute; that keeps the serverless database awake (§4).

**Observer.** Every transition, manual or automatic, publishes `EventStateChanged`. Subscribers implement `IEventStateObserver`:

- `CalendarSyncObserver` enqueues a calendar update.
- `AuditObserver` writes the audit entry.
- `NotificationObserver` notifies the Event Manager. An in-app notification is enough; email is out of scope unless time allows.

The state machine never references calendars or notifications directly.

### 8.4 Commercial cycle (C) (FR-11–17)

**Endpoints:**

- `GET|POST /api/events/{id}/quotes` and `GET|PUT /api/quotes/{id}`. Lines are edited inline; totals are recomputed server-side and stored.
- `POST /api/events/{id}/quotes/copy` with `{ sourceQuoteId }`. Copies lines, quantities and prices from a previous event of the **same client**, and sets `CopiedFromQuoteId` (FR-15).
- `POST /api/quotes/{id}/submit`, `/approve` (`quote.approve`), `/issue`, `/accept`
- `POST /api/events/{id}/confirmation` with a PO or deposit (FR-12, `confirmation.record`). This triggers the lifecycle confirm.
- `GET /api/invoices?status=` and `POST /api/events/{id}/invoices` (FR-13). Then `PATCH /api/invoices/{id}` to mark it paid (`invoice.manage`).
- Should: `GET /api/invoices/uninvoiced-events` (FR-16), `GET|PUT /api/events/{id}/reconciliation` (FR-17)

**Rules:**

- **FR-14.** A quote whose `TotalIncVat` exceeds `Quotes:ApprovalThresholdZar` cannot be issued until it reaches `Approved`, which requires `quote.approve` (the Director). A quote the Director wrote is approved on submit.
- **FR-11.** The quote response includes the target margin band for the event's pack size from `Quotes:MarginBands` (`$margin`), and the actual margin (`$margin`).
- **Quote versions.** Editing a quote after it has been issued creates a new `Version`; the old version becomes `Superseded`. An issued quote's totals never change (T1 §5.2.1).
- **FR-13.** An invoice requires an `EVENT_CONFIRMATION` and carries its PO number or deposit reference.

### 8.5 Boards and tasks (B) (FR-18–23)

There are **three** different "board" concepts. Do not conflate them.

1. **Events board (FR-18).** Cards are *events*; columns are `ConfirmedInPlanning | InProgress | Finished`. It is not stored in `BOARD`. Data comes from C's `GET /api/events`, and moves go through D's `/transitions`. Columns that change automatically show an "Auto" badge, as in the prototype.
2. **Event task board.** `BOARD.BoardType='Event'`, one per event. It holds the checklist cards seeded from the template.
3. **Admin Tasks board (FR-19).** `BoardType='Admin'`, a singleton with no event. Its columns are Assigned, In Progress / Needs Review, and Complete.

**Endpoints:**

- `GET /api/boards/admin`, `GET /api/events/{id}/board`
- `POST /api/boards/{boardId}/cards`, `GET|PATCH /api/cards/{id}` (with `rowVersion`)
- `POST /api/cards/{id}/move` with `{ columnId, position, rowVersion }`
- `PUT /api/cards/{id}/assignees`
- `POST /api/cards/{id}/complete` and `/return` with `{ reviewNotes }` (FR-20)
- Should: `GET|POST /api/cards/{id}/attachments` (FR-22). These use D's `IFileStorage`.

**Rules:**

- **FR-21.** Crew roles list and act only on cards assigned to them.
- **FR-20, admin flow:**
  - A reply or attachment from the assignee moves the card to `InProgressOrNeedsReview`.
  - **Only the creating manager** (`CreatedByUserId`) can move it to `Complete`, or return it to `Assigned` with `ReviewNotes`, which sets `ReturnedByUserId` and `ReturnedAt`.
  - The assignee can never complete the card.
- **Moves:**
  - Optimistic on the client.
  - On the server, check `rowVersion` and `WipLimit` (FR-23, display only), and renumber positions in that column within one transaction.
- **Due dates.** A card's `DueAt` is pushed to Google Calendar through D's outbox (FR-40, US-10).

### 8.6 Venues and site visits (B) (FR-32, FR-33)

**Endpoints:**

- `GET|POST /api/venues`, `GET|PUT /api/venues/{id}`. Venues persist between events and are reused (FR-32).
- `GET|POST /api/events/{id}/site-visits` and `PUT /api/site-visits/{id}`

**Rules:**

- Crew roles see venue access details only for venues of events they are assigned to (US-23).
- `AccessRoute`, `SignInProcedure` and `Notes` are plain text, length-limited and encoded on render.

### 8.7 Stock, templates and order lists (D) (FR-24–30)

**Endpoints:**

- `GET /api/stock/categories`, `GET|POST|PUT /api/stock/items`, `GET|POST|PUT /api/suppliers`, `GET|POST|PUT /api/equipment` (`stock.manage`)
- `GET|PUT /api/events/{id}/stock-requirements` (`stock.plan`). The response includes `warnings[]` for each line.
- `POST /api/order-lists/generate` with `{ from, to }`, which returns one list per supplier (FR-28)
- `GET /api/order-lists`, `GET /api/order-lists/{id}`, `POST /api/order-lists/{id}/submit`, `/approve` (FR-30), `/mark-placed`
- `GET /api/checklist-templates`
- `IEventTemplateSeeder`: the contract C calls on event create.

**Rules:**

- **FR-25 seeding** (NFR-03: an event is set up in under 5 minutes):
  - Pick the active template matching the division, `BoardType='Event'` and `EventType`.
  - Create the board's columns and cards. Each card's `DueAt` is the matching milestone time plus `offsetHours`.
  - Create a stock requirement for every `defaultStockRequirements` entry: `QuantityRequired = ceil(quantityPerHundredGuests × packSize / 100)`, `RequiredByDate` = LoadIn date, using the template's `SourceMode`.
- **FR-27, shortfall warning:**
  - `expected = ConsumptionPerHundredGuests × (PackSizeActual ?? PackSizeEstimated) / 100`.
  - If `QuantityRequired < expected`, add `{ code: "SHORTFALL", expected, planned }`.
  - Show the warning while the LoadIn milestone is in the future.
- **FR-28, order list:**
  - Include requirements of events in `ConfirmedInPlanning` or `InProgress` with `RequiredByDate` in [from, to] and `SourceMode` of `Order` or `Rent`.
  - Group by `StockItem.DefaultSupplierId`.
  - `QuantityOrdered` is the sum of `QuantityRequired`. `RequiredByDate` is the earliest date in the group. `EstimatedUnitCost` (`$cost`) is the item's `StandardUnitCost`.
  - Items with no supplier go into an "Unassigned supplier" warning, not a list.
  - Must complete in **under 5 s for a seeded December** (NFR-08). Use set-based EF queries and no N+1 queries.
- **FR-29, lead time.** If `today + Supplier.LeadTimeDays > RequiredByDate`, add `{ code: "LEAD_TIME", leadTimeDays, requiredBy }`. This is `ValidateLeadTime` in T1 Figure 10.
- **FR-30, approval.**
  - The list moves `Draft → PendingApproval → Approved → Placed`.
  - The approver must have `order.approve` **and must not be** `GeneratedByUserId`.
- **Masking.** This module carries `$cost` fields: `StandardUnitCost` and `EstimatedUnitCost`. Use the masking recipe (§7.3); C reviews.

### 8.8 Incidents and file storage (D) (FR-31)

**Shared file storage.** `IFileStorage` (`Platform/Files`) is needed by FR-06, FR-22 and FR-31. **Build it first in Phase 2.** It provides:

- `UploadAsync(stream, fileName, contentType)`, returning `{ blobUri, sha256 }`;
- `OpenReadAsync`;
- `GetDownloadUrl` (a short SAS link).

It validates file type and size and computes the SHA-256 hash.

**Endpoints:**

- `POST /api/events/{id}/incidents`, as multipart `{ incidentType, assetId?, stockItemId?, quantity, description, photo? }`
- `GET /api/events/{id}/incidents`, `GET /api/incidents?assetId=`, `PATCH /api/incidents/{id}` (resolution notes, `ReplacementCost` `$cost`)

**Rules:**

- At least one of `assetId` or `stockItemId` must be set (a CHECK constraint, plus validation).
- Crew can report incidents against assigned events only.
- The form is quick-entry and mobile-first, built around the client's common failures: glassware, ice machines and electricals (T1 §11).

### 8.9 Google Calendar sync (D) (FR-40–42, NFR-12)

**Endpoints:**

- `GET /api/calendar/connection`
- `POST /api/calendar/connect`, which returns the Google OAuth URL
- `GET /api/calendar/oauth/callback`
- `DELETE /api/calendar/connection` (`calendar.connect`)

**How it works:**

- **Scope.** Request the narrowest Google scope that allows writing events (`calendar.events`). Carbonate makes **insert and update calls only, never reads**.
- **Tokens.** The refresh token goes to Key Vault under `TokenSecretName`; the database holds only that reference.
- **Outbox.** Observers and the boards module write `CALENDAR_OUTBOX` rows. `CalendarSyncWorker` then:
  1. Takes due rows.
  2. Checks `CALENDAR_LINK` for the source entity: if a link exists it **updates** that Google event, otherwise it inserts one and stores `GoogleEventId` (FR-41: no duplicates).
  3. On failure, retries with exponential backoff (`Attempts`, `NextAttemptAt`) and records `LastError`.
  4. Never drops a row, and never blocks a user request (NFR-12).
- **Payload** (FR-42):
  - Title: `EventCode — <milestone or event name>`. For confidential events: `Confidential event — <EventCode>`.
  - Includes dates and the venue name.
  - **No** client names, contact details or money values.
- **Google testing-mode limit.** While the Google OAuth consent screen is in "Testing" with an External user type, [refresh tokens expire after 7 days](https://www.unipile.com/google-oauth-refresh-token/). Reconnect the calendar before the demo, or move the app to production status. The UI shows a "reconnect needed" state when a push fails with `invalid_grant`.

---

## 9. Front end (B)

**Screens** (T1 Table 7; prototype at `https://st10444972-ethan-algeo.github.io/INSY7315-Carbonate-Prototype/`):

- Login, plus MFA
- Events board
- Event detail, which has two variants: full, and no-cost with cost rows **absent**
- Create/edit event
- Admin tasks board
- Admin task detail
- Stock & order lists
- Incident report
- Quotes & invoices
- Native calendar view
- Settings: users and roles, audit log, Google Calendar connect

**Availability by role:**

- Accounts is routed to invoices and order approvals, and is read-only on the events board.
- The Operations Manager has **no** quotes or invoices screen. T1 Table 7 says "Create/edit"; §7.6 overrides it.
- The crew roles see assigned events and tasks only, plus incident report.

**Layouts:**

- **Desk roles** get a sidebar with this navigation order: Events, Admin Tasks, Stock & Orders, Quotes & Invoices, Calendar, Settings.
- **Crew Lead and Casual Crew** get a bottom tab bar on a phone-width layout: My events, My tasks, Report incident. They must be fully usable at **360 px** (NFR-23), with 44 px touch targets (NFR-24).

**Design tokens.** Carry over the tokens from the prototype's `style.css`:

```
--ink #16211c · --paper #f7f8f6 · --surface #ffffff · --accent #2f8f63 · --accent-strong #1f6b48 · --accent-tint #e6f2ec
--line #dde3de · --muted #5b6b62 · --warning #b54708 · --warning-tint #fdf1e8 · --danger #b42318
headings: Manrope 600 · body: Inter 14px/1.5 · radius 6px
```

The logo comes from the prototype repo (`CarbonateLogo.png`). Check every text and colour pair against a 4.5:1 contrast ratio.

**Rules:**

1. **Render only what the API returns.** If a `$` field is absent, the row or column is absent too: no dash, no "hidden", no greyed-out placeholder.
2. **Optimistic card moves** must show within 300 ms (NFR-07), using TanStack Query `onMutate`. Roll back on error, and on a `409` show a "changed by someone else" toast with a reload action (NFR-15).
3. **Every screen** has loading, empty, error and success states. Problem-details messages are shown in plain language.
4. **Vocabulary** (NFR-05, Appendix C): use pack size, load-in, strike, recce, costing. Never use "sprint", "project" or "ticket".
5. **Formats** (NFR-32): `Intl.NumberFormat('en-ZA', { style: 'currency', currency: 'ZAR' })` for money, and `Intl.DateTimeFormat('en-ZA', { timeZone: 'Africa/Johannesburg' })` for dates, giving day-month-year.
6. **Accessibility:**
   - Drag-and-drop has a keyboard alternative: the dnd-kit keyboard sensor, plus a "Move to…" menu on each card.
   - All controls are labelled; focus is visible and managed in dialogs.
   - Run axe in tests.
   - Check with keyboard only and with a screen reader on the boards and forms.
7. **Performance.** Meaningful content within 4 s on a 1 Mbps throttled profile (NFR-06):
   - Split code by route.
   - Load crew screens first.
   - No heavy chart or calendar library on the crew path.
8. **Browsers.** Test the current and previous versions of Chrome, Safari, Edge and Firefox (NFR-25).
9. **Auth handling:**
   - On app load, call `/api/auth/refresh` to restore the session.
   - On a `401`, refresh once and retry the request; if that fails, go to Login.
   - Never put tokens in storage.

---

## 10. Testing and verification

| Layer | What | Owner |
|---|---|---|
| Domain unit tests | `ScheduleCalculator`, state machine transitions, shortfall, lead time, order grouping, quote totals and VAT, crew expiry predicate | The module owner |
| API integration tests | Every endpoint: happy path, validation `400`, `403` without permission, `404` when not visible, `409` concurrency/transition | The module owner |
| **Masking acceptance suite** | Every endpoint returning a `$` field × the 6 seeded role accounts. Assert keys are **absent from the raw JSON**, plus the staff self rule. **A required CI check** (NFR-28). | B (C reviews; D wires the check) |
| Security tests | Object-level access (crew requesting an unassigned event gets `404`); token expiry; refresh reuse detection; lockout; rate limit; XSS payloads from the OWASP cheat sheet on every free-text field; ZAP baseline scan on staging before submission | C, with D for ZAP |
| SPA tests | Component tests with MSW; axe checks; masked-render test (a no-cost DTO renders no cost row) | B |
| E2E smoke | After deploy to staging: health, login per role, open an event, generate an order list | D |
| NFR verification | NFR-01 (5 untrained testers, under 2 min); NFR-03 (event set-up under 5 min); NFR-06 (throttled timing); NFR-08 (December order list under 5 s); NFR-13/14 (restore drill). Record the results in `docs/nfr-results.md`. | B (UI), D (order list, uptime, restore, setup), C (API timing and security) |

**Test data.** D builds `SeedData.Demo` (Appendix D). Integration tests seed their own minimal data per test class; they never depend on the demo seed.

---

## 11. DevOps (D)

**Branch protection** on `main` and `dev`:

- PR required, with 1 approving review.
- CI must be green.
- Linear history.
- No force-push.

**Workflows:**

| File | Trigger | Jobs |
|---|---|---|
| `ci.yml` | PR to `dev` or `main` | **API:** restore, build (warnings as errors in `Domain`/`Application`), unit tests, integration tests (Testcontainers), `dotnet list package --vulnerable --include-transitive` (fail on High/Critical). **Web:** `npm ci`, lint, typecheck, test, build, `npm audit --audit-level=high`. **Masking suite** as a separate required job. Test reports published to the run summary. |
| `cd-dev.yml` | Push to `dev` | Run CI, then deploy the API to the `dev` slot and the SPA to the dev environment, then run the smoke test |
| `cd-prod.yml` | Push to `main` | Run CI, deploy the API to `staging`, deploy the SPA, smoke test, then **manual approval** (GitHub environment `production`), then slot swap |

**Azure authentication.** Use OIDC federated credentials with `azure/login` (client, tenant and subscription IDs only). No publish profiles or secrets in GitHub.

**Supply chain:**

- Dependabot for NuGet, npm and GitHub Actions.
- Secret scanning and push protection on.

**Releases:**

- Tag `main` at each gate: `v0.1-gate1` … `v1.0-task2`.
- Deploy to production only Monday–Wednesday, 09:00–15:00 SAST, and never within 48 h of a load-in milestone on real client data (NFR-11). During the Task 2 build with demo data this rule doesn't bind, but the README states it.

**README** (B writes the content, D the setup section). It must let a new developer get the system running in 30 minutes (NFR-26):

- overview;
- live URL;
- architecture summary;
- prerequisites;
- `docker compose up`, user-secrets, `dotnet ef database update`, `npm run dev`;
- demo logins per role (demo environment only; remove them before any real client data is loaded);
- how to run the tests;
- links to the Task 1 document and the presentation slides.

---

## 12. Phases and gates

The phases are roughly equal in length. Set the dates once the Week 9 due date is confirmed.

| Phase | Target date | Gate (must be true to exit) |
|---|---|---|
| 1. Foundations | _TBC_ | Login works on the dev slot · Schema v1 (all tables, including `[+]`) migrated · Masking mechanism and permission policies in place · OpenAPI contract with every Phase 2 endpoint stubbed · CI green |
| 2. Modules | _TBC_ | All 29 Must FRs merged into `dev` with tests |
| 3. Integration | _TBC_ | Production deploys from the pipeline · Masking suite green and required · Calendar sync live · Screens wired to the real API |
| 4. Hardening | _TBC_ | UAT done with Carbon staff · Accessibility audit passed · README complete · Demo rehearsed and recorded · Link submitted on ARC |

### Work per member

| Phase | B: Christiaan (acting lead) | C: Ethan | D: Ulrich |
|---|---|---|---|
| 1 | Repo skeleton and `web/` scaffold · SPA shell, routing, auth flow, token handling, layouts, design tokens · MSW mocks from the contract · Boards and venues endpoints into the contract · Masking test harness (six role clients) · `docs/traceability.md` · Attendance log started | Solution scaffold · Schema v1 and migrations (including every `[+]`) · Auth, MFA, refresh · Permission handler · `IFinancialMasker` and filter · `IAuditService` · Swagger contract | Repo rules, CI · Azure resources in SA North (same-site hosting decision, §4) · Key Vault, managed identity · `cd-dev` · Seed data spec and demo data (Appendix D), with real figures from the client through B |
| 2 | Venues and site visits API (first PR, small) · Boards and cards API · Events board, event detail (both variants), event form, admin board and task detail, event task board · Crew screens | Events, milestones, cascade, crew assignment · Commercial cycle · Users · Crew expiry worker | `IFileStorage` (first) · **Template seeder (FR-25), early: it's on the critical path** · Stock catalogue, requirements, shortfall, order lists · State machine, transitions, `EventTransitionWorker`, observers · Incidents API |
| 3 | Masking suite across all `$` endpoints · Stock, finance, incident and settings screens · Optimistic moves and conflicts · UI NFR runs | Review of all `$` and permission PRs · Shoulds FR-10, FR-37 · API timing and security NFR runs · RLS (stretch) | Calendar OAuth and outbox worker · `cd-prod` with approval and swap · App Insights, availability test · Restore drill · Order list under 5 s load test |
| 4 | UAT with Carbon staff · README content · Presentation storyline and slides · Accessibility and cross-browser pass · Demo rehearsed and recorded | Security test pass · Bug fixes | ZAP scan · Smoke tests · Hosting rationale and setup section in README · ARC submission |

**Critical path:** Schema v1 → auth and masking → events API → D's template seeder → wired screens → production deploy.

- B never waits on C: B builds against MSW mocks generated from the contract.
- B and D start their modules as soon as Schema v1 merges.
- **Watch D's Phase 2.** Lifecycle, incidents and stock all land there. D builds the seeder first; if D is behind at mid-phase, B takes order-list generation (FR-28) (§2 fallback).
- **Weekly check.** At each session B compares progress with this table, and moves work early rather than at the gate.

---

## 13. Definition of done (every PR)

- [ ] It does one thing, and the commit message names the FR/US it serves.
- [ ] Business rules have unit tests; endpoints have integration tests (happy path, `403`, `404`, validation).
- [ ] If it returns a `$` field: the field is tagged, masked in the service, and has masking-suite rows. C has approved.
- [ ] If it adds an endpoint: it has a permission attribute, object-level checks, and is in the OpenAPI contract.
- [ ] If it writes data: it calls `IAuditService`.
- [ ] If it changes the schema: it contains one migration, rebased on `dev`, and C has approved.
- [ ] If it adds UI: the screen has loading, empty and error states, works at 360 px where it is crew-facing, passes axe, and uses client vocabulary.
- [ ] CI is green and it deploys to the dev slot.
- [ ] `docs/traceability.md` row updated: endpoint, screen, tests, status.
- [ ] No secrets, no `console.log` of data, no `TODO` without `(plan)` and a PR note.

---

## 14. Rules for coding agents

1. **Stay in your module.** Edit only the folders for the module and FR IDs you were given. To change shared code (`Platform/`, `Common/`, DTOs that others consume, permission codes, the schema), make the smallest change possible and flag it for the owner in the PR description.
2. **Never weaken the security model.** Do not:
   - add `[AllowAnonymous]`;
   - skip the permission attribute or the service-layer check;
   - return an entity instead of a DTO;
   - add an untagged money field;
   - store tokens in browser storage;
   - log personal data;
   - use `innerHTML` or `dangerouslySetInnerHTML` outside `SanitisedDescription.tsx`.

   If a task seems to require any of these, stop and ask.
3. **Do not invent requirements.** Implement what the FR and this plan say. If something is ambiguous, write `// TODO(plan): <question>` and list it in the PR.
4. **Keep the client's words.** Use the names in this plan for UI copy and domain names: pack size, load-in, strike, recce, costing, debrief.
5. **Follow the conventions** in §5: routes, problem details, UTC storage, `DateOnly`, `decimal(18,2)`, string enums, `rowVersion`.
6. **Prefer the boring option.** This is a 15-user system: one API, EF Core, plain services. Do not add a message bus, CQRS framework, microservice, Redis cache or GraphQL. Note any new package in the PR, with the reason.
7. **Tests come with the code.** Never mark work done without the tests in §10 for that change.
8. **Migrations.** Never edit a merged migration. Never run destructive SQL against shared Azure databases. Use local Docker for experiments.
9. **Secrets.** Never write keys, connection strings or tokens into code, config files, tests or docs. Use user-secrets locally and Key Vault in Azure.
10. **Commit hygiene.** The human commits under their own account, with messages like `FR-xx: …`. Do not squash other members' commits.

---

## 15. Open decisions and Task 1 discrepancies

B owns this list, as acting team lead. Each fix lands in the Task 3 report as "updated since Task 1".

| # | Item | Proposed resolution | Owner |
|---|---|---|---|
| 1 | **FR-04 is missing from T1 Table 5.** The milestone cascade appears throughout the design but dropped out of the FR table. | Treat FR-04 as **Must**, from US-13. Restore it in the report. | B |
| 2 | T1 Table 7 gives the Operations Manager "Create/edit" on quotes and invoices, but §7.6 gives Ops no financial permissions. | Follow §7.6: no quotes screen for Ops. Fix Table 7. | B |
| 3 | `[Δ]` .NET 8 reaches end of support on 10 Nov 2026. | Build on .NET 10 LTS (§4). Confirm as a team. | C, D |
| 4 | Same-site hosting is needed for `SameSite=Strict` refresh cookies. | SWA Standard with a linked backend, or custom domains, or SPA from `wwwroot` (§4). | D |
| 5 | A nonce-based CSP isn't possible on static hosting. | `script-src 'self'` with no inline scripts (§7.5). Update T1 §7.4. | B, C |
| 6 | DDM and RLS with a single app identity work differently from what T1 implies. | DDM covers direct database access; RLS through `SESSION_CONTEXT` is a stretch (§7.2, §7.3). Describe what is actually built. | C |
| 7 | `EVENT` has no start/end times, but FR-02 needs them. | Add `StartsAt` and `EndsAt` (§6). | C |
| 8 | No column for staff cost (FR-35, third tier). | Add `CREW_ASSIGNMENT.HourlyRate` as `$staff`. | C |
| 9 | Missing columns for approval, separation of duties and photos. | `QUOTE` approval columns, `ORDER_LIST` approval columns, `TASK_CARD.CreatedByUserId`, `INCIDENT_REPORT` photo columns, `SITE_VISIT` licence plate/driver/crew, `STOCK_CATEGORY.DivisionId`, `QUOTE_LINE.Category` (all `[+]` in §6). | C |
| 10 | Quote approval threshold value and margin bands by pack size. | Ask the Director and Bookkeeper; until then use placeholder config values flagged in the README. | B |
| 11 | The prototype shows the Event Manager assigning admin tasks, but T1 §7.6 limits admin task review to Director and Ops. | Follow §7.6 (`admin_task.assign` for Director and Ops); confirm with the client at UAT. | B |
| 12 | Enquired events that never proceed: FR-02 doesn't allow Enquired → Cancelled. | Keep the FR. A Director soft-deletes stale enquiries. | C |
| 13 | T1 §9 prices SQL at Basic/Standard; §5 chose serverless. | D decides from actual credit; align §9 in Task 3. | D |
| 14 | No crew lead or casual crew member was interviewed (T1 §2.1 limitation). | Include one in NFR-01 testing and UAT. | B |
| 15 | **Jack is in hospital and out for Task 2.** | Email the lecturer or WIL coordinator now, with the date and the reassignment in §2. Log his absences in the attendance log as "hospitalised, apologies", and keep that email as evidence for attendance and peer evaluation. | B |
| 16 | Role A's Task 3 deliverables (final report compilation, user guide, EXPO board) have no owner. | Decide after Task 2, depending on whether Jack is back. | B, with the team |

---

## Appendix A: Functional requirements

M = Must, S = Should, C = Could. "Owner" is the API owner; B builds all UI.

| ID | Requirement (short) | Pri | Module | Owner |
|---|---|---|---|---|
| FR-01 | Record an event with all booking fields | M | Events | C |
| FR-02 | Stages Enquired → ConfirmedInPlanning → InProgress → Finished; last two automatic; Cancelled only from ConfirmedInPlanning | M | Lifecycle | D |
| FR-03 | Enquired events excluded from the events board | M | Events | C |
| FR-04 | Milestones with finish-to-start dependencies and lag; moving one cascades | M | Events | C |
| FR-05 | Intra-day service schedule (JSON) | S | Events | C |
| FR-06 | Event documents; confidential flag and banner | S | Events | C |
| FR-07 | Assign named crew to an event | M | Events | C |
| FR-08 | Pack size estimate and actual | S | Events | C |
| FR-09 | Indefinite retention; compare costs with the same client's previous events | M | Events | C |
| FR-10 | Reject conflicting concurrent edits with a warning | S | Events | C |
| FR-11 | Costing lines with internal cost and client price; target margin band | M | Commercial | C |
| FR-12 | Client confirmation by PO or deposit | M | Commercial | C |
| FR-13 | Invoice carries the confirmation reference and links to the event | M | Commercial | C |
| FR-14 | Cost and margin for Director, Accounts and EM only; Director approval above a threshold | M | Commercial | C |
| FR-15 | Copy a previous costing for the same client | M | Commercial | C |
| FR-16 | List delivered but uninvoiced events | S | Commercial | C |
| FR-17 | Post-event reconciliation | S | Commercial | C |
| FR-18 | Events board, 3 active stages, manual and automatic moves | M | Boards | B (UI) |
| FR-19 | Admin tasks board, 3 stages | M | Boards | B |
| FR-20 | Only the assigning manager completes or returns, with notes | S | Boards | B |
| FR-21 | Assign cards; crew see only their own | M | Boards | B |
| FR-22 | Files and photos on cards | S | Boards | B |
| FR-23 | Display WIP limits | C | Boards | B |
| FR-24 | Stock catalogue by category and division | M | Stock | D |
| FR-25 | Seed the board and stock from a template, quantities per 100 guests | M | Stock | D |
| FR-26 | Event stock requirement by item, quantity and source mode | M | Stock | D |
| FR-27 | Shortfall warning against expected consumption | M | Stock | D |
| FR-28 | Consolidated order list over a period | M | Stock | D |
| FR-29 | Supplier lead-time warning | S | Stock | D |
| FR-30 | Order list approved by a different user | S | Stock | D |
| FR-31 | Incident reports (breakage, failure, shortfall), asset link, photo | M | Incidents | D |
| FR-32 | Persistent venue records | M | Venues | B |
| FR-33 | Site visit details | M | Venues | B |
| FR-34 | Authentication; multiple roles per account; MFA for Director and Accounts | M | Platform | C |
| FR-35 | Server-side financial tiers (price/cost/margin; staff cost self rule) | M | Platform | C |
| FR-36 | Crew scoped to assigned events; deactivated 7 days after debrief | M | Platform | C |
| FR-37 | Immutable audit trail viewable by the Director | S | Platform | C |
| FR-38 | Director and Ops manage users and roles | M | Platform | C |
| FR-39 | Native month/week calendar view | S | Calendar | B |
| FR-40 | Push event dates, milestones and task due dates to Google; outbound only | M | Calendar | D |
| FR-41 | Update the existing entry, never duplicate; queue and retry | M | Calendar | D |
| FR-42 | No client names, contacts or money in calendar entries; redacted confidential titles | M | Calendar | D |

## Appendix B: Non-functional requirements that code must meet

| NFR | Target | Where it bites | Verify (owner) |
|---|---|---|---|
| 01 | Untrained user finds events, call times and tasks in under 2 min | Crew screens | 5 testers (B) |
| 02 | Zero client configuration; seeded on first run | Seeder | Fresh deploy (D, C) |
| 03 | Event and its board and stock created in under 5 min | Create flow, template seeder | Timed run (B) |
| 04 | Primary task within 3 navigation steps | Navigation | Journey review (B) |
| 05 | Client vocabulary | All UI copy | Terminology audit (B) |
| 06 | Under 2 s at 5 Mbps; under 4 s at 1 Mbps | SPA bundle, API latency | Throttled profiling (B) |
| 07 | Card move shown in under 300 ms | Optimistic UI | Measured (B) |
| 08 | December order list in under 5 s | Order generation | Load test (D) |
| 09 | Any past costing in under 2 s | Commercial queries, indexes | Synthetic 10-year data (C) |
| 10 | 99.0% uptime (99.5% Oct–Jan) | Hosting | App Insights availability test (D) |
| 11 | Maintenance windows | Release policy | README and process (D) |
| 12 | Calendar failure never blocks core functions | Outbox | Integration disabled test (D) |
| 13/14 | RPO 24 h, RTO 4 h; 35-day PITR; restore tested | Azure SQL | Restore drill (D) |
| 15 | Never silently overwrite | `rowVersion` | Concurrent edit test (C, B) |
| 16 | Audit insert-only | DB permissions | Attempted modification test (C) |
| 17 | Masked values absent from the response | Masking | Masking suite (B) |
| 18 | MFA for Director and Accounts | Auth | Per-role auth test (C) |
| 19 | 15-min access token, 8-h session | Auth | Expiry test (C) |
| 20 | TLS 1.2+; rate limit and lockout | Hosting, auth | Scan and automated test (C, D) |
| 21 | No dormant casual crew after 7 days | Crew expiry worker | Seeded expiry test (C) |
| 22 | South Africa North; 5-year purge of personal data | Region; retention job (stretch) | Config check (D) |
| 23 | Usable at 360 px on an older Android device | Crew screens | Device test (B) |
| 24 | Contrast 4.5:1; 44 px targets | All UI | axe and manual (B) |
| 25 | Current and previous Chrome, Safari, Edge, Firefox | SPA | Browser matrix (B) |
| 26 | Running locally in 30 min from the README | README, compose | Outsider trial (B, D) |
| 27 | Single deploy unit; configuration not code | Hosting | Clean deploy (D) |
| 28 | Masking tests gate production | CI | Required check (D, B) |
| 29 | Client self-administers users | Users | Handover walkthrough (C) |
| 30 | Cost within the T1 §9 projection | Azure tiers, worker sleep | Monthly review (D) |
| 31 | 15 users, 50 events a year, 10-year retention | Schema, indexes | Capacity check (C) |
| 32 | ZAR, SAST, day-month-year | Formatting | Locale review (B) |

## Appendix C: Client vocabulary

| Term | Meaning in Carbonate |
|---|---|
| Pack size / pax | Expected guest numbers; drives stock quantities (`PackSizeEstimated`, `PackSizeActual`) |
| Costing | A quote: lines with cost to us and price to client |
| Recce | A site visit before the event |
| Load-in | Delivering and setting up infrastructure and stock at the venue |
| Doors | When guests arrive and the event goes live |
| Strike | Breaking down the setup after the event |
| Load-out | Removing everything from the venue |
| Debrief | Post-event review; crew access expires 7 days after it |
| Reconciliation | Post-event analysis: what sold, wastage, profit to the client and to Carbon |
| Activation | A brand activation, often booked days ahead, sometimes the day before |
| CE / CLM | Carbon Events (bars and service) / Carbon Logistics Management (infrastructure, logistics) |

## Appendix D: Demo seed data (D)

Use the same data as the Task 1 prototype, so screenshots and the demo stay consistent. Every value below is fictional demo data.

- **Divisions:** CE (Carbon Events), CLM (Carbon Logistics Management).
- **Users:** one per role, plus extras for assignment.

  | Name | Role |
  |---|---|
  | Director | Director |
  | Sarah M. | Event Manager |
  | Ops Manager | Operations Manager |
  | Bookkeeper | Accounts |
  | Thabo N. | Crew Lead |
  | Priya R. | Casual Crew |

  Director and Accounts have TOTP enrolled with a documented demo secret. Passwords are documented in the README only for the demo environment.
- **Events.** Names, venues, pax and types follow the prototype. **Dates are computed relative to the seed time**, so each status matches its dates whenever the demo is run.

  | Code | Event | When (relative to seed) | Venue | Pax | Type | Status |
  |---|---|---|---|---|---|---|
  | NAI-WED-26 | Naidoo Wedding (confidential) | +3 weeks | Steenberg Estate | 180 | Wedding | ConfirmedInPlanning |
  | VAN-ACT-26 | Vantage Brand Activation | +4 days (short-notice booking) | V&A Waterfront | 600 | Activation | ConfirmedInPlanning |
  | MER-YE-26 | Meridian Year-End Function | +6 weeks | The Point Hotel | 240 | Corporate | ConfirmedInPlanning |
  | RIV-FEST-26 | Riverlight Festival | started 2 h ago, ends in 6 h | Riverside Grounds | 2,500 | Festival | InProgress |
  | DEL-GOLF-26 | Delacroix Corporate Golf Day | −2 weeks | Steenberg Golf Club | 90 | Corporate | Finished |
  | (any code) | One `Enquired` event | +8 weeks | — | — | — | Enquired: must not appear on the board |

  To show an automatic transition live in the presentation, re-run a small "demo reset" that moves one confirmed event's `StartsAt` to a few minutes ahead.
- **Admin tasks:**
  - "Renew liquor licence — Riverside venue" (High, Thabo N., Assigned)
  - "Update PPE stock list" (Priya R., Assigned)
  - "Quarterly ice machine service" (Thabo N., In Progress / Needs Review)
  - "Health & safety file — October audit" (Priya R., Complete)
- **Suppliers:** Coastal Ice Co. (lead time 5 days), Vine & Co. Distributors (lead time 2 days, liquor supplier).
- **Stock:**
  - Cups 500 ml (Stock)
  - Ice, bulk, per kg (Order, Coastal Ice)
  - Spirits, mixed (Order, Vine & Co.)
  - Mobile bar unit (Rent)
  - Glassware categories
  - Ice machine as a serialised asset

  Plan Vantage Brand Activation's ice **below** expected consumption, so FR-27 shows. Its load-in falls inside Coastal Ice's 5-day lead time, so FR-29 shows too.
- **Quote categories** (T1 App. B5): Set up & strike, Infrastructure, Transportation, Crew, Ice, Stock, Bar kit, Glassware, Other. Staff rate card roles: bartender, waiter, manager, bar support.
- **Default quantities per 100 guests.** Use Carbon's real figures from the Head of Logistics and the Bookkeeper. Do not guess; mark any placeholder clearly in the seed file.
