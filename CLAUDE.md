# Carbonate — instructions for coding agents

@docs/PROJECT_PLAN.md

The project plan above is the source of truth. Read sections 0, 2, 5, 7 and 14 first, then the
section 8 subsection for the module you were given. If the plan and this file disagree, the plan wins.

## Before you start

The human will tell you which module and which FR ids you are working on. If they haven't, ask.
Do not pick your own scope.

## Order of authority

1. `docs/PROJECT_PLAN.md`
2. Task 1 section 7.6 (RBAC) and 2.3 (functional requirements)
3. Other parts of the Task 1 document
4. The prototype

If all four are silent, **do not invent behaviour**. Write `// TODO(plan): <question>`, mention it in
the PR description, and carry on with the rest.

## Hard rules

These come from section 14 of the plan. Breaking any of them fails code review.

1. **Stay in your module.** Edit only the folders for the module you were given. Changing anything
   under `Platform/`, `Common/`, a DTO another module consumes, a permission code or the schema needs
   the owner's approval — make the smallest possible change and flag it in the PR description.
2. **Never weaken the security model.** Do not add `[AllowAnonymous]`, skip a permission attribute or
   service-layer check, return an entity instead of a DTO, add an untagged money field, store tokens in
   browser storage, log personal data, or use `innerHTML` / `dangerouslySetInnerHTML` outside
   `SanitisedDescription.tsx`. If a task seems to need one of these, stop and ask.
3. **Tests come with the code.** Business rules get unit tests; endpoints get integration tests covering
   happy path, 400, 403 and 404. Work is not done without them.
4. **Prefer the boring option.** This is a 15-user internal system. No message bus, CQRS framework,
   microservice, Redis cache or GraphQL. Note any new package in the PR with a reason.
5. **Secrets never touch the repo.** User-secrets locally, Key Vault in Azure. Not in code, config,
   tests or docs.
6. **Migrations.** Never edit a merged migration. Never run destructive SQL against a shared Azure
   database — use the local Docker SQL container for experiments.

## Conventions you must follow

- Routes `/api/…`, plural nouns, kebab-case. State transitions are `POST /api/<resource>/{id}/<verb>`.
- JSON camelCase; enums as strings; **money fields omitted, not null, when masked**.
- Times stored UTC as `datetime2`, sent ISO 8601 with `Z`. Calendar dates are `DateOnly`.
- Money is `decimal(18,2)` ZAR. VAT comes from `Finance:VatRate` config, never hard-coded.
- GUID primary keys generated in application code. `AUDIT_ENTRY.AuditId` is the one `bigint` identity.
- Errors are RFC 7807 problem details: 400 validation, 401 unauthenticated, 403 no permission,
  404 not found **or not visible**, 409 concurrency or invalid transition, 422 business-rule violation.
- Updatable aggregates carry `rowVersion` (base64) in their DTOs; a mismatch is 409.

## Client vocabulary

Use the client's words in domain names and UI copy: **pack size, costing, recce, load-in, doors,
strike, load-out, debrief, reconciliation, activation**. Never "sprint", "project" or "ticket".

## Commits and PRs

- Branch `feature/FR-xx-short-name`.
- Commit messages start with the requirement id: `FR-28: consolidate order lines by supplier`.
- Small PRs into `dev`, one reviewer.
- The human commits under their own GitHub account. Do not amend or squash other people's commits.

---

## Module ownership (who owns what)

**The team is three people.** Jack Evans (role A) is out for Task 2; his work was redistributed on
3 Oct 2026. There is no role A — do not assign work to it.

| Module | Folders | Owner |
|---|---|---|
| Platform: auth, users, roles, masking, audit | `Platform/Auth`, `Platform/Users`, `Platform/Audit` | C — Ethan |
| Events, milestones, crew | `Features/Events` | C — Ethan |
| Commercial: costings, confirmation, invoices | `Features/Commercial` | C — Ethan |
| Boards and tasks | `Features/Boards`, `web/` | B — Christiaan |
| Venues and site visits | `Features/Venues` | B — Christiaan |
| The whole SPA | `web/` | B — Christiaan |
| **Event lifecycle (FR-02)** | `Domain/Lifecycle`, `Features/Lifecycle` | **D — Ulrich** |
| **Stock, templates, order lists (FR-24–30)** | `Features/Stock` | **D — Ulrich** |
| **Incidents and file storage (FR-31)** | `Features/Incidents`, `Platform/Files` | **D — Ulrich** |
| **Google Calendar sync (FR-40–42)** | `Features/Calendar` | **D — Ulrich** |
| **Seed and demo data (Appendix D)** | `Carbonate.Infrastructure` seeding | **D — Ulrich** |
| Repo, CI/CD, hosting | `.github/`, `infra/` | **D — Ulrich** |

## Notes for D's modules specifically

**Build order in Phase 2 matters.** D's phase is the heaviest: build `IFileStorage` first (FR-06 and
FR-22 depend on it), then the **template seeder (FR-25)**, which is on the critical path because C's
event-create endpoint calls `IEventTemplateSeeder.SeedAsync` inside its transaction. Stock, lifecycle
and incidents follow.

**Stock, templates and order lists (FR-24–30).** `IEventTemplateSeeder.SeedAsync(eventId, eventType,
divisionId, packSize)` picks the active template for the division, `BoardType='Event'` and event type,
creates the board columns and cards (each card's `DueAt` is the matching milestone time plus
`offsetHours`), and creates one stock requirement per `defaultStockRequirements` entry with
`QuantityRequired = ceil(quantityPerHundredGuests × packSize / 100)`.

Shortfall (FR-27): `expected = ConsumptionPerHundredGuests × (PackSizeActual ?? PackSizeEstimated) / 100`;
warn when `QuantityRequired < expected`, while LoadIn is still in the future.

Order list (FR-28): requirements of `ConfirmedInPlanning` or `InProgress` events, `RequiredByDate` in
range, `SourceMode` of `Order` or `Rent`, grouped by `StockItem.DefaultSupplierId`. Items with no
supplier become an "Unassigned supplier" warning, not a list. **Must run under 5 s for a seeded
December (NFR-08)** — set-based EF queries, no N+1.

`StandardUnitCost` and `EstimatedUnitCost` are `$cost` fields: tag them, mask them, add masking-suite
rows, and get C's review.

**Lifecycle (FR-02).** The state machine lives in `Domain/Lifecycle`, one class per state implementing
`IEventState`, each owning its allowed transitions. It has no database access and no knowledge of
calendars or notifications. `EventTransitionWorker` is a `BackgroundService` that computes the next due
`StartsAt`/`EndsAt` and **sleeps until then, capped at 30 minutes** — never a short polling loop,
because that keeps the serverless database awake and breaks the cost model. Every transition publishes
`EventStateChanged`; observers (`CalendarSyncObserver`, `AuditObserver`, `NotificationObserver`)
subscribe.

**File storage (FR-31).** `IFileStorage` in `Platform/Files` is shared — FR-06 and FR-22 depend on it,
so build it first. It validates type (jpeg, png, webp, heic, pdf) and size (10 MB cap), computes the
SHA-256 hash, and serves through short-lived SAS links with `Content-Disposition: attachment`. Never a
public container.

**Calendar (FR-40–42).** Outbound only — insert and update calls, never reads. The refresh token goes
to Key Vault; the database holds only `TokenSecretName`. Observers write `CALENDAR_OUTBOX` rows and
`CalendarSyncWorker` drains them with exponential backoff, checking `CALENDAR_LINK` first so an update
targets the existing Google event rather than creating a duplicate. Payloads carry **no client names,
contact details or money values**; confidential events push a redacted title.

**Health endpoint.** `/health` must not query the database. The availability test pings it every five
minutes and a query there would stop the serverless database auto-pausing.
