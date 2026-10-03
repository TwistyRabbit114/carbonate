# Carbonate — agent instructions

For Codex, Cursor, Copilot and any agent that does not read `CLAUDE.md`.

**Read `CLAUDE.md` and `docs/PROJECT_PLAN.md` before writing any code.** They hold the full rules;
this file only exists so agents that look for `AGENTS.md` find their way there.

The short version:

- The human tells you which module and FR ids you are on. Stay inside those folders.
- Never add `[AllowAnonymous]`, skip a permission check, return an entity instead of a DTO, add an
  untagged money field, store tokens in browser storage, or log personal data.
- Money fields are **omitted from JSON when masked**, not sent as null.
- Tests ship with the code: unit tests for business rules, integration tests for endpoints covering
  happy path, 400, 403 and 404.
- Prefer the boring option — this is a 15-user internal system.
- Secrets go in user-secrets locally and Key Vault in Azure, never in the repo.
- Ambiguity gets `// TODO(plan): <question>` and a note in the PR, never an invented requirement.
- Commits: `FR-xx: short description`. Branches: `feature/FR-xx-short-name`.
