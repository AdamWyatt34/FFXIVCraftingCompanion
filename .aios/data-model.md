# Data model — FFXIVCraftingCompanion

Seeded by onboard on 2026-09-11 from `src/FFXIV.Infrastructure/Migrations/AppDbContextModelSnapshot.cs` (static extract, no database contacted). Extract: `.aios/data-model.json`. The data-model pod refreshes this on its schedule; schema drift against a live DB is an attended mode.

## Shape

One context, `AppDbContext`, 26 entities, 243 columns. Six of the entities are ASP.NET Identity tables (`ApplicationUser`, `IdentityRole`, and the four claim/login/role/token joins). The remaining twenty split into three clusters.

**Game reference data** (populated by the XIVAPI sync, keyed by game ids):

- `Item` (ItemId), `ItemSource`, `VendorInfo` (currency item + item), `GatheringInfo`
- `Recipe` (RecipeId, FK to result `Item`), `RecipeIngredient` (composite RecipeId + ItemId), `RecipeLevelEntry` (keyed by RecipeLevel; the rlvl table the engine formulas read)
- `Equipment` (EquipmentId, 34 columns, FK to `Recipe`), `CraftingAction` (keyed by ActionId)
- `SyncMetadata` tracks sync runs

**Per-user data** (FK to `ApplicationUser.UserId`):

- `CharacterProfile` (22 columns, Lodestone import target), `RefreshToken`
- `FavoriteRecipe`, `SavedList`, `CraftPlan`
- `SharedRotation` (18 columns, FK to `Recipe` and user) with `RotationVote`

**Operational**: `AnalyticsEvent`, `Feedback`, both standalone.

## Things worth knowing

- `RecipeLevelEntry` is keyed by recipe level (rlvl), distinct from `ClassJobLevel`. The engine's difficulty/quality formulas depend on this distinction; see commit bb6e8ad.
- `SharedRotation` persists action id sequences. Crafting action ids are therefore part of the stored data contract and must never be renumbered.
- No NodaTime columns; all temporal columns are plain `DateTime`/`DateTimeOffset`.
- Migrations live in a charter no-go zone. Schema changes go through EF migrations with a human.
