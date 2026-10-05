# Traceability

Requirement → API → screen → tests → status. One row per functional requirement from Appendix A of
the project plan.

**Owner: B (Christiaan).** D scaffolded this file and filled the rows for the modules D owns
(lifecycle, stock, incidents, calendar). **B and C: please fill your own rows.** Anything marked
`TBC` is waiting on its owner.

**Status words.** *Done* means merged into `dev`, tested and deployed. *API only* means the endpoints
work and are tested but no screen reaches them yet. *Partial* means some of the requirement is built
and the gap is named. *Not built* means exactly that, with a reason.

**Screens.** B filled the Screen column on every row, and added the front-end tests to the Tests
column. Where a screen now reaches an *API only* row, or found a gap behind a *Done*, there is a note
after the status in italics. The status itself is left for its owner to change.

---

## Functional requirements

| ID | Requirement | Owner | API | Screen | Tests | Status |
|---|---|---|---|---|---|---|
| FR-01 | Record an event with all booking fields | C | `POST /api/events` | Events board → New event; event page → Edit | `EventsEndpointTests`, `EventFormPage.test.tsx`, `eventForm.test.ts` | **Done** · *B: the form can't save on the real API yet, because it needs `GET /api/clients` and `GET /api/divisions` (C)* |
| FR-02 | Stages, last two automatic, Cancelled only from ConfirmedInPlanning | **D** | `POST /api/events/{id}/transitions`, `GET /api/events/{id}/allowed-transitions` | Events board, drag between columns | `EventStateMachineTests` (45), `EventTransitionWorkerTests`, `EventsEndpointTests` | **Done** |
| FR-03 | Enquired events excluded from the events board | C | `GET /api/events?board=true` | Events board | `EventsEndpointTests` | **Done** |
| FR-04 | Milestones with dependencies and lag; moving one cascades | C | `POST /api/events/{id}/milestones/{id}/reschedule` | Event page → Milestones → Reschedule | `ScheduleCalculatorTests`, `MilestoneChainTests`, `EventDetailPage.test.tsx` | **API only** — cascade built and tested, no screen reschedules a milestone · *B: screen now built* |
| FR-05 | Intra-day service schedule (JSON) | C | `PUT /api/events/{id}/service-schedule` | Event page → Service schedule, read only | `serviceSchedule.test.ts` | **Not built** — endpoint answers 501; a Should, cut for time · *B: the event page shows a saved schedule but can't edit it* |
| FR-06 | Event documents; confidential flag and banner | C | `GET\|POST /api/events/{id}/documents` | Confidential banner on the event page; no documents screen | TBC | **Not built** — endpoint answers 501; a Should, cut for time |
| FR-07 | Assign named crew to an event | C | `GET\|POST /api/events/{id}/crew` | Event page → Crew → Assign crew; crew see it under My events | `EventsEndpointTests`, `EventDetailPage.test.tsx`, `MyEventsPage.test.tsx` | **API only** · *B: screen now built. The Event Manager holds `crew.assign` but can't load the people list, which needs `user.manage` (C)* |
| FR-08 | Pack size estimate and actual | C | `PATCH /api/events/{id}/pack-size` | Pack size shown on the event page, not edited | TBC | **Not built** — endpoint answers 501; a Should, cut for time |
| FR-09 | Indefinite retention; compare costs with previous events | C | `GET /api/events/{id}/cost-history` | Quotes & Invoices → Start a costing → copy from an earlier event | `CommercialEndpointTests`, `FinancePage.test.tsx` | **API only** · *B: screen now built* |
| FR-10 | Reject conflicting concurrent edits | C | `rowVersion` on every updatable aggregate | A "changed by someone else" message with a reload, on card moves, event edits, admin tasks and costings | `EventsEndpointTests`, `EventMoves.test.tsx`, `EventFormPage.test.tsx`, `AdminTaskDetailPage.test.tsx`, `EventTaskBoardPage.test.tsx`, `QuotePage.test.tsx` | **API only** — stale `rowVersion` returns 409 with the current masked values · *B: screens now built* |
| FR-11 | Costing lines with cost and price; target margin band | C | `GET\|POST\|PUT /api/events/{id}/quotes` | Quotes & Invoices → costing page | `QuoteCalculatorTests`, `CommercialEndpointTests`, `QuotePage.test.tsx` | **API only** · *B: screen now built* |
| FR-12 | Client confirmation by PO or deposit | C | `POST /api/events/{id}/confirmation` | Event page → Record PO or deposit | `CommercialEndpointTests`, `EventDetailPage.test.tsx` | **API only** · *B: screen now built* |
| FR-13 | Invoice carries the confirmation reference | C | `POST /api/events/{id}/invoices` | Quotes & Invoices → Invoices, listed and marked paid | `CommercialEndpointTests`, `FinancePage.test.tsx` | **API only** · *B: invoices list and mark paid on screen, but raising one can't, because nothing the screens read carries the confirmation id (C)* |
| FR-14 | Cost and margin restricted; Director approval above a threshold | C | masking filter + `POST /api/quotes/{id}/approve` | Costing page → Approve; money columns and rows appear only when the API sends them | `FinancialMaskerTests`, `FinancialMaskingFilterTests`, `CommercialEndpointTests`, `QuotePage.test.tsx`, `MaskingTests` | **API only** — masking by tier and Director approval above the threshold, which is a placeholder pending the client (D-011) · *B: screen now built* |
| FR-15 | Copy a previous costing | C | `POST /api/events/{id}/quotes/copy` | Quotes & Invoices → Start a costing → copy | `CommercialEndpointTests`, `FinancePage.test.tsx` | **API only** · *B: screen now built* |
| FR-16 | List delivered but uninvoiced events | C | `GET /api/invoices/uninvoiced-events` | none | none (not built) | **Not built** — endpoint answers 501; a Should, cut for time |
| FR-17 | Post-event reconciliation | C | `GET\|PUT /api/events/{id}/reconciliation` | none | none (not built) | **Not built** — endpoint answers 501; a Should, cut for time |
| FR-18 | Events board, 3 active stages, manual and automatic moves | B | `GET /api/events?board=true`, `POST /api/events/{id}/transitions` | Events board: drag, or a card's "Move to…" menu | `EventsBoardPage.test.tsx`, `EventMoves.test.tsx`, `moves.test.ts`, `filters.test.ts` | **Done** |
| FR-19 | Admin tasks board, 3 stages | B | `GET /api/boards/admin`, `POST /api/boards/{id}/cards`, `POST /api/cards/{id}/move` | Admin Tasks board and task page | `BoardEndpointTests`, `CardPositionerTests`, `AdminTasksPage.test.tsx`, `AdminTaskDetailPage.test.tsx` | **Done** |
| FR-20 | Only the assigning manager completes or returns, with notes | B | `POST /api/cards/{id}/complete`, `/return` | Admin task page → Manager review | `AdminTaskRulesTests`, `AdminReviewEndpointTests`, `rules.test.ts`, `AdminTaskDetailPage.test.tsx` | **Done** |
| FR-21 | Assign cards; crew see only their own | B | `PUT /api/cards/{id}/assignees`, `GET /api/events/{id}/board` | Admin Tasks, event task board, My tasks | `CardEditEndpointTests`, `EventTaskBoardPage.test.tsx`, `EventCardDetailPage.test.tsx`, `MyTasksPage.test.tsx` | **Done** |
| FR-22 | Files and photos on cards | B | `GET\|POST /api/cards/{id}/attachments` | none | none (not built) | **Not built**: both endpoints answer 501; a Should, left for time after the Musts |
| FR-23 | Display WIP limits | B | `wipLimit` on `BoardColumnDto` | Event task board column headers; the move dialog says when a column is at its limit | `KanbanColumn.test.tsx`, `EventTaskBoardPage.test.tsx` | **Done** |
| FR-24 | Stock catalogue by category and division | **D** | `GET /api/stock/categories`, `GET\|POST\|PUT /api/stock/items`, `/api/suppliers`, `/api/equipment`, `GET /api/checklist-templates` | Stock & Orders → Catalogue | `StockEndpointTests`, `CataloguePage.test.tsx` | **API only** — screen pending (B) · *B: screen now built* |
| FR-25 | Seed board and stock from a template, per 100 guests | **D** | `IEventTemplateSeeder`, called inside `POST /api/events` | Event page → Task board, and its stock list | `TemplateMathTests` (11), `EventTemplateSeederTests` (6) | **Done** |
| FR-26 | Event stock requirement by item, quantity and source mode | **D** | `GET\|PUT /api/events/{id}/stock-requirements` | Stock & Orders → Stock requirements; event page → Stock | `StockEndpointTests`, `StockPage.test.tsx` | **API only** · *B: screen now built* |
| FR-27 | Shortfall warning against expected consumption | **D** | `warnings[]` on each requirement line, code `SHORTFALL` | Stock & Orders → Stock requirements, beside the line | `StockRulesTests`, `StockEndpointTests`, `StockPage.test.tsx` | **API only** · *B: screen now built* |
| FR-28 | Consolidated order list over a period | **D** | `POST /api/order-lists/generate`, `GET /api/order-lists` | Stock & Orders → Order lists → Generate | `OrderListBuilderTests`, `StockEndpointTests`, `OrderListPerformanceTests`, `StockPage.test.tsx` | **API only** · *B: screen now built* |
| FR-29 | Supplier lead-time warning | **D** | `warnings[]`, code `LEAD_TIME` | Stock & Orders → Stock requirements, beside the line | `StockRulesTests`, `StockEndpointTests`, `StockPage.test.tsx` | **API only** · *B: screen now built* |
| FR-30 | Order list approved by a different user | **D** | `POST /api/order-lists/{id}/submit`, `/approve`, `/mark-placed` | Order list page; Quotes & Invoices → Order list approvals | `OrderListBuilderTests`, `StockEndpointTests`, `StockPage.test.tsx`, `FinancePage.test.tsx` | **API only** · *B: screen now built* |
| FR-31 | Incident reports, asset link, photo | **D** | `POST\|GET /api/events/{id}/incidents`, `GET /api/incidents`, `PATCH /api/incidents/{id}` | Crew → Report; event page → Incidents | `FileValidationTests` (16), `IncidentEndpointTests` (13), `ReportPage.test.tsx`, `EventDetailPage.test.tsx` | **API only** · *B: screen now built. Casual Crew can't load the item and equipment lists, which need `stock.view`, so they can't report yet (D)* |
| FR-32 | Persistent venue records | B | `GET\|POST\|PUT /api/venues` | Settings → Venues; event page → Venue | `VenueEndpointTests`, `SettingsPage.test.tsx`, `VenuePanel.test.tsx` | **Done** |
| FR-33 | Site visit details | B | `GET\|POST /api/events/{id}/site-visits`, `PUT /api/site-visits/{id}` | Event page → Venue → Add recce | `SiteVisitEndpointTests`, `VenuePanel.test.tsx` | **Done** |
| FR-34 | Authentication, multiple roles, MFA for Director and Accounts | C | `POST /api/auth/login`, `/mfa/verify`, `/mfa/enrol`, `/refresh` | Log in, MFA | `AuthServiceTests`, `ApiSecurityTests`, `LoginPage.test.tsx`, `Mfa.test.tsx` | **Done** |
| FR-35 | Server-side financial tiers | C | `IFinancialMasker` + global filter | All money fields; a column or row only appears when the API sends it | `FinancialMaskerTests`, `MaskingTests` (masking suite), `QuotePage.test.tsx` | **API only** — enforced on every response, so the screens only show what they are sent |
| FR-36 | Crew scoped to assigned events; deactivated after debrief | C | `CrewAccountExpiry` worker | My events and the crew event page | `CrewExpiryTests`, `CrewExpiryServiceTests`, `MyEventsPage.test.tsx` | **API only** · *B: screen now built. The users list doesn't say when a casual crew member's access runs out (C)* |
| FR-37 | Immutable audit trail viewable by the Director | C | `GET /api/audit` | Settings → Audit log | `UsersEndpointTests`, `SchemaTests` (insert-only trigger), `SettingsPage.test.tsx`, `auditChanges.test.ts` | **API only** · *B: screen now built* |
| FR-38 | Director and Ops manage users and roles | C | `GET\|POST /api/users`, `PATCH /api/users/{id}` | Settings → Users | `UsersEndpointTests`, `LastDirectorTests`, `SettingsPage.test.tsx` | **API only** · *B: screen now built* |
| FR-39 | Native month/week calendar view | B | none | Calendar (placeholder) | none (not built) | **Not built**: only if time, after the Musts and the in-scope Shoulds |
| FR-40 | Push dates and milestones to Google; outbound only | **D** | `CALENDAR_OUTBOX` → `CalendarSyncWorker`; `POST /api/calendar/connect`, `GET /api/calendar/oauth/callback` | Settings → Google Calendar, which says it isn't switched on yet | `CalendarBackoffTests`, `CalendarStateTokenTests` (11), `SettingsPage.test.tsx` | **Partial** — outbox, retry and the signed OAuth `state` are built and tested; the Google connect flow is not (needs a Google Cloud project; D-010) |
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

## Non-functional requirements verified by B

| NFR | Target | How it is verified | Status |
|---|---|---|---|
| NFR-17 | Masked values absent from the response | `MaskingTests`: each of the six roles signs in through the real login and calls every endpoint that carries money. A key the role isn't allowed must be missing from the raw JSON, not sent as null, and the staff rate shows only on your own row. A reflection test fails if a new money field isn't on the suite's list | **Done** |
| NFR-28 | Masking tests gate production | The suite is tagged `Category=Masking`, which the required "Masking acceptance suite" job in `ci.yml` runs | **Done** |
| NFR-15 | Never silently overwrite | A 409 shows a "changed by someone else" message with a reload, never a quiet save. The tests are listed under FR-10 | **Done** (screens) |
| NFR-23 | Usable at 360 px | Crew screens checked at 360 px against the local API with the demo data on 5 Oct: nothing scrolls sideways | **Partial**: not yet tried on a real Android phone |
| NFR-24 | Contrast 4.5:1, 44 px targets | axe runs in the component tests, but it can't measure contrast or target size there | **Partial**: the manual audit isn't done |
| NFR-01, 03, 04, 06, 07, 25 | Timed and measured runs | NFR-03 also waits on the clients and divisions lookups, since it times creating an event | **Not run yet** |

---

## Known gaps, with reasons

| Gap | Why | Owner |
|---|---|---|
| Client and division lookups (FR-01) | `GET /api/clients` and `GET /api/divisions` don't exist yet, so New event and Edit event can't save on the real API. The form asks for `{ clientId, name }` and `{ divisionId, code, name }` | C |
| The Event Manager can't pick crew (FR-07) | They hold `crew.assign`, but `GET /api/users` needs `user.manage` | C |
| Raising an invoice from the screens (FR-13) | It needs the event's confirmation id, and no response the screens read carries it | C |
| Casual Crew can't report an incident (FR-31) | The item and equipment lists need `stock.view`, which Casual Crew doesn't hold | D |
| No crew on any demo event | The demo data gives Thabo and Priya admin tasks but no crew assignment, so My events is empty for both until someone assigns them | D |
| Google OAuth connect flow (FR-40) | Needs a Google Cloud project and consent screen, and refresh tokens expire after 7 days in Testing mode. Everything behind it — outbox, deduplication, redaction, signed `state` — is built and tested (D-010) | D |
| Card attachments (FR-22) and the native calendar (FR-39) | Only if time, after the Musts and the in-scope Shoulds | B |
| ZAP scan | Not run | D |
| Accessibility audit | axe runs in the component tests; the manual keyboard, screen reader and contrast pass isn't done | B |
