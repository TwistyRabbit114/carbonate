# Decision log

Short record of every decision that deviates from the Task 1 document or the project plan.
Each one must land in the Task 3 report as "updated since Task 1". Add new entries at the top.

Format: date · decision · why · who · what changes in the Task 3 report.

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
