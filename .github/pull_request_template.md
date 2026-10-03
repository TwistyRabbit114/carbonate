<!-- Title format: FR-xx: short description -->

## What and why

Closes FR-__ / US-__

<!-- One or two sentences. What business rule does this implement? -->

## Definition of done (project plan section 13)

- [ ] Does one thing; the commit message names the FR or US
- [ ] Business rules have unit tests
- [ ] Endpoints have integration tests: happy path, 400 validation, 403 without permission, 404 when not visible
- [ ] CI is green and it deployed to the dev environment
- [ ] `docs/traceability.md` row updated (endpoint, screen, tests, status)
- [ ] No secrets, no `console.log` of data, no `TODO` without `(plan)` and a note below

## Only if it applies

- [ ] **Returns a money field** — tagged with `[FinancialField(...)]`, masked in the service, masking-suite rows added. **C must approve.**
- [ ] **Adds an endpoint** — has a permission attribute, object-level checks, and is in `docs/api/openapi.json`
- [ ] **Writes data** — calls `IAuditService`
- [ ] **Changes the schema** — one migration, rebased on `dev`. **C must approve.**
- [ ] **Touches `.github/` or infrastructure** — **D must approve.**
- [ ] **Adds UI** — loading, empty and error states; works at 360 px if crew-facing; passes axe; uses client vocabulary (pack size, load-in, strike, recce, costing)
- [ ] **Adds a package** — named below, with the reason

## Open questions for the plan

<!-- List any // TODO(plan): comments you left, or delete this section. -->
