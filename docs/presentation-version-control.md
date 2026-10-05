# Presentation: version control and the delivery pipeline

Presenter: D (Ulrich Bezuidenhout) · Target: 6 minutes · INSY7315 Task 2

The argument running through this segment: **the process is visible in the repository.** Every claim
below can be clicked on live. Nothing here is a description of what we intended to do.

---

## Before you start

Have these open in tabs, in this order:

1. The repo home page — branch list showing `main`, `dev`, and the `feature/*` branches
2. A merged pull request with the Definition of Done checklist filled in (FR-25 or FR-02)
3. The **Actions** tab, filtered to show both green and red runs
4. **CD production** run #3 or later, paused on *Review deployments*
5. `docs/decisions.md`
6. The live dev API's `/health` or `/swagger`

Zoom the browser to about 150%. A room cannot read default-size GitHub.

---

## 1. The shape of the repository (45 seconds)

> "One repository, three long-lived places: `main` is what is in production, `dev` is what is next,
> and every piece of work is a branch named after the requirement it implements —
> `feature/FR-25-event-template-seeder`, `feature/FR-02-event-lifecycle`."

> "Commit messages start with the requirement id. So `git log` is a traceability matrix you get for
> free: `FR-25: seed the board and stock requirements from a checklist template`."

**Show:** the branch dropdown, then the commit list on `dev`.

**The point to land:** a marker or a new team member can go from a requirement in the specification to
the exact commits that implemented it without asking anyone.

---

## 2. How work gets in (60 seconds)

> "Nothing is committed to `dev` directly. Everything arrives as a pull request with a template that
> carries our definition of done — tests, no secrets, and conditional sections that only apply
> sometimes."

> "Two of those conditions matter for this system. If a pull request returns a money field, the
> security owner must approve it. If it touches the pipeline or infrastructure, I must. That is
> enforced by a CODEOWNERS file, not by remembering."

**Show:** a merged PR with the checklist ticked, including an honestly *unticked* box.

**The point to land:** leaving a box unticked and explaining why is the system working. We have PRs
that say "does one thing — no, three, flagging rather than hiding it."

---

## 3. What has to be true before a merge (90 seconds)

> "Three checks run on every pull request, and all three are required."

| Check | What it does |
|---|---|
| **API build and test** | Restore, build with warnings as errors in the layers that hold business rules, unit tests, integration tests against a real SQL Server in a container, and a dependency vulnerability scan that fails on high or critical |
| **Web build and test** | Lint, typecheck, test, build, `npm audit` |
| **Masking acceptance suite** | Financial masking, deliberately a separate job |

> "The masking suite is split out on purpose. Our requirements say a Crew Lead must never see what an
> event costs us. If that breaks, I want it to be its own red tick in the run summary — not one
> failure buried inside four hundred others."

> "There is also a contract test. Our OpenAPI file is committed, and the test fails if the API has
> changed and the file has not. The front end generates its TypeScript types from that file, so the
> contract cannot silently drift away from the code."

**Show:** the Actions tab with the three named checks on a PR.

---

## 4. How it reaches a server (60 seconds)

> "Merging to `dev` builds and deploys automatically, then smoke-tests the health endpoint."

> "Merging to `main` builds, and then **stops**."

**Show:** the CD production run paused on *Review deployments*. Pause on it. Let the room see it
waiting.

> "That is a GitHub environment with a required reviewer. Nothing reaches production without a person
> approving it. Our project plan originally called for a staging slot and a slot swap, which the
> hosting tier we could afford does not support — so we kept the part that actually matters, which is
> that a human decides."

**The point to land:** the gate is a deliberate replacement for something we could not have, not an
accident.

---

## 5. When it went wrong (90 seconds — the strongest part)

Pick **two** of these three. Do not rush them; this is the section that separates a description from
an account.

### a) The pipeline could not use the authentication the plan specified

> "Our plan specified OIDC federated credentials — short-lived tokens, nothing long-lived stored in
> GitHub. The subscription sits in the institution's tenant, which denies student accounts access to
> Microsoft Entra ID entirely. The app registrations page returns a 401. So federated credentials
> could not be created at all."

> "We fell back to a publish profile in an encrypted secret. It is weaker, and we wrote down exactly
> how it is weaker and what we did to reduce it: scoped to one app, never in the repository, secret
> scanning on, regenerable instantly."

### b) A deployment that looked broken and wasn't

> "Production deployed and the smoke test failed twelve times with a 500. The deployment was fine. The
> database is serverless and pauses when idle, so a cold start plus the first-run seed took longer
> than the two minutes the check allowed."

> "The fix was not to loosen the check. It was to make it able to tell the difference between *still
> starting* and *answered and unhealthy*, and to say which in the log. A check that cannot tell you
> which of those happened is not much of a check."

### c) The health endpoint that must not touch the database

> "Our health endpoint deliberately runs no queries. An availability test pings it every five minutes.
> If it touched the database, the database would never pause, and the entire cost model behind
> choosing serverless would collapse."

**The point to land in all three:** the constraint came first, the decision followed, and both are
written down.

---

## 6. Decisions are a file, not a memory (45 seconds)

**Show:** `docs/decisions.md`.

> "Eleven decision records. Every deviation from the design document, with the constraint that forced
> it, what we gave up, and what has to change in the final report."

> "One example. Our database masks financial columns at rest. The application identity is granted
> permission to read through that mask — which sounds backwards until you see why: the database
> masking protects against someone connecting to the database directly, and the application's own
> masking is what separates a Director from a Crew Lead. Without the grant, a Director would see
> masked costs and the feature would be quietly broken."

---

## 7. Close (20 seconds)

> "Seventeen pull requests, every one reviewed. Around three hundred and fifty tests, running on every
> change. Two environments, and a production deploy that waits for a person. The repository is the
> record — of what we built, and of what we had to change our minds about."

---

## Likely questions

**"Why not trunk-based development?"**
Three people, three modules, one shared schema, and a hard deadline. Short-lived feature branches with
required checks gave us isolation without long-lived divergence. Our branches lived hours to a day,
not weeks.

**"Did the required checks ever actually stop anything?"**
Yes. The build failed on a locale-dependent date format in a field the front end parses. It would have
worked on every machine in this room and broken in a different region.

**"Why squash merges?"**
Linear history on `main` and `dev`. One commit per requirement, so the log reads as a list of delivered
requirements rather than a list of keystrokes.

**"What would you do differently?"**
Infrastructure as code. Our Azure configuration lives in the portal, which means it is documented but
not reproducible — I can tell you what the settings are, but I cannot rebuild the environment from the
repository. For a longer-lived system that would be the first thing to fix.

---

## If asked to prove something live

- **A requirement to its code:** search the commit list for `FR-27`
- **The gate:** open the paused production run
- **The system running:** `/health`, then `/swagger`
- **A real constraint:** `docs/decisions.md`, record D-004
