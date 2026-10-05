# Seed data

Owner: D (Ulrich Bezuidenhout) · Plan Appendix D

Carbonate seeds in two layers, and the difference matters.

| Layer | What it is | Where it runs | Code |
|---|---|---|---|
| **Platform** | Roles, permissions, the role–permission matrix, divisions, the admin board | Every environment | `Seeding/PlatformSeeder.cs` (C) |
| **Templates** | The checklist templates FR-25 seeds new events from | Every environment | `Seeding/TemplateSeeder.cs` (D) |
| **Demo** | Users, clients, venues, suppliers, stock catalogue, the six demo events | Dev only | `Seeding/DemoDataSeeder.cs` (D) |

All three add only what is missing, so running them twice changes nothing and an edit made in the UI
is never overwritten. Turn the lot off with `Seeding:Enabled=false`.

**Demo data never runs in production.** The demo seeder is gated on `Seeding:Demo=true`, which is set
on `carbonate-api-dev` and not on `carbonate-api-prod`. Demo passwords live in the README for the demo
environment only, and have to be removed before any real client data is loaded.

**The demo password is `Carbonate-Demo-2026!` for every seeded account.** That is acceptable precisely
because the data is fictional and the environment is disposable; it would not be acceptable anywhere
near real client data.

**Boards and stock requirements are not written by the demo seeder.** It creates the events and then
calls `IEventTemplateSeeder.SeedAsync` — the same path an event create takes — so the demo exercises
FR-25 rather than faking its output. If the seeder breaks, the demo data notices.

---

## ⚠ Placeholder figures

Appendix D says: *"Use Carbon's real figures from the Head of Logistics and the Bookkeeper. Do not
guess; mark any placeholder clearly in the seed file."*

**Every `quantityPerHundredGuests` value currently in `TemplateSeeder.cs` is a placeholder.** They are
marked with `// TODO(plan)` in the code and listed here so they are easy to replace in one pass:

| Template | SKU | Placeholder per 100 guests | Unit |
|---|---|---|---|
| Standard event run sheet | `CUP-500` | 220 | cups |
| Standard event run sheet | `ICE-BULK-KG` | 45 | kg |
| Standard event run sheet | `SPIRIT-MIX` | 8 | bottles |
| Standard event run sheet | `GLASS-WINE` | 130 | glasses |
| Wedding run sheet | `GLASS-WINE` | 180 | glasses |
| Wedding run sheet | `GLASS-CHAMP` | 110 | glasses |
| Wedding run sheet | `ICE-BULK-KG` | 35 | kg |
| Wedding run sheet | `SPIRIT-MIX` | 6 | bottles |
| Logistics run sheet | `BAR-MOBILE` | 1 | unit |

**Action:** B to get the real numbers from the Head of Logistics and the Bookkeeper, then D replaces
them. Until that happens, the shortfall warning (FR-27) demonstrates correctly but the numbers it
warns about are not the client's.

Unit costs (`StandardUnitCost`) are also placeholders. They are `$cost` fields, so they are masked for
most roles anyway, but a plausible figure is better than a wrong one in front of the client.

---

## Divisions

| Code | Name |
|---|---|
| `CE` | Carbon Events |
| `CLM` | Carbon Logistics Management |

Seeded by `PlatformSeeder`. The template seeder hangs off these, so it runs after.

## Users

One per role, plus extras for assignment. All fictional.

| Name | Role | Notes |
|---|---|---|
| Director | Director | TOTP enrolled |
| Sarah M. | Event Manager | |
| Ops Manager | Operations Manager | |
| Bookkeeper | Accounts | TOTP enrolled |
| Thabo N. | Crew Lead | |
| Priya R. | Casual Crew | |

Director and Accounts have MFA enrolled with a documented demo secret. The secret and the passwords
go in the README under the demo environment, and nowhere else — not in the repo's code, config or
tests (plan section 14, rule 5).

## Events

**Dates are computed relative to the seed time**, not hard-coded, so each status still matches its
dates whenever the demo is run. This is the difference between a demo that works in October and one
that quietly breaks in November.

| Code | Event | When (relative to seed) | Venue | Pax | Type | Status |
|---|---|---|---|---|---|---|
| `NAI-WED-26` | Naidoo Wedding (confidential) | +3 weeks | Steenberg Estate | 180 | Wedding | ConfirmedInPlanning |
| `VAN-ACT-26` | Vantage Brand Activation | +4 days (short-notice booking) | V&A Waterfront | 600 | Activation | ConfirmedInPlanning |
| `MER-YE-26` | Meridian Year-End Function | +6 weeks | The Point Hotel | 240 | Corporate | ConfirmedInPlanning |
| `RIV-FEST-26` | Riverlight Festival | started 2 h ago, ends in 6 h | Riverside Grounds | 2 500 | Festival | InProgress |
| `DEL-GOLF-26` | Delacroix Corporate Golf Day | −2 weeks | Steenberg Golf Club | 90 | Corporate | Finished |
| `ENQ-TBC-26` | Unnamed enquiry | +8 weeks | — | — | — | Enquired |

The `Enquired` event is there to prove a negative: it **must not** appear on the events board (FR-01).

**Demo reset.** To show an automatic lifecycle transition live in the presentation, a small reset moves
one `ConfirmedInPlanning` event's `StartsAt` to a few minutes ahead, so `EventTransitionWorker` picks
it up while the audience is watching.

## Admin tasks

On the admin board seeded by `PlatformSeeder`.

| Subject | Priority | Assigned to | Column |
|---|---|---|---|
| Renew liquor licence — Riverside venue | High | Thabo N. | Assigned |
| Update PPE stock list | Normal | Priya R. | Assigned |
| Quarterly ice machine service | Normal | Thabo N. | In Progress / Needs Review |
| Health & safety file — October audit | Normal | Priya R. | Complete |

## Suppliers

| Name | Lead time | Liquor supplier |
|---|---|---|
| Coastal Ice Co. | 5 days | No |
| Vine & Co. Distributors | 2 days | Yes |

## Stock

| SKU | Name | Unit | Source mode | Default supplier |
|---|---|---|---|---|
| `CUP-500` | Cups 500 ml | cups | Stock | — |
| `ICE-BULK-KG` | Ice, bulk | kg | Order | Coastal Ice Co. |
| `SPIRIT-MIX` | Spirits, mixed | bottles | Order | Vine & Co. Distributors |
| `BAR-MOBILE` | Mobile bar unit | units | Rent | — |
| `GLASS-WINE` | Wine glasses | glasses | Stock | — |
| `GLASS-CHAMP` | Champagne flutes | glasses | Stock | — |

Plus an **ice machine** as a serialised `EQUIPMENT_ASSET`, which is what the FR-31 incident demo is
reported against.

### Two deliberate traps in the demo data

These exist so the warnings have something to warn about. Neither is an accident.

1. **FR-27, shortfall.** Vantage Brand Activation's ice is planned **below** expected consumption, so
   the requirement screen shows the shortfall warning rather than an empty list.
2. **FR-29, lead time.** Vantage's load-in falls **inside** Coastal Ice's 5-day lead time, so the order
   list shows the lead-time warning. This is why the event is seeded at +4 days rather than +4 weeks.

`BAR-MOBILE` has no default supplier, which exercises the "Unassigned supplier" path in FR-28 — items
with no supplier become a warning, not an order list.

## Quote categories and rate card

Categories (Task 1 Appendix B5): Set up & strike, Infrastructure, Transportation, Crew, Ice, Stock,
Bar kit, Glassware, Other.

Staff rate card roles: bartender, waiter, manager, bar support. Rates are `$staff` fields and are
placeholders until the Bookkeeper provides them.

---

## Checklist templates (FR-25)

Three templates ship as configuration, not demo data, because without one every new event starts with
an empty board.

| Division | Name | Applies to |
|---|---|---|
| CE | Standard event run sheet | any event type with no more specific template |
| CE | Wedding run sheet | Wedding |
| CLM | Logistics run sheet | any event type |

**How a template is chosen.** The seeder loads the division's active `BoardType='Event'` templates,
then prefers one whose `eventTypes` names the event's type; a template with an empty `eventTypes`
is the division's general fallback. A template naming *other* types is never used as a fallback — a
wedding run sheet must not be applied to a festival.

**Why the event type lives in the JSON.** `CHECKLIST_TEMPLATE` has no `EventType` column. Adding one
is a schema change C owns, and a division has a handful of templates — few enough to match in memory
without an index. If the schema gains the column later, the lookup moves into the `Where` clause and
nothing else changes.

### Definition JSON shape

```jsonc
{
  "eventTypes": ["Wedding"],          // empty = applies to any type
  "columns": [
    { "name": "Assigned", "position": 0, "wipLimit": null, "isDoneColumn": false }
  ],
  "cards": [
    {
      "subject": "Pull stock and check against the requirement list",
      "description": null,
      "columnName": "Assigned",        // must match a column name above
      "priority": "High",              // Low | Normal | High | Critical
      "milestoneType": "LoadIn",       // null = no due date
      "offsetHours": -48               // negative = before the milestone's scheduled start
    }
  ],
  "defaultStockRequirements": [
    { "sku": "ICE-BULK-KG", "quantityPerHundredGuests": 45, "sourceMode": "Order" }
  ]
}
```

The C# contract is `Carbonate.Application.Features.Stock.TemplateDefinition`. The database enforces
`ISJSON(DefinitionJson) = 1`; the shape above is enforced by deserialisation, and a template that
fails to parse is skipped with an error log rather than taking the event create down with it.

### What the seeder does

Per plan section 8.7:

- Creates the board, its columns (renumbered from zero, so a gap or duplicate in the JSON cannot
  violate the unique `(BoardId, Position)` index) and its cards.
- Each card's `DueAt` is the matching milestone's `ScheduledStart` plus `offsetHours`. A card naming
  no milestone, or naming one the event does not have, gets **no** due date — a card due at an
  invented time is worse than a card with none.
- Creates one stock requirement per entry:
  `QuantityRequired = ceil(quantityPerHundredGuests × packSize / 100)`, rounded **up** because stock is
  ordered in whole units. `RequiredByDate` is the LoadIn date, falling back to the event date.
- A SKU with no active stock item is reported in `UnknownSkus`, not thrown — a template that outlives
  a discontinued product should not block an event create.

**It does not call `SaveChangesAsync`.** C's event-create endpoint calls it inside its own transaction,
so the caller decides whether the whole create commits. That keeps a half-built event out of the
database if anything downstream fails.

### Known gap

`TASK_CARD.Status` is a free string and neither the plan nor Task 1 gives the vocabulary for event
boards — only the admin board's three columns. Seeded cards start at `"Assigned"`, marked
`// TODO(plan)` in the code. **B owns the card status vocabulary; this needs confirming with him.**
