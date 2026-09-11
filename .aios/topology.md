# Topology — FFXIVCraftingCompanion

Seeded by onboard on 2026-09-11 (static extract, `.aios/topology.json`). Charter no-go zones were excluded from the scan. The topology pod refreshes this on its schedule.

## Integration edges

| Kind | Target | Notes |
|---|---|---|
| database | SQL Server via `AppDbContext` | Must be supplied; Azure SQL in production, docker locally |
| cache | Redis | External; production runs in-memory caching instead |
| http-client | unresolved (node) | The frontend's server-side API calls; the extractor cannot resolve the target host statically |

The three outbound HTTP clients in Infrastructure (XIVAPI, Universalis, Lodestone via NetStone) are not surfaced as edges by the extractor. That is a scanner gap, not an absence.

## Inbound API surface

Sixteen controller prefixes under `/api/`. Auth by prefix:

- **Authenticated by attribute**: `/api/favorites`, `/api/notifications`, `/api/profile`, `/api/saved-lists`
- **Default policy**: `/api/admin`, `/api/analytics`, `/api/auth`, `/api/crafting`, `/api/craft-plans`, `/api/feedback`, `/api/gathering`, `/api/gear`, `/api/items`, `/api/market`, `/api/recipes`, `/api/rotations`
- **Explicitly anonymous** (registered as `topology-public-endpoint` findings):
  - `POST /api/craft-plans/resolve`
  - `POST /api/craft-plans/export`
  - `GET /api/craft-plans/{id}/resolved`
  - `POST /api/craft-plans/{id}/cost`
  - `GET /api/profile/lodestone-preview`

The five anonymous endpoints look intentional: craft plans are shareable, and the Lodestone preview runs before a user links a character. They stay open as findings until a human dismisses them with that reason.

## Request path

Controller → MediatR handler in `FFXIV.Application` → domain engine (`FFXIV.Domain/CraftingEngine`) or EF repository / external client in `FFXIV.Infrastructure` → DTO. Solver jobs (`POST /api/crafting/solve`) run through `SolverJobService` and are polled by job id.
