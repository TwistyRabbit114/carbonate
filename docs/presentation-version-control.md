# Presentation walkthrough — D (Ulrich)

Role D: DevOps, cloud hosting, and the lifecycle, stock, incidents and calendar modules.

**Slot: 10 minutes.** Recorded, so each segment below can be a separate take and cut together. Nothing
here depends on a live run going right first time.

---

## Before you record

**Warm the system up, 15 minutes before.** The database auto-pauses and the first request after that
takes up to a minute. Open the dev URL, log in, click around the board, leave it. Do it again five
minutes before recording.

**Tabs, in this order, left to right.** Set them up once and don't rearrange mid-take.

1. The running app — `carbonate-api-dev-…azurewebsites.net`, signed in as `sarah@carbon.demo`
2. GitHub → the repo home page
3. GitHub → a merged PR with the checklist filled in (FR-25 or FR-02)
4. GitHub → **Actions**, unfiltered so both green and red runs show
5. GitHub → **CD production**, a run paused on *Review deployments*
6. `docs/decisions.md` on GitHub
7. Git Bash, sized large, in `~/Documents/PROG7314/carbonate`

**Browser zoom to 150%.** A recording that nobody can read is worth nothing.

**Close** Gmail, the FPS counter overlay, and anything else in the top bar.

---

## Segment 1 — What I own (0:30)

**Screen:** the running app, events board.

> "I'm Ulrich, role D. I own three things on this project: the hosting and the cloud setup, the
> delivery pipeline — so the repository rules, the automated checks and the deployments — and four
> feature modules: the event lifecycle, stock and order lists, incident reports, and the Google
> Calendar sync."
>
> "Most of what I'll show you is either running right now, or it's a record of a decision we had to
> change our minds about."

---

## Segment 2 — Hosting and cloud architecture (2:00)

**Screen:** Azure portal → `rg-carbonate` → Overview, resource list visible.

> "Everything runs in one resource group in South Africa North. That region is a requirement —
> NFR-22, data residency — and it's also just correct: the client, their staff and their venues are
> all in Cape Town, so a European region would add about 150 milliseconds to every request for no
> benefit."

**Point at the resources as you name them.**

> "Two App Services, dev and production, on one Basic B1 plan. The plan asked for Standard tier with
> deployment slots. Standard is about five times the price, and the subscription is Azure for
> Students with a hundred-dollar credit that has to last until the EXPO — so Standard would have
> taken the system offline before anyone marked it."
>
> "We couldn't go cheaper either. The Free tier doesn't support Always On, and this system runs two
> background workers: one that moves events through their stages automatically, and one that pushes
> to Google Calendar. On Free they'd silently stop after twenty minutes of inactivity. B1 is the
> cheapest tier where the architecture actually works."

**Screen:** switch to the SQL database → Overview, showing serverless and auto-pause.

> "The database is serverless with auto-pause. Carbon run about fifty events a year and the system is
> idle most nights — serverless bills per second and pauses when idle."

**Screen:** `docs/hosting.md` on GitHub, section 3.

> "I'll come back to that one, because the bill said something different."

**Screen:** the App Service → Identity blade, System assigned = On.

> "One thing I'm pleased with: there's no password anywhere in this system's configuration. The apps
> connect to the database, the storage account and the key vault with a managed identity. Not a
> password in a vault — no password exists at all. The database is set to Entra-only authentication."

---

## Segment 3 — Version control and the pipeline (3:00)

**Screen:** GitHub repo home, branch dropdown open.

> "Three places: `main` is production, `dev` is what's next, and every piece of work is a branch named
> after the requirement — `feature/FR-25-event-template-seeder`, `feature/FR-02-event-lifecycle`."

**Screen:** the commit list on `dev`.

> "Commit messages start with the requirement id. So the git log is a traceability matrix you get for
> free — you can go from a requirement in the specification to the exact commits that implemented it
> without asking anyone."

**Screen:** a merged PR, checklist visible.

> "Nothing goes into `dev` directly. Everything is a pull request with a template carrying our
> definition of done. Two of those conditions matter here: if a pull request returns a money field,
> the security owner has to approve it; if it touches the pipeline or the infrastructure, I do. That's
> enforced by a CODEOWNERS file, not by remembering."

**Scroll to an unticked box.**

> "And leaving a box unticked is the system working. This one says 'does one thing — no, three,
> flagging rather than hiding it.'"

**Screen:** Actions tab, a PR run with the three checks.

> "Three checks run on every pull request and all three are required. The API build with its unit and
> integration tests. The web build. And the financial masking suite, deliberately split into its own
> job — our requirements say a Crew Lead must never see what an event costs us, and if that breaks I
> want it to be its own red tick, not one failure buried inside four hundred."
>
> "There's also a contract test. Our OpenAPI file is committed, and the test fails if the API has
> changed and the file hasn't. The front end generates its TypeScript types from that file, so the
> contract can't quietly drift away from the code."

**Screen:** CD production, paused on *Review deployments*. **Pause here. Let it sit.**

> "Merging to `dev` deploys automatically. Merging to `main` builds — and then stops."
>
> "That's a GitHub environment with a required reviewer. Nothing reaches production without a person
> approving it. The plan wanted a staging slot and a slot swap, which the tier we could afford doesn't
> support — so we kept the part that actually matters, which is that a human decides."

---

## Segment 4 — The modules, running (2:30)

**Screen:** the app, events board.

> "Now the parts I built, running against the live system."

**Point at the In Progress column.**

> "Riverlight Festival is in In Progress, and nobody moved it there. Its start time passed while the
> system was idle, a background worker woke up, the state machine allowed the move, and three
> observers fired — one wrote the audit entry, one queued a calendar update, one notified the manager.
> Same for Delacroix in Finished. Those two columns are marked AUTO for that reason."

**Drag an event from Confirmed to In Progress.**

> "A person can also move one early — the client's ready, so the crew starts. The clock doesn't get a
> vote when a person is asking."

**Try to drag the Finished event somewhere.**

> "But Finished is terminal. The API returns a 409 and the reason, because no amount of paperwork makes
> that move legal."

**Point at the board generally.**

> "Five events here. There's a sixth in the database — an enquiry — and it's deliberately not on the
> board. That's FR-01, and the demo data has it so we can prove a negative."

**Point at Naidoo Wedding's CONFIDENTIAL badge.**

> "This one's confidential. When it syncs to Google Calendar it goes across as 'Confidential event'
> and its code — no client name, no contact details, no money. Google is outside our security
> boundary, so the payload carries only enough for the team to know they're committed at that time."

**Screen:** Git Bash. Run the two masking curls (have them ready to paste).

> "And the financial masking, from the API directly. Same endpoint, two roles."

**Run as Sarah, then as Ops.**

> "The Event Manager sees the unit cost. The Operations Manager doesn't — and look at what that means:
> the field isn't zero and it isn't null, it's *absent*. That's the rule from our design document, and
> it's enforced in the service and again by a global filter."

---

## Segment 5 — When it went wrong (1:30)

**This is the most important ninety seconds. Don't rush it.**

**Screen:** `docs/decisions.md`, scrolled to D-011.

> "Two things from the last two days."
>
> "The dev environment started throwing errors on every request — a corrupt-assembly exception, deep
> inside the .NET runtime. Production was running the identical build, perfectly healthy. That's what
> told us the code was fine. The deployment tool copies files over whatever is already there and
> removes nothing, and dev had been deployed a dozen times in three days — there was a .NET 9 runtime
> folder left behind from an earlier build, mixed in with .NET 10 assemblies. One app setting fixed
> it permanently: the deployment is now mounted as a package instead of copied over the top."
>
> "It was only diagnosable because two environments run the same artefact. That's a better argument
> for a second environment than 'test before production'."

**Scroll to D-012.**

> "The second one is more uncomfortable. Our hosting rationale argues for a serverless database
> because it pauses when idle and costs almost nothing. I checked the actual bill today. Two days of
> running cost fourteen dollars forty-three, and eighty per cent of that was database compute."
>
> "The cause is my own background worker. It sleeps until the next event is due, capped at thirty
> minutes so a newly created event gets noticed. The database pauses after an hour of inactivity. A
> query every thirty minutes means it never reaches an idle hour — so it never pauses, and we pay for
> it around the clock."
>
> "Nothing about that is visible in code review or in testing. The worker is correct, the queries are
> efficient, it's the design the plan asked for. It only shows up on a bill. It's written up with the
> options and what each one costs, and the fix is a one-line constant once the demonstration is over —
> I didn't change it tonight because it would risk the automatic transitions I just showed you."

---

## Segment 6 — Close (0:30)

**Screen:** `docs/decisions.md`, top of the file.

> "Twelve decision records. Every deviation from our design document, with the constraint that forced
> it, what we gave up, and what has to change in the final report. The tenant that blocked the
> authentication method we planned. The tier we couldn't afford. A database permission that sounds
> backwards until you see why."
>
> "Around thirty pull requests, every one reviewed. Six hundred-odd tests running on every change. Two
> environments, and a production deploy that waits for a person."
>
> "The repository is the record — of what we built, and of what we had to change our minds about."

---

## If you're asked questions

**"Why not trunk-based development?"**
Three people, three modules, one shared schema, a hard deadline. Short-lived branches with required
checks gave isolation without long divergence — ours lived hours, not weeks.

**"Did the required checks ever stop anything real?"**
Three times in two days. A locale-dependent date format in a field the front end parses — it would
have worked on every machine in this room and broken in another region. A database query written so
that Entity Framework couldn't translate it, which compiled fine and failed on every request. And a
performance test that found an audit column too small for a realistic order run — one order list fits,
twenty don't.

**"What would you do differently?"**
Infrastructure as code. Our Azure configuration lives in the portal, so it's documented but not
reproducible — I can tell you every setting, but I can't rebuild the environment from the repository.
For anything longer-lived that's the first thing I'd fix.

**"What isn't finished?"**
The Google OAuth connect flow. Everything behind it — the outbox, the retry, the deduplication, the
redaction, the signed callback state — is built and tested. The flow itself needs a Google Cloud
project, and in testing mode the credentials expire every seven days. It's documented rather than
half-built.

---

## Recording notes

- **Record each segment separately.** Six short takes beat one long one, and a fluffed line costs you
  thirty seconds instead of ten minutes.
- **Segment 4 is the one to rehearse.** Dragging cards live is where a recording goes wrong. Do a dry
  run first and know which event you're dragging.
- **If a drag fails on camera, say so and move on.** "That's the 409 I mentioned" is a better recovery
  than silence, and an honest stumble costs nothing.
- **Put the event back afterwards** if you drag one, so the board is in a known state for the next take.
- **Don't read this document aloud.** Know the three or four things in each segment and say them in
  your own words. The written version is stiffer than you should sound.
