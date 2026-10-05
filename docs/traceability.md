# Traceability

Requirement → API → screen → tests → status. One row per functional requirement from Appendix A of
the project plan.

**Owner: B (Christiaan).** D scaffolded this file and filled the rows for the modules D owns
(lifecycle, stock, incidents, calendar). **B and C: please fill your own rows.** Anything marked
`TBC` is waiting on its owner.

**Status words.** *Done* means merged into `dev`, tested and deployed. *API only* means the endpoints
work and are tested but no screen reaches them yet. *Partial* means some of the requirement is built
and the gap is named. *Not built* means exactly that, with a reason.

---

## Functional requirements

| ID | Requirement | Owner | API | Screen | Tests | Status |
|---|---|---|---|---|---|---|
| FR-01 | Record an event with all booking fields | C | `POST /api/events` | Events board → New event | `EventsEndpointTests` | **Done** |
| FR-02 | Stages, last two automatic, Cancelled only from ConfirmedInPlanning | **D** | `POST /api/events/{id}/transitions`, `GET /api/events/{id}/allowed-transitions` | Events board, drag between columns | `EventStateMachineTests` (45), `EventTransitionWorkerTests`, `EventsEndpointTests` | **Done** |
| FR-03 | Enquired events excluded from the events board | C | `GET /api/events?board=true` | Events board | `EventsEndpointTests` | **Done** |
| FR-04 | Milestones with dependencies and lag; moving one cascades | C | `POST /api/events/{id}/milestones/{id}/reschedule` | TBC | `ScheduleCalculatorTests`, `MilestoneChainTests` | **API only** — cascade built and tested, no screen reschedules a milestone |
| FR-05 | Intra-day service schedule (JSON) | C | `PUT /api/events/{id}/service-schedule` | TBC | TBC | **Not built** — endpoint answers 501; a Should, cut for time |
| FR-06 | Event documents; confidential flag and banner | C | `GET\|POST /api/events/{id}/documents` | TBC | TBC | **Not built** — endpoint answers 501; a Should, cut for time |
| FR-07 | Assign named crew to an event | C | `GET\|POST /api/events/{id}/crew` | TBC | `EventsEndpointTests` | **API only** |
| FR-08 | Pack size estimate and actual | C | `PATCH /api/events/{id}/pack-size` | TBC | TBC | **Not built** — endpoint answers 501; a Should, cut for time |
| FR-09 | Indefinite retention; compare costs with previous events | C | `GET /api/events/{id}/cost-history` | TBC | `CommercialEndpointTests` | **API only** |
| FR-10 | Reject conflicting concurrent edits | C | `rowVersion` on every updatable aggregate | TBC | `EventsEndpointTests` | **API only** — stale `rowVersion` returns 409 with the current masked values |
| FR-11 | Costing lines with cost and price; target margin band | C | `GET\|POST\|PUT /api/events/{id}/quotes` | TBC | `QuoteCalculatorTests`, `CommercialEndpointTests` | **API only** |
| FR-12 | Client confirmation by PO or deposit | C | `POST /api/events/{id}/confirmation` | TBC | `CommercialEndpointTests` | **API only** |
| FR-13 | Invoice carries the confirmation reference | C | `POST /api/events/{id}/invoices` | TBC | `CommercialEndpointTests` | **API only** |
| FR-14 | Cost and margin restricted; Director approval above a threshold | C | masking filter + `POST /api/quotes/{id}/approve` | TBC | `FinancialMaskerTests`, `FinancialMaskingFilterTests`, `CommercialEndpointTests` | **API only** — masking by tier and Director approval above the threshold, which is a placeholder pending the client (D-011) |
| FR-15 | Copy a previous costing | C | `POST /api/events/{id}/quotes/copy` | TBC | `CommercialEndpointTests` | **API only** |
| FR-16 | List delivered but uninvoiced events | C | `GET /api/invoices/uninvoiced-events` | TBC | none (not built) | **Not built** — endpoint answers 501; a Should, cut for time |
| FR-17 | Post-event reconciliation | C | `GET\|PUT /api/events/{id}/reconciliation` | TBC | none (not built) | **Not built** — endpoint answers 501; a Should, cut for time |
| FR-18 | Events board, 3 active stages, manual and automatic moves | B | `GET /api/events?board=true` | Events Board | `EventsBoardPage.test.tsx`, `EventMoves.test.tsx` | TBC |
| FR-19 | Admin tasks board, 3 stages | B | `GET /api/boards/admin` | Admin Tasks | `AdminTasksPage.test.tsx`, `BoardEndpointTests` | TBC |
| FR-20 | Only the assigning manager completes or returns, with notes | B | `POST /api/cards/{id}/complete`, `/return` | Admin Tasks | `AdminTaskRulesTests`, `AdminReviewEndpointTests` | TBC |
| FR-21 | Assign cards; crew see only their own | B | `PUT /api/cards/{id}/assignees` | Admin Tasks | `CardEditEndpointTests` | TBC |
| FR-22 | Files and photos on cards | B | `GET\|POST /api/cards/{id}/attachments` | TBC | TBC | TBC |
| FR-23 | Display WIP limits | B | `wipLimit` on `BoardColumnDto` | Kanban column | `KanbanColumn.test.tsx` | TBC |
| FR-24 | Stock catalogue by category and division | **D** | `GET /api/stock/categories`, `GET\|POST\|PUT /api/stock/items`, `/api/suppliers`, `/api/equipment`, `GET /api/checklist-templates` | Stock & Orders (placeholder) | `StockEndpointTests` | **API only** — screen pending (B) |
| FR-25 | Seed board and stock from a template, per 100 guests | **D** | `IEventTemplateSeeder`, called inside `POST /api/events` | Event board appears on create | `TemplateMathTests` (11), `EventTemplateSeederTests` (6) | **Done** |
| FR-26 | Event stock requirement by item, quantity and source mode | **D** | `GET\|PUT /api/events/{id}/stock-requirements` | Stock & Orders (placeholder) | `StockEndpointTests` | **API only** |
| FR-27 | Shortfall warning against expected consumption | **D** | `warnings[]` on each requirement line, code `SHORTFALL` | Stock & Orders (placeholder) | `StockRulesTests`, `StockEndpointTests` | **API only** |
| FR-28 | Consolidated order list over a period | **D** | `POST /api/order-lists/generate`, `GET /api/order-lists` | Stock & Orders (placeholder) | `OrderListBuilderTests`, `StockEndpointTests`, `OrderListPerformanceTests` | **API only** |
| FR-29 | Supplier lead-time warning | **D** | `warnings[]`, code `LEAD_TIME` | Stock & Orders (placeholder) | `StockRulesTests`, `StockEndpointTests` | **API only** |
| FR-30 | Order list approved by a different user | **D** | `POST /api/order-lists/{id}/submit`, `/approve`, `/mark-placed` | Stock & Orders (placeholder) | `OrderListBuilderTests`, `StockEndpointTests` | **API only** |
| FR-31 | Incident reports, asset link, photo | **D** | `POST\|GET /api/events/{id}/incidents`, `GET /api/incidents`, `PATCH /api/incidents/{id}` | Crew → Report (placeholder) | `FileValidationTests` (16), `IncidentEndpointTests` (13) | **API only** |
| FR-32 | Persistent venue records | B | `GET\|POST\|PUT /api/venues` | TBC | `VenueEndpointTests` | TBC |
| FR-33 | Site visit details | B | `GET\|POST /api/events/{id}/site-visits` | TBC | `SiteVisitEndpointTests` | TBC |
| FR-34 | Authentication, multiple roles, MFA for Director and Accounts | C | `POST /api/auth/login`, `/mfa/verify`, `/mfa/enrol`, `/refresh` | Log in, MFA | `AuthServiceTests`, `ApiSecurityTests`, `LoginPage.test.tsx`, `Mfa.test.tsx` | **Done** |
| FR-35 | Server-side financial tiers | C | `IFinancialMasker` + global filter | All money fields | `FinancialMaskerTests`, masking suite | **API only** — enforced on every response, so the screens only show what they are sent |
| FR-36 | Crew scoped to assigned events; deactivated after debrief | C | `CrewAccountExpiry` worker | TBC | `CrewExpiryTests`, `CrewExpiryServiceTests` | **API only** |
| FR-37 | Immutable audit trail viewable by the Director | C | `GET /api/audit` | TBC | `UsersEndpointTests`, `SchemaTests` (insert-only trigger) | **API only** |
| FR-38 | Director and Ops manage users and roles | C | `GET\|POST /api/users`, `PATCH /api/users/{id}` | TBC | `UsersEndpointTests`, `LastDirectorTests` | **API only** |
| FR-39 | Native month/week calendar view | B | — | Calendar (placeholder) | TBC | TBC |
| FR-40 | Push dates and milestones to Google; outbound only | **D** | `CALENDAR_OUTBOX` → `CalendarSyncWorker`; `POST /api/calendar/connect`, `GET /api/calendar/oauth/callback` | Calendar (placeholder) | `CalendarBackoffTests`, `CalendarStateTokenTests` (11) | **Partial** — outbox, retry and the signed OAuth `state` are built and tested; the Google connect flow is not (needs a Google Cloud project; D-010) |
| FR-41 | Update the existing entry, never duplicate; queue and retry | **D** | `CalendarSyncWorker` checks `CALENDAR_LINK` before every push | — | `CalendarBackoffTests` | **Partial** — logic built and tested, not exercised against Google |
| FR-42 | No client names, contacts or money in entries; redacted confidential titles | **D** | `CalendarPayloadBuilder` | — | `CalendarPayloadTests` (9) | **Done** — payload rules complete and tested |

---

## Non-functional requirements verified by D

| NFR | Target | How it is verified | Status |
|---|---|---|---|
| NFR-08 | Order list generation under 5 s for a seeded December | `OrderListPerformanceTests` — 12 events, ~480 requirement lines, timed | **Done**. Found a real defect on its first run: the audit entry wrote every list id into `AUDIT_ENTRY.EntityId` (`nvarchar(64)`), which overflows at realistic volume |
| NFR-12 | A user request never waits on Google | Observers write an outbox row and return; `CalendarSyncWorker` drains it out of band | **Done** |
| NFR-13 / 14 | RPO 24 h, RTO 4 h | Timed point-in-time restore, 3 Oct — 18 minutes (`hosting.md` §4) | **Done** |
| NFR-22 | Data residency, South Africa | Every resource in South Africa North | **Done** |
| NFR-26 | A new developer runs it in 30 minutes | README "Running it locally" | **Done** |
| NFR-11 | Release windows | Stated in the README and `hosting.md` | **Done** (policy, not enforced in code) |

---

## Known gaps, with reasons

| Gap | Why | Owner |
|---|---|---|
| Stock, order list and incident **screens** | The APIs are built and tested; the SPA reached events and admin tasks first | B |
| Google OAuth connect flow (FR-40) | Needs a Google Cloud project and consent screen, and refresh tokens expire after 7 days in Testing mode. Everything behind it — outbox, deduplication, redaction, signed `state` — is built and tested (D-010) | D |
| ZAP scan | Not run | D |
| Accessibility audit | Not run | B |
