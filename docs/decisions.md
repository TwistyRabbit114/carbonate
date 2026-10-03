# Decision log

Short record of every decision that deviates from the Task 1 document or the project plan.
Each one must land in the Task 3 report as "updated since Task 1". Add new entries at the top.

Format: date · decision · why · who · what changes in the Task 3 report.

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
