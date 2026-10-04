# Carbonate web app

The React single-page app for Carbonate. It calls the API on the relative path `/api`, so it has to be
served from the same site as the API: in production the API serves the built files from `wwwroot`, and in
development Vite proxies `/api` through to the API.

Setting up the whole system (database, API, secrets) is in the main README. This file only covers the SPA.

## Scripts

| Command | What it does |
|---|---|
| `npm run dev` | Dev server on http://localhost:5173, with `/api` proxied to the API |
| `npm run dev:mocks` | The same, but with a mocked API running in the browser, so no backend is needed |
| `npm run test` | Component and accessibility tests, once (`npm run test:watch` to keep them running) |
| `npm run lint` | ESLint, including the XSS rules (`react/no-danger`, `no-unsanitized`) |
| `npm run typecheck` | TypeScript in strict mode |
| `npm run build` | Production build into `dist/` |
| `npm run gen:api` | Regenerates the API types from `docs/api/openapi.json` |

Node 22.22 or newer is needed. The proxy target in `vite.config.ts` should match the HTTPS port in the
API's `launchSettings.json`.

## Working without the API

`npm run dev:mocks` swaps the API for [MSW](https://mswjs.io) in the browser, so screens can be built and
shown before their endpoints exist. Log in with any of these demo accounts. The password for all of them is
`demo`.

| Email | Role | Notes |
|---|---|---|
| director@example.com | Director | Asks for a code: enter `123456` |
| sarah@example.com | Event Manager | |
| ops@example.com | Operations Manager | |
| bookkeeper@example.com | Accounts | Shows first-time code setup, then enter `123456` |
| thabo@example.com | Crew Lead | Crew layout |
| priya@example.com | Casual Crew | Crew layout |

The mock keeps its session in memory, so a full page reload signs you out. That's deliberate: nothing
session-related is written to browser storage, even in mock mode.

**Mocks never ship.** The mock code is only loaded in mocks mode, and the production build removes both
that code and the `mockServiceWorker.js` file.

## Where things live

| Folder | Contents |
|---|---|
| `src/app` | Router, providers, layouts (desk and crew), route guards, navigation by permission |
| `src/auth` | Session, permissions, login and two-step sign-in |
| `src/api` | `apiFetch` (in-memory access token, single-flight refresh), problem details, API types |
| `src/components` | Shared UI: buttons, fields, page head, alerts, empty and error states |
| `src/features` | One folder per area: events, admin tasks, stock, finance, calendar, settings, crew |
| `src/styles` | Design tokens, base styles and breakpoints |
| `src/test` | Test setup, the mock API, fixtures and render helpers |
