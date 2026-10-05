# Non-functional results

Two parts: the back end and security (C), then the screens (B).

## Back end and security (role C)

Results for the requirements C owns (plan section 8: NFR-09 and NFR-15 to NFR-21), plus the masking gate
(NFR-28) and the user rule (NFR-29). Every row names the test that proves it, so it can be re-run with
`dotnet test` (the integration tests start a throwaway SQL Server container).

| NFR | Requirement | Result | Evidence |
|---|---|---|---|
| NFR-09 | A past costing and the cost comparison open in under 2 s on ten years of history | **Met locally.** 600 events (10 years at 60 a year) for one client, each with an accepted costing of 8 lines: the cost comparison took 120 ms and opening an old costing took 332 ms. Not yet timed on the hosted Azure SQL serverless database | `CostHistoryPerformanceTests` (fails above 2 s) |
| NFR-15 | A conflicting edit is rejected, not overwritten | **Met.** A stale `rowVersion` returns 409 with the current, masked values; covered for events, costings and task cards | `EventsEndpointTests`, `CommercialEndpointTests`, `CardEditEndpointTests` |
| NFR-16 | The audit trail cannot be changed or deleted | **Met.** The database refuses UPDATE and DELETE on audit rows (insert-only trigger); every write endpoint tested writes its audit entry | `SchemaTests`, the `audit` assertions in the endpoint tests |
| NFR-17 | Cost fields never reach a role that may not see them | **Met.** Absent from the JSON, not null, on every listed endpoint for every role, and present for the finance roles; a test fails if a new money field is not on the list | masking suite (`MaskingTests`, required check), `FinancialMaskerTests`, `FinancialMaskingFilterTests` |
| NFR-18 | A second factor for the Director and Accounts | **Met.** Sign-in returns a short-lived token that works only on the enrolment and verify routes; a wrong or reused code is refused | `ApiSecurityTests`, `AuthServiceTests`, `DemoAccountsTests` |
| NFR-19 | Sessions are short-lived and revocable | **Met.** Access token 15 minutes, session cap 8 hours; the refresh cookie is HttpOnly, SameSite=Strict and scoped to `/api/auth`, works once, and a replay revokes the whole chain; deactivating an account ends its tokens | `SessionSecurityTests`, `AuthServiceTests` |
| NFR-20 | Repeated bad sign-ins are slowed down | **Met.** Five wrong passwords lock the account for 15 minutes even against the right password; it recovers afterwards; unknown email and wrong password give the same answer; sign-in is also rate limited per client | `SessionSecurityTests`, `ApiSecurityTests` |
| NFR-21 | Casual crew accounts end after the event | **Met.** A daily worker deactivates a casual-crew-only account 7 days after the debrief once they have no future assignment, and records it in the audit trail | `CrewExpiryServiceTests`, `CrewExpiryTests`, `CrewAccountExpiryWorker` schedule test |
| NFR-28 | Masking tests gate production | **Met.** The masking suite is its own CI job and a required check on pull requests, so nothing reaches `main` without it passing | CI "Masking acceptance suite" |
| NFR-29 | At least one active Director at all times | **Met.** The last active Director cannot be deactivated or lose the role | `LastDirectorTests` |

### Security checks run (over HTTP, against a real database)

- Forged access tokens: extra permissions added without re-signing, and a token with its signature removed, are both 401. The MFA-step token cannot be used as a sign-in.
- Hostile text (script tags, event handlers, SQL fragments) in an event name is stored as plain text and returned as JSON with `X-Content-Type-Options: nosniff`; the table survives the SQL fragment. Card descriptions go through the strict allowlist sanitiser (B).
- Security headers and the content security policy are asserted on every response; a CSP violation report endpoint is in place. A headless browser run against the hosted dev site showed one remaining violation (a library probing `eval`), which is blocked and harmless.
- Every endpoint declares its authorisation, and a test fails if one does not; the only anonymous routes are login, MFA verify, refresh, CSP report and health.

### Not done (C)

- **OWASP ZAP baseline scan:** not run (D).
- **Row-Level Security** (plan stretch): not built. Object-level access is enforced in the queries (404 for an event the caller cannot see) instead.
- **NFR-09 on the hosted database:** the figures above are from a local container. The hosted serverless database is slower on its first request after idle.
- **Password reset:** there is no password reset endpoint yet.

## The screens (role B)

Measured on 5 October against a local copy of the system: the production build of the app, served over HTTPS
the way the dev site serves it, talking to the real API with the demo data. Playwright drove real browsers.
Slow links use Chrome's network throttling: 5 Mbps with 40 ms round trips, and 1 Mbps with 100 ms. Each load
time is the middle of three cold runs, with nothing cached.

| NFR | Requirement | Result | Evidence |
|---|---|---|---|
| NFR-01 | Someone untrained finds their events, call times and tasks in under 2 minutes | **Not run.** It needs five people who haven't seen the app. The screens are built for it: crew land on their next event with the call time and venue at the top, 0 steps from signing in | NFR-04 below |
| NFR-03 | An event with its board and stock is set up in under 5 minutes | **Met by a script, a person still to time.** A scripted run of New event took 16.7 s to a saved event, with its task board and stock list seeded from the template. That's a floor, not how long a person takes to type twelve fields | New event form on the local copy |
| NFR-04 | Each role's main task within 3 steps | **Met.** Counted from where each role lands after signing in: crew see tonight's call time and venue in 0 steps and report a breakage in 1; the Event Manager reaches an event's costing in 2; Accounts land on order list approvals and invoices (0); Operations reach Generate order lists in 1; the Director reaches the audit log in 1 | Scripted journeys, one per role |
| NFR-06 | Under 2 s at 5 Mbps, under 4 s at 1 Mbps | **Met at 5 Mbps. At 1 Mbps, only once the files are compressed.** The dev site sends the app's files uncompressed, so a first visit at 1 Mbps takes 5.1 to 6.1 s. With gzip the same visit takes 2.5 to 3.0 s. A repeat visit takes 1.0 s even uncompressed, because the built files are cached for a year. The fix is compressing the app's files on the API (C); numbers in the table below | Sign-in page, crew landing, events board |
| NFR-07 | A card move shows in under 300 ms | **Met.** With a full second added to every request, the card showed in its new column after 43 to 82 ms, before the server had answered | Three moves on an event task board |
| NFR-23 | Usable at 360 px on a phone | **Met in browsers; a real phone still to try.** At 360 px nothing scrolls sideways on any crew screen or on the main office screens, and the crew journey passes in all four browser engines at that width | Accessibility sweep, browser run |
| NFR-24 | Contrast of at least 4.5:1, touch targets of 44 px | **Met on the crew screens.** axe in real Chrome, where it can measure contrast, found no WCAG 2.2 AA problems on 27 screens and dialogs at desktop and phone width. Everything tappable on the crew screens is at least 44 px; four links and the skip link were smaller and were fixed on 5 October. On office screens viewed at phone width, an event title on the board and two supplier names in a table are 21 to 36 px high. They pass WCAG's own target size rule but not our 44 px aim | Accessibility sweep |
| NFR-25 | The current and previous Chrome, Safari, Edge and Firefox | **Met for the current versions.** Chrome 154, Edge 154, Firefox 155 and WebKit 26.6 (Safari's engine, run on Windows rather than Safari on a Mac): the office journey and the crew journey at 360 px pass in all four. Previous versions weren't tried | Browser run |

**Load times in full** (NFR-06), first visit unless it says otherwise:

| Served | Link | Sign-in page | Crew: next event and call time | Office: events board |
|---|---|---|---|---|
| Uncompressed, as the dev site does today | 5 Mbps | 1.4 s | 1.4 s | 1.9 s |
| Uncompressed, as the dev site does today | 1 Mbps | 5.6 s | 5.1 s | 6.1 s |
| Gzip | 5 Mbps | 0.9 s | 0.9 s | 1.4 s |
| Gzip | 1 Mbps | 2.5 s | 2.5 s | 3.0 s |
| Uncompressed, repeat visit | 1 Mbps | | 1.0 s | 1.0 s |

The whole app is 806 KB of JavaScript, or 271 KB gzipped, split by route so each screen loads only what it
needs. The first chunk every visit needs is 360 KB, or 112 KB gzipped.

**Keyboard only** (Chrome): signing in works without a mouse. "Skip to content" is the first stop and jumps to
the main content. Every stop checked shows a focus ring. Dialogs take focus when they open, keep it however far
you tab, close on Escape and hand focus back to the button that opened them. Cards move from their "Move to…"
menu, and the events board's stage menu opens and lists the allowed moves.

### Not done (B)

- **NFR-01 with five untrained people,** ideally a crew member from Carbon among them.
- **A screen reader pass** with NVDA or VoiceOver. axe checked the accessibility tree and every control has a
  name, but nobody has listened to the boards and forms yet.
- **A real Android phone at 360 px,** and the previous version of each browser.
- **NFR-03 timed with a person** rather than a script.
- **The dev site itself.** These numbers come from a local copy with throttled links standing in for the
  network, so the trip to South Africa North isn't in them.
