# Hosting and cloud architecture

Owner: D (Ulrich Bezuidenhout) · Last updated 3 Oct 2026

This document explains what Carbonate runs on and **why each choice was made**, including the
options that were rejected. Short decision records are in `docs/decisions.md`; this is the narrative
version for the README and the presentation.

---

## 1. Topology

Everything lives in one resource group, `rg-carbonate`, in **South Africa North**.

| Service | Resource | SKU | Purpose |
|---|---|---|---|
| Compute | `asp-carbonate` | App Service plan, **Basic B1**, Linux | Hosts both API environments |
| API (dev) | `carbonate-api-dev` | Web App, .NET 10 | Integration environment, deployed from `dev` |
| API (prod) | `carbonate-api-prod` | Web App, .NET 10 | Production, deployed from `main` behind manual approval |
| Database | `sql-carbonate` / `carbonate-dev` | Azure SQL, General Purpose **serverless**, 0.5–1 vCore, auto-pause 1 h | Relational data |
| Files | `stcarbonate` | Storage account, Standard LRS, private `uploads` container | Incident photos, documents, card attachments |
| Secrets | `kv-carbonate` | Key Vault, Standard, RBAC permission model | JWT signing key, Google refresh token, connection details |
| Monitoring | `appi-carbonate` | Application Insights | Request traces, failures, availability test `carbonate-dev-health` against `/health` |

The React SPA is **not** separately hosted. It is built in CI and published into the API's `wwwroot`,
so one deployable unit serves both.

**Region.** Every resource is in South Africa North. NFR-22 requires it, and it is the correct choice
independently: the client, their staff and their venues are all in Cape Town, so a European or US
region would add roughly 150–180 ms of round-trip latency to every request for no benefit, and would
raise data-residency questions about client contact details under POPIA.

---

## 2. Why these choices

### Compute: Basic B1 rather than Standard S1

The project plan specified Standard tier with deployment slots. We are on **B1** instead.

**Constraint.** S1 in South Africa North is roughly five times the price of B1. The subscription is
Azure for Students with a fixed $100 credit, and the system has to stay hosted through to the EXPO.
S1 would have exhausted the credit in about six weeks, taking the system offline before the project
was assessed.

**Why not Free F1 instead.** F1 does not support **Always On**. Carbonate runs two in-process hosted
services — `EventTransitionWorker` (FR-02 automatic stage changes) and `CalendarSyncWorker`
(FR-40–42 outbox drain). On F1 the app unloads after about 20 minutes of inactivity and both workers
silently stop. Automatic transitions would appear to work in testing and then fail in front of the
client. B1 is the cheapest tier where the architecture actually functions.

**What we gave up.** B1 has no deployment slots, so there is no staging slot and no zero-downtime
swap. We preserved the important half of that — a controlled promotion to production — by using a
GitHub environment with a required reviewer, so nothing reaches production without a human approving
it. Both environments are separate App Services on the same plan; apps are billed per plan, so the
second environment costs nothing.

**Upgrade path.** Scaling the plan to S1 is a single portal change with no downtime and no code
change. The slot-swap step is already written in `cd-prod.yml`, commented out, ready to enable.

### Data: Azure SQL serverless

Chosen in Task 1 §5 and unchanged. The decisive setting is **auto-pause after one hour**.

Carbon Events run 40–50 costed events a year with about 15 users, and the system is idle most nights
and weekends. Serverless bills compute per second and pauses entirely when idle, which turns a
roughly $15/month provisioned database into a few dollars. The trade-off is a cold start of up to a
minute on the first request after a pause — acceptable for an internal tool, and mitigated before the
demo by warming the database deliberately.

**Authentication is Microsoft Entra-only.** There is no SQL username or password anywhere, because
none exists. Each App Service connects with its system-assigned managed identity. This is what makes
"no credentials in connection strings" literally true rather than aspirational.

**Permissions are least privilege.** The app identities hold `db_datareader` and `db_datawriter` only
— never `db_owner`. A successful SQL injection against the application still could not drop a table
or read the system catalogues (Task 1 §7.5).

### Files: Blob Storage, private container

Binary data does not belong in a relational database — blob storage is roughly two orders of
magnitude cheaper per GB and does not bloat backups. The `uploads` container is **private with no
anonymous access**; files are served through short-lived SAS links with
`Content-Disposition: attachment` and an explicit content type, so an uploaded `.html` or `.svg`
cannot execute in the application's origin (Task 1 §7.5).

### Secrets: Key Vault with RBAC

Key Vault uses the **Azure RBAC** permission model rather than the legacy vault access policies, so
access is auditable through the same IAM surface as every other resource. The App Services hold
**Key Vault Secrets User** — read-only. They consume secrets; they never write them.

### Same-origin hosting instead of a Static Web App

The refresh token is an `HttpOnly; Secure; SameSite=Strict` cookie. Strict cookies are not sent on
cross-site requests, and Safari — required by NFR-25 — blocks third-party cookies outright. The SPA
and API therefore have to be same-site.

A Static Web App on the Standard plan with a linked backend would achieve that, at about $9/month for
the linked-backend feature alone. Serving the built SPA from the API's `wwwroot` is same-**origin**,
which is strictly stronger, costs nothing, and gives a single deployment unit (NFR-27). We lose the
CDN and pull-request preview environments; neither matters at 15 users.

### Deployment credentials: publish profile instead of OIDC

The plan specified OIDC federated credentials with `azure/login`, which is the better practice —
short-lived tokens, nothing long-lived stored in GitHub.

**It was not available to us.** The subscription sits in the institution's Entra tenant (ADvTECH
Ltd.), which denies student accounts access to Microsoft Entra ID entirely; the App registrations
blade returns HTTP 401. Federated credentials cannot be created without an app registration.

We fell back to an App Service publish profile held as an encrypted GitHub Actions secret. It is
weaker — a long-lived credential rather than a short-lived token — and it is mitigated by scoping
each profile to a single App Service, never committing it to the repository, keeping GitHub secret
scanning and push protection enabled, and the fact that it can be regenerated from the portal
instantly if exposure is suspected.

### The health endpoint does not touch the database

`/health` returns a flat 200 with no dependency checks. The Application Insights availability test
pings it every few minutes; if that endpoint ran a query, the serverless database would never
auto-pause and the entire cost model above would collapse. A deeper dependency check belongs on
`/health/ready`, which the availability test does not call.

This is a small decision that is easy to get wrong and expensive to get wrong.

---

## 3. Cost

### What it actually cost

Measured from Cost Management on 5 October, for the `rg-carbonate` resource group. The resources were
created on 3 October, so this is roughly two days of running, not a month.

| Service | Actual |
|---|---|
| SQL Database | $11.57 |
| Azure Monitor (Application Insights) | $1.86 |
| Azure App Service | $1.00 |
| Key Vault | <$0.01 |
| Storage | <$0.01 |
| Bandwidth | $0.00 |
| **Total** | **$14.43** |

By resource, the two databases are $8.65 (dev) and $2.86 (prod); `appi-carbonate` is $1.86 and the
App Service plan $1.00.

### What that tells us, and it is not what we predicted

The original estimate in this document was $20–30 a month, on the assumption that the serverless
database would be paused most of the time. **Two days of real usage cost $14.43, which annualises far
above that**, and 80% of it is SQL compute.

The cause is our own background worker. `EventTransitionWorker` sleeps until the next event is due,
**capped at 30 minutes** so that a newly created event is noticed without a restart. Azure SQL
serverless pauses after **one hour** of no activity. A worker that queries every half hour therefore
guarantees the database never idles long enough to pause, so we pay for provisioned compute around the
clock. The cap that makes the feature responsive is the same cap that defeats the cost model.

This was not visible in design or in testing. It only appears on a real bill.

**Options, with the trade-off each carries:**

| Option | Effect | Cost |
|---|---|---|
| Raise the worker's sleep cap to several hours | The database can pause between events | An event created or rescheduled while the worker sleeps is not picked up until it wakes |
| Signal the worker when an event is created or rescheduled | Keeps both responsiveness and pausing | More moving parts than a 15-user system warrants |
| Stop the dev App Service when not demonstrating | Removes the dev database's share entirely | Manual, and easy to forget before a demo |
| Apply the free serverless offer to the dev database | Up to 100,000 vCore-seconds free each month | The offer is chosen at database creation; dev predates it, so this means recreating the database |

**What we did at the time:** nothing automatic, and said so. With a deadline, changing the worker's
timing risked the automatic transitions that FR-02 is demonstrated by.

### How it ended, 8 October

The credit ran out three days after submission and the subscription was disabled. October to that
point: **$95.27, $9.97 a day**.

| Resource | Oct to date | Share |
|---|---|---|
| `sql-carbonate` | $87.73 | 92% |
| `appi-carbonate` | $4.64 | 5% |
| `asp-carbonate` (B1 plan) | $2.89 | 3% |
| Key Vault, Storage | <$0.02 | ~0% |

The App Service — the line everyone assumes is the expensive one — was three percent. The database
was ninety-two.

**The budget alert described in the previous version of this section did not exist.** Cost Management
showed `Budget: None`. The claim was wrong when it was written and it is corrected here rather than
quietly removed.

### What was changed

| Change | Effect |
|---|---|
| `EventTransitionWorker.MaxSleep` 30 min → 6 hours | The database reaches its 60-minute idle threshold and pauses (D-012) |
| Deleted `carbonate-api-prod` and the prod database | One environment. Kept dev, because `Seeding:Demo` is set there and the demo world exists nowhere else |
| Retired `appi-carbonate` | Availability test recreated in the per-app component, inside the free ingestion allowance |
| Converted to pay-as-you-go | Student credit is gone |
| Budget created, 50% / 80% alerts | This time it exists |

Expected steady state is roughly **$0.40 a day**, of which the B1 plan is most. B1 is the floor: Free
tier has no Always On, which stops the background workers, and no custom domain support.

**What this costs in capability.** One environment means the two-environment diagnosis that solved
D-011 is no longer available, and a paused database means a cold start of up to a minute on the first
request after idle — so `scripts/presentation-warmup.sh` is now required before any demonstration
rather than merely advisable.

### The design point that still holds

Workers that **sleep until their next due time rather than polling** is still right: a one-minute
polling loop would do the same thing as this, only thirty times more often and with no benefit. The
lesson is narrower and more useful — a sleep cap is not a free parameter, it is a floor on how often
the database is touched, and it has to be set against the auto-pause delay rather than independently
of it.

---

## 4. Reliability

| Concern | Measure |
|---|---|
| Worker continuity | **Always On** enabled on both App Services |
| Availability monitoring | Application Insights availability test against `/health` |
| Backups | Azure SQL automated backups, 35-day point-in-time restore |
| Durability of files | Blob Storage LRS, soft delete |
| Deployment safety | Smoke test against `/health` after every deploy; production requires a human approval |
| Deployment integrity | `WEBSITE_RUN_FROM_PACKAGE=1` on both apps, so a deploy replaces the application rather than copying over it and leaving older files behind (D-011) |
| Rollback | Redeploy the previous artefact, or restore the database to a point in time |
| Release windows | Production deploys Monday–Wednesday, 09:00–15:00 SAST, never within 48 h of a load-in on real client data (NFR-11) |

**Targets.** RPO 24 hours, RTO 4 hours (NFR-13/14). Uptime target 99.0%, rising to 99.5% over the
October–January peak season (NFR-10).

### Restore drill (NFR-13, NFR-14)

A point-in-time restore was performed and timed rather than assumed.

| | |
|---|---|
| Date | 3 October 2026 |
| Source | `carbonate-dev` (Azure SQL, General Purpose serverless) |
| Restored to | `carbonate-restoretest`, same logical server |
| Restore point | Approximately 10 minutes prior |
| **Elapsed time** | **≈ 18 minutes**, from submitting the restore to the success notification |
| Outcome | Succeeded; restored database verified, then deleted |

**Against the target.** 18 minutes sits comfortably inside the 4-hour RTO (NFR-14), with a wide margin
for a larger database later.

**What the number tells us.** The database was effectively empty, so almost none of that 18 minutes
was spent moving data — it is the fixed cost of provisioning a new serverless database and replaying
the backup chain. Restore time will therefore grow slowly rather than linearly as real event data
accumulates, and the 4-hour target remains realistic at the projected 1.5 GB per year.

**Operational note.** Because this is the measured floor rather than a worst case, an incident
response plan should assume roughly 20–30 minutes to a restored database, plus the time to repoint the
application. That is still well inside target, but it is not instant — so for a bad deployment,
redeploying the previous artefact is the faster recovery path, and a restore is reserved for actual
data loss.

---

## 5. Known limitations

Stated deliberately — these are conscious trade-offs, not oversights.

- **No deployment slots**, so promotion to production is a redeploy rather than a swap. Mitigated by
  the approval gate and the smoke test. Resolved by scaling to S1.
- **No WAF and no private database endpoint.** Both are explicitly out of scope (Task 1 Table 6) and
  disproportionate for a 15-user internal system. The SQL firewall and HTTPS-only enforcement carry
  the load instead.
- **Publish profile rather than OIDC**, forced by tenant policy as described above.
- **Single region, no geo-redundancy.** At this scale and budget, a regional outage is accepted risk;
  the backup retention covers data loss, not availability.
- **Cold start** of up to a minute after the database auto-pauses. Warm it before any demo. The
  startup seed runs inside `StartAsync`, so the first request after a deploy can wait two to three
  minutes while a paused database wakes.
- **The dev environment runs as `Development`**, which exposes the Swagger UI, the OpenAPI document
  anonymously, and the developer exception page with full stack traces. Acceptable for fictional data
  on a disposable host with an unpublished URL; it is removed after the demo, and production has none
  of it (decision D-011).

---

## 6. Outstanding before submission

- [x] Application Insights availability test configured against `/health` (`carbonate-dev-health`)
- [x] One timed point-in-time restore performed and the result recorded (NFR-13/14) — 18 minutes, section 4
- [x] Actual cost read from Cost Management and the estimates replaced with measured figures
      (5 Oct) — and the finding recorded: the transition worker's 30-minute sleep cap prevents the
      serverless database from auto-pausing, which is where 80% of the spend goes (section 3)
- [ ] Consolidate Application Insights — creating the two Web Apps auto-provisioned a component each
      (`carbonate-api-dev`, `carbonate-api-prod`) alongside the one we created deliberately
      (`appi-carbonate`). Three components for two apps is untidy; point both apps at `appi-carbonate`
      and remove the duplicates, or keep the per-app ones and remove `appi-carbonate`.
