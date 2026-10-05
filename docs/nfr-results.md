# Non-functional results (role C)

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

## Security checks run (over HTTP, against a real database)

- Forged access tokens: extra permissions added without re-signing, and a token with its signature removed, are both 401. The MFA-step token cannot be used as a sign-in.
- Hostile text (script tags, event handlers, SQL fragments) in an event name is stored as plain text and returned as JSON with `X-Content-Type-Options: nosniff`; the table survives the SQL fragment. Card descriptions go through the strict allowlist sanitiser (B).
- Security headers and the content security policy are asserted on every response; a CSP violation report endpoint is in place. A headless browser run against the hosted dev site showed one remaining violation (a library probing `eval`), which is blocked and harmless.
- Every endpoint declares its authorisation, and a test fails if one does not; the only anonymous routes are login, MFA verify, refresh, CSP report and health.

## Not done

- **OWASP ZAP baseline scan:** not run (D).
- **Row-Level Security** (plan stretch): not built. Object-level access is enforced in the queries (404 for an event the caller cannot see) instead.
- **NFR-09 on the hosted database:** the figures above are from a local container. The hosted serverless database is slower on its first request after idle.
- **Password reset:** there is no password reset endpoint yet.
