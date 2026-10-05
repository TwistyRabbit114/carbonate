# Decision log

Short record of every decision that deviates from the Task 1 document or the project plan.
Each one must land in the Task 3 report as "updated since Task 1". Add new entries at the top.

Format: date · decision · why · who · what changes in the Task 3 report.

---

## D-012 — Safeguards on user administration that the plan does not state

**5 Oct 2026 · Owner: C**

The plan says the Director and the Operations Manager create users, deactivate them and change roles
(FR-38), and that there must always be one active Director. Taken literally, anyone holding
`user.manage` could make themselves a Director, which defeats the financial-masking model. So:

- **Only a Director can give someone the Director role, or change a Director's account.** The
  Operations Manager manages everyone else.
- **Nobody can change their own roles or deactivate themselves.** Renaming themselves is fine.
- **The last active Director cannot be removed**, by deactivating them or by taking their role. The
  check runs after the change is saved, inside the transaction, so a change that would leave none is
  undone. It also covers two Directors removing each other at the same moment.
- A role change or deactivation bumps `SecurityStamp` and revokes every refresh token for that person,
  so their session ends within the 15 minutes an access token lasts, as the plan requires.
- Giving someone Director or Accounts means they set up two-step sign-in on their next login; nothing
  extra is needed.
- The initial password is chosen by the administrator and passes the same 12 character and breach
  check as any password. There is no "must change at first login" flag, because the schema has no
  column for one.

**Crew expiry (FR-36).** A daily worker deactivates an account that holds *only* the casual crew
role, has been assigned to at least one event, has no shift still ahead, and whose every event had its
Debrief end (actual end, else scheduled) more than `Crew:DeactivateAfterDays` (7) ago. Two choices the
plan does not spell out: an account with **no assignments** is left alone, so a new account is not
deactivated before its first shift; and an event **with no Debrief milestone** keeps the account open,
because it cannot be shown to be over. It runs at 02:30 Cape Town and sleeps until then.

**Not built.** There is no password reset endpoint, because none is in the published contract.
Plan section 7.1 says a reset bumps the stamp; that needs an endpoint, so it is an open question.

**Why.** Each rule closes a way for administration to be used to widen access.

**Trade-off.** The Operations Manager cannot promote anyone to Director, and an administrator needs a
colleague to change their own roles.

**Task 3 report.** Describe these safeguards under access control, and list password reset as outstanding.

---

## D-011 — How costings, confirmation and invoices behave where the plan is silent

**5 Oct 2026 · Owner: C**

The plan fixes the approval rule (FR-14), versioning and the confirmation-to-lifecycle link. Where it
stops, these are the working choices, all for the client to confirm at UAT:

- **Approval threshold and margin bands are placeholders.** `Quotes:ApprovalThresholdZar` is R100 000
  and the four margin bands are invented, in `appsettings.json`, flagged in a `_comment`. Plan section
  15, item 10 asks B to get the real figures. Nothing in code depends on the values.
- **Submit.** Only a draft can be submitted. If the submitter holds `quote.approve` the costing is
  approved immediately ("a costing the Director wrote counts as approved"); otherwise it waits.
- **Issue.** A costing at or below the threshold can be issued from Draft without anyone approving it.
  Above it, issue is a 422 until the Director has approved it.
- **Editing.** Draft: in place. Pending or approved: in place, and it goes back to Draft with the approval
  removed, because the numbers changed after approval. Issued: a new version, and the old one becomes
  Superseded. Accepted or superseded: refused. The plan only says issued; accepted is my addition.
- **Margin** is profit on the price before VAT, because VAT is not Carbon's money.
- **Confirmation** is allowed only while the event is Enquired, and is one transaction with the
  `Enquired` to `ConfirmedInPlanning` move. A deposit needs amount, date and reference; a PO needs number
  and date.
- **Invoices** are created `Issued` (the accountant raises them with an issue date), numbered
  `INV-<year>-<0001>` per year, and due after the client's payment terms. The amount defaults to the
  accepted costing. They can then be marked Paid (with a date) or Void. `Draft` exists in the enum but
  nothing creates it.
- **Cost history** shows each earlier event's accepted costing, or its latest one if none was accepted.

**Why.** Each of these is the smallest behaviour that satisfies the plan's wording and keeps the money
rules in one place, rather than inventing workflow the client has not asked for.

**Trade-off.** If the client wants a separate approval step for every costing, or invoices that start as
drafts, those are small changes in `QuoteRules` and `CommercialService`.

**Task 3 report.** List the placeholders as outstanding client confirmations, and describe the quote
status flow as built.

---

## D-011 — Deploys run from package, and only dev runs as `Development`

**5 Oct 2026 · Owner: D**

Two deployment settings, both found the hard way.

### `WEBSITE_RUN_FROM_PACKAGE = 1` on both App Services

**What happened.** The dev API started cleanly, ran its workers, queried the database — and then threw
`System.BadImageFormatException: Bad IL range` on every request that reached the rate limiter, with
garbled type names in the stack trace. Production, running the **same artefact**, was perfectly
healthy.

The difference was deployment history. `azure/webapps-deploy` copies files over whatever is already in
`wwwroot` and removes nothing, and dev had been deployed a dozen times across several days of
fast-moving code. Listing the directory found `runtimes/unix/lib/net9.0` — libraries from an older
deployment that targeted .NET 9, sitting alongside .NET 10 assemblies. The loader was resolving a
mixture of the two.

**The fix.** `WEBSITE_RUN_FROM_PACKAGE = 1` mounts the deployment zip read-only instead of extracting
it, so what runs is exactly the artefact and nothing can linger between deploys.

**Why it is worth recording.** The symptom looked like a compiler or runtime fault and was neither. It
was only diagnosable because two environments run the same artefact — prod being healthy is what
proved the code innocent in one request. That is an argument for having a second environment that has
nothing to do with "testing before production".

**Trade-off.** `wwwroot` becomes read-only at runtime, so nothing can write into the application
directory. Carbonate writes files to Blob Storage, so this costs us nothing.

### `ASPNETCORE_ENVIRONMENT = Development` on dev only

Set on `carbonate-api-dev` so the Swagger UI is reachable, which matters while there is no SPA to
demonstrate. **Never set on `carbonate-api-prod`.**

**What it exposes.** More than expected. `WebApplication` adds the developer exception page
automatically in Development, so an unhandled error on the dev hostname returns a full stack trace,
internal paths, request headers and the caller's IP. `/openapi/v1.json` is also anonymous there, so the
whole API surface is readable by anyone with the URL.

**Why that is acceptable here, and only here.** The dev environment holds fictional demo data, is
disposable, and its hostname is not published. The same settings on production would be a real
disclosure problem, which is why the absence of both settings on prod is deliberate rather than
incidental — as is the absence of `Seeding__Demo`.

**Remove it after the demo.** Three app settings separate the dev environment from a production one,
and that is a thin margin to leave standing indefinitely.

**Task 3 report.** Worth a short section on deployment hygiene: the failure, how two environments
made it diagnosable, and the configuration that prevents it. Also state the dev exposure explicitly
rather than leaving it implied.

---

## D-010 — The Google OAuth callback is anonymous, with the user carried in a signed `state`

**4 Oct 2026 · Owner: D**

`GET /api/calendar/oauth/callback` is `[AllowAnonymous]`. The user it belongs to travels in the OAuth
`state` parameter as a signed token: five-minute expiry, a `purpose` claim of `calendar_oauth`, subject
= the user id, signed with the existing `Jwt:SigningKey`. The callback validates it and refuses
anything expired, unsigned, signed by something else, or carrying a different purpose. It never issues
a session token — connecting a calendar is not a sign-in.

This takes the anonymous endpoint list from four to five.

**Why.** Google redirects the **browser** to the callback, and a browser following a redirect sends no
`Authorization` header. A permission attribute on that endpoint can never fire, so leaving one there
would be security theatre: the endpoint would be unreachable, not protected. `state` is the parameter
OAuth defines for carrying context across the round trip, and signing it is also the CSRF defence
`state` exists to provide. A bare user id in the query string would let anyone connect their own Google
account to someone else's Carbonate user.

**Why a signed token rather than a state table.** A row would let us mark a state used, which a token
cannot. It is also a schema change, and the schema is C's. For a value that lives five minutes, the
token is the boring option (§14 rule 4).

**Trade-off.** A state is replayable inside its five-minute window. The exposure is bounded: a replay
needs Google's authorisation code too, and those are single-use, so a second attempt has nothing to
redeem. If a state table is added later, `ICalendarStateTokens` is the only thing that changes.

**Task 3 report.** Task 1 §7.1 lists the anonymous endpoints. Add the callback and this reasoning —
it is a good example of a control that had to change shape because of how a protocol actually works.

---

## D-009 — Event board cards are `Open` or `Done`

**4 Oct 2026 · Owner: B**

Recorded here because D's template seeder (FR-25) sets the status on every seeded card. A card is
`Done` exactly when its column has `IsDoneColumn = true`, otherwise `Open`; the boards service keeps it
in step on every move, along with `CompletedAt`. Admin boards keep the plan's three columns:
`Assigned`, `InProgressOrNeedsReview`, `Complete`.

**Task 3 report.** `TASK_CARD.Status` is described without a vocabulary. State both sets.

---

## D-008 — The template's event type lives in `DefinitionJson`, not a column

**4 Oct 2026 · Owner: D**

FR-25 picks a template by division, `BoardType='Event'` **and event type**. `CHECKLIST_TEMPLATE` has no
`EventType` column, so the type is an `eventTypes` array inside `DefinitionJson`. The seeder loads the
division's active Event templates — a handful of rows — and matches in memory. An empty array means the
template applies to any type; a template naming other types is never used as a fallback.

**Why.** Adding a column is a schema change C owns, on the day schema v1 merged, for a lookup that
returns under ten rows. The boring option (§14 rule 4) is to match in memory.

**Trade-off.** The event type is not indexable and not visible in a `SELECT` over the table. If the
template count ever grows past a handful, the lookup moves into the `WHERE` clause and nothing else
changes.

**Task 3 report.** Describe the template definition JSON shape (`docs/seed-data.md`) alongside the
Task 1 §5.3 schemas.

---

## D-007 — Deployment configuration: app settings, Key Vault references and the `UNMASK` grant

**4 Oct 2026 · Owner: D**

How the dev App Service actually reaches its dependencies, recorded because none of it is visible in
the repository:

- Each App Service holds a **system-assigned managed identity**, which is a contained database user in
  `carbonate-dev` created `FROM EXTERNAL PROVIDER`, in `db_datareader` and `db_datawriter` only.
- Those identities are additionally granted **`UNMASK`**. Dynamic Data Masking is protection against
  someone connecting to the database directly; it is *not* the mechanism that masks `$cost` fields
  between application roles — `IFinancialMasker` is. Without `UNMASK` the app reads `0.00` for every
  masked column and a Director would see masked costs, which is the opposite of the intent. This
  amends D-005.
- Secrets are **Key Vault references** (`@Microsoft.KeyVault(SecretUri=…/)`), not literal app settings:
  `Jwt__SigningKey` → `JwtSigningKey`, `Auth__MfaEncryptionKey` → `AuthMfaEncryptionKey`. The trailing
  slash means the current version, so rotating a secret needs no App Service change.
- `ConnectionStrings__Sql` uses `Authentication=Active Directory Default`, so the identity is picked up
  from the platform. There is no password in it because no SQL password exists.
- `Jwt__Issuer` and `Jwt__Audience` are the environment's own base URL, so a dev token is not valid in
  production.

**Why record it.** `docs/hosting.md` claims Key Vault holds the JWT signing key. This is the proof, and
it is what someone rebuilding the environment from scratch needs.

**Trade-off.** Configuration that lives only in the portal is configuration that can drift between dev
and production. Infrastructure-as-code (Bicep) would fix that and is out of scope at this size; the
settings table above is the mitigation.

**Migrations are applied by a human, not the app.** The app identity has no DDL rights by design, so
`dotnet ef database update` runs under the Entra admin account. A deploy never migrates.

**Task 3 report.** Task 1 §7 describes DDM as a masking control. Clarify that it guards direct database
access and that the application-layer masker is what separates roles.

---

## D-006 — How the MFA steps authenticate, and a new secret

**3 Oct 2026 · Owner: C**

- Login returns a five-minute MFA token for Director and Accounts. `mfa/verify` takes it in the body,
  as the plan says. `mfa/enrol` and `mfa/confirm` take it as the **bearer token**, behind an `MfaPending`
  policy, so the only anonymous endpoints stay the five the plan lists. The MFA token is refused
  everywhere else, including by the deny-by-default policy.
- TOTP seeds are encrypted at rest with AES-256-GCM. The key is a new setting, `Auth:MfaEncryptionKey`
  (base64, 32 bytes). Locally it goes in user-secrets; in Azure it goes in Key Vault with the JWT key.
- A role or password change will revoke the user's refresh tokens (the Users module calls `RevokeAllForUserAsync`) instead of comparing a stored stamp,
  because `REFRESH_TOKEN` has no stamp column. The next refresh fails, as the plan requires.
- Replay of a used TOTP code inside its 30-second window is not blocked; the rate limit and lockout
  are the control.

**Why.** Keeps the anonymous surface small and avoids a schema change.

**Trade-off.** Two ways of passing the MFA token, which B needs to know when building the login screens.

**Task 3 report.** Describe the sign-in flow as built, and list TOTP replay protection as not built.

---

## D-005 — Schema v1 additions and database-level guards

**3 Oct 2026 · Owner: C**

Schema v1 follows the plan's data model, with these additions and choices:

- `EVENT.IsActive` added so a Director's delete is a soft delete (FR-09). `EVENT.VenueId` is nullable
  because the demo `Enquired` event has no venue; the API still requires a venue on create (FR-01).
- `CreatedAt` added to `QUOTE`, `INVOICE` and `TASK_CARD`, which had no timestamp, so the clustered
  index can sit on a timestamp as the plan asks. Small lookup tables keep a clustered GUID key.
- The audit table is insert-only through an `INSTEAD OF UPDATE, DELETE` trigger, so it holds whichever
  principal connects, not only the app identity. Monthly partitioning and the 24-month purge are not
  built; a purge job would have to disable the trigger deliberately.
- Dynamic Data Masking is applied to seven `$cost` columns by raw SQL in the migration. A later
  migration that alters one of those columns must drop and re-add the mask.
- Check constraints repeat the API's rules where the database can express them: event code allowlist,
  event window, board type, incident subject, JSON column validity, and order-list approver differing
  from its generator (FR-30).

**Why.** Marks for the database are for constraints and integrity, and the plan wants the database to
be a second line of defence behind the services.

**Trade-off.** Constraints duplicate some service validation, so a rule change touches two places.

**Task 3 report.** Describe DDM as protection against direct database access, not between app roles,
and state that partitioning and the retention purge were not built.

---

## D-004 — Publish profiles instead of OIDC federated credentials

**3 Oct 2026 · Owner: D**

GitHub Actions authenticates to Azure using an App Service publish profile stored as an encrypted
GitHub Actions secret, not OIDC federated credentials.

**Why.** The project plan (§11) specifies OIDC with `azure/login` and no publish profiles. That
requires creating an Entra ID app registration. The institutional tenant this subscription sits in
(ADvTECH Ltd.) denies students access to Microsoft Entra ID entirely — the portal returns 401 on the
App registrations blade — so federated credentials cannot be created. This is an environmental
constraint, not a design preference.

**Trade-off.** A publish profile is a long-lived credential held in GitHub rather than a short-lived
federated token, so it is weaker. It is mitigated by: storing it as an encrypted repository secret
(never in the repo), scoping it to a single App Service rather than the subscription, and the fact
that it can be regenerated from the portal at any time if exposure is suspected. Secret scanning and
push protection are on, so an accidental commit of it would be blocked.

**Task 3 report.** Section 8 of the Task 1 document describes the GitHub Actions pipeline. Note the
authentication method actually used and the tenant restriction that forced it — this is a legitimate
real-world constraint and worth describing rather than hiding.

---

## D-003 — No Static Web App; the SPA is served from the API

**3 Oct 2026 · Owner: D**

The SPA is built in CI and copied into the API's `wwwroot`, with a fallback to `index.html`. This is
hosting option 3 in project plan section 4.

**Why.** The refresh token is an `HttpOnly; Secure; SameSite=Strict` cookie, which is not sent on
cross-site requests, and NFR-25 requires Safari, which blocks third-party cookies. The SPA and API
therefore have to be same-site. Option 1 (Static Web App Standard with a linked backend) achieves that
but costs about $9/month purely for the linked-backend feature. Serving from `wwwroot` is same-*origin*,
which is strictly stronger, costs nothing, and gives a single deploy unit (NFR-27).

**Trade-off.** We lose Static Web App's CDN and pull-request preview environments. Neither is marked,
and at 15 users the CDN is irrelevant.

**Task 3 report.** Task 1 section 4.2 shows a separate Static Web App in the deployment diagram. Update
the diagram and the section 9 costs.

---

## D-002 — App Service B1 Basic with two apps, not Standard with slots

**3 Oct 2026 · Owner: D**

One B1 App Service plan in South Africa North hosting two apps, `carbonate-api-dev` and
`carbonate-api-prod`. The plan is scaled to S1 for the final week only, to add a staging slot and
demonstrate a slot swap, then scaled back down.

**Why.** The project plan asks for Standard tier with deployment slots. S1 in South Africa North is
roughly $73/month, which exhausts the $100 Azure for Students credit in about six weeks — before the
EXPO, and the system has to stay hosted until then (plan section 15, item 13). B1 is roughly $13/month
and still supports **Always On**, which the `EventTransitionWorker` and `CalendarSyncWorker` hosted
services require. Apps are billed per plan, so the second app costs nothing.

**Trade-off.** No zero-downtime slot swap during the build. The deployment gate is preserved through the
GitHub `production` environment with a required reviewer, so promotion to production is still manual and
auditable. The week of the demo, the plan goes to S1 so the slot swap can be shown live and the
`cd-prod.yml` swap step (currently commented out) is enabled.

**Task 3 report.** Task 1 section 4.2 and section 9 both assume Standard with slots. Correct both, and
state the actual monthly figure from Cost Management.

---

## D-001 — `/health` does not touch the database

**3 Oct 2026 · Owner: D**

`/health` returns a flat 200 with no dependency checks. A deeper check, if needed, goes on
`/health/ready` and is not used by the availability test.

**Why.** Azure SQL serverless auto-pauses after an hour of inactivity, which is the whole cost case in
Task 1 section 5. The Application Insights availability test pings the health endpoint every five
minutes; if that endpoint queried the database, it would never pause and the serverless bill would
become a provisioned bill.

**Task 3 report.** Worth a sentence in the hosting rationale — it is a real example of cost-aware design.

---

<!--
Template for new entries:

## X-00n — <one-line decision>

**<date> · Owner: <role>**

<what was decided>

**Why.** <reasoning, with the constraint that forced it>

**Trade-off.** <what is lost, and why that is acceptable>

**Task 3 report.** <what has to change in the Task 1 content>
-->
