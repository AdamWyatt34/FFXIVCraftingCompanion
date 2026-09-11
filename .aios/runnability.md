# Runnability — FFXIVCraftingCompanion

Seeded by onboard on 2026-09-11 (static inventory, `.aios/runnability.json`; composes `.aios/topology.json`). Config was read for key names only. No values were read or echoed, and the app was not booted. The `check` boot mode is attended and was not run.

## To run locally you need

1. **Toolchain**: .NET 10 SDK (`global.json` pins 10.0.100, roll-forward latest feature) and Node 22 (`frontend/.nvmrc`).
2. **SQL Server** reachable through `ConnectionStrings:DefaultConnection` (must supply; `docker compose up -d` provides SQL Server 2022 on 1433).
3. **Redis** on 6379 for local caching (same compose file). Production uses in-memory caching, so Redis is external and optional there.
4. Then: `cd src/FFXIV.Api && dotnet run` (http://localhost:5159) and `cd frontend && npm ci && npm run dev` (http://localhost:3000). First run needs `POST /api/admin/sync` to load game data.

## Config inventory

| Key | Class | Reason | Secret |
|---|---|---|---|
| `ConnectionStrings:DefaultConnection` | required | consumed without a guard | yes |
| `Discord:ClientSecret` | probably required | empty secret-shaped key, must be supplied | yes |
| `Sentry:Enabled` | probably required | `GetValue` without a default | no |
| `ApplyMigrationsOnStartup` | probably required | `GetValue` without a default, not in config files | no |
| `Sentry:Dsn` | optional | code default | no |
| `Sentry:TracesSampleRate` | optional | `GetValue` with a default | no |

Dead config: none found. Unresolved config reads: none.

## Secrets by name

- `ConnectionStrings:DefaultConnection` (connection string)
- `Discord:ClientSecret` (environment)

`TESTING_GUIDE.md` Part 2 walks through registering the Discord OAuth app and the other external services. Supply secrets through user-secrets or environment variables, never by editing `appsettings.Production.json` (a charter no-go zone).

## Limitations of this pass

- The extractor found no launch profile ports, so the ports above come from `TESTING_GUIDE.md`.
- XIVAPI, Universalis, and Lodestone are outbound dependencies the topology scan did not surface. A full data sync needs XIVAPI reachable.
