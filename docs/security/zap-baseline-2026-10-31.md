# OWASP ZAP baseline scan, 31 Oct 2026

Run against the hosted dev environment in response to Task 2 feedback item F-04.

**Result: 0 failures, 61 rules passed, 7 warnings.**

| | |
|---|---|
| Target | `carbonate-api-dev`, hosted dev environment |
| Scan type | ZAP baseline (`zap-baseline.py`) |
| Tool | `ghcr.io/zaproxy/zaproxy:stable` |
| URLs reached | 10 |
| Run by | D |
| Full report | `zap-report.html` in this folder |

---

## What a baseline scan does, and does not do

A baseline scan spiders the target and analyses the traffic **passively**. It sends no attack
payloads, which is why it is safe to run against a live environment.

**It ran unauthenticated.** The ten URLs reached were the single-page application shell, its static
assets, and the login endpoint. Everything behind authentication, which is almost the entire
application, was not scanned. A clean baseline result is therefore evidence about the public surface
and nothing more. It is reported that way on purpose.

A full active scan, or an authenticated scan with a session token, would cover more and is the
obvious next step for a system going into real use.

---

## Findings and triage

### Acted on

| Finding | Instances | Action |
|---|---|---|
| **Strict-Transport-Security Header Not Set** [10035] | 4 | **Fixed.** `UseHsts()` was skipped because the dev App Service ran with `ASPNETCORE_ENVIRONMENT=Development`. That setting existed only to expose the Swagger explorer. The explorer now has its own flag and the environment variable is removed, so HSTS is sent. Decision D-015 |

### Acted on by the security owner, 1 Nov

| Finding | Instances | Action |
|---|---|---|
| **Cross-Origin-Embedder-Policy missing or invalid** [90004] | 7 | **Partly acted on, and the remainder deliberately accepted.** `Cross-Origin-Opener-Policy: same-origin` and `Cross-Origin-Resource-Policy: same-origin` are now sent. COEP is **not**: `require-corp` would block incident photographs and attachments, which load from the blob storage host without a resource policy header of their own, and `credentialless` is unsupported in Safari. ZAP will continue to report this. Decision D-016 |
| **CSP: Notices** [10055] | 1 | A policy is present and enforced. ZAP has advisory notes on it. Reviewed against the policy in `SecurityHeaders`; no change made |

### Related change in the same pass

`/health/ready` was added, opening a database connection with a five second limit and answering 200
or 503 with no detail in the body. `/health` is unchanged and still queries nothing, because the
availability test and the deployment smoke test call it on a timer and a query there would prevent
the serverless database from ever pausing (D-001, D-012). **Nothing is scheduled against
`/health/ready`**, by design. Decision D-016.

### Accepted, with reasons

| Finding | Instances | Why no action |
|---|---|---|
| **Non-Storable Content** [10049] | 9 | Caching guidance, not a vulnerability. Authenticated API responses and the SPA shell are deliberately not cacheable. Making them storable would be the defect |
| **Re-examine Cache-control Directives** [10015] | 1 | The same class of advice against the application root |
| **Information Disclosure, Suspicious Comments** [10027] | 1 | In a minified third-party bundle, `/assets/index-*.js`. ZAP matches words such as `admin` or `todo` in vendor code. No comment of ours is involved |
| **Modern Web Application** [10109] | 1 | Informational. ZAP is reporting that the target is a single-page application and that a spider may not reach every route. True, and the reason the authenticated surface is out of scope above |

---

## Notable passes

Worth recording, because these are the rules a scanner would be expected to fail on a system that
had not thought about them:

- Content Security Policy header set [10038]
- Anti-clickjacking header present [10020]
- `X-Content-Type-Options` set [10021]
- No cookie missing `HttpOnly` [10010], `Secure` [10011] or `SameSite` [10054]
- No debug error messages or application error disclosure [10023], [90022]
- No sensitive information in URLs or the referrer header [10024], [10025]
- No server or framework version leakage: `Server` [10036], `X-Powered-By` [10037],
  `X-AspNet-Version` [10061]
- No source code disclosure [10099], no directory browsing [10033], no private IP disclosure [2]
- No vulnerable JavaScript libraries detected by Retire.js [10003]
- No session identifier in URL rewriting [3]

---

## Re-running it

The scan is automated in `.github/workflows/zap-baseline.yml`. It can be dispatched manually and
runs weekly, so a regression in a security header is caught without anyone remembering to look. The
workflow wakes the environment first, since the database auto-pauses and a spider against a sleeping
environment finds nothing.

It can also be run locally:

```bash
docker run --rm -v "$(pwd):/zap/wrk/:rw" -t ghcr.io/zaproxy/zaproxy:stable \
  zap-baseline.py -t <dev url> -r zap-report.html -w zap-report.md -I
```
