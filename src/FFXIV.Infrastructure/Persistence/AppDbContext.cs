using FFXIV.Domain.Entities;
using FFXIV.Infrastructure.Identity;
using Microsoft.AspNetCore.Identity.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore;

namespace FFXIV.Infrastructure.Persistence;

public class AppDbContext : IdentityDbContext<ApplicationUser>
{
    public AppDbContext(DbContextOptions<AppDbContext> options) : base(options) { }

    public DbSet<Item> Items => Set<Item>();
    public DbSet<Recipe> Recipes => Set<Recipe>();
    public DbSet<RecipeIngredient> RecipeIngredients => Set<RecipeIngredient>();
    public DbSet<Equipment> Equipment => Set<Equipment>();
    public DbSet<CraftingAction> CraftingActions => Set<CraftingAction>();
    public DbSet<SyncMetadata> SyncMetadata => Set<SyncMetadata>();
    public DbSet<ItemSource> ItemSources => Set<ItemSource>();
    public DbSet<GatheringInfo> GatheringInfos => Set<GatheringInfo>();
    public DbSet<VendorInfo> VendorInfos => Set<VendorInfo>();
    public DbSet<RefreshToken> RefreshTokens => Set<RefreshToken>();
    public DbSet<CharacterProfile> CharacterProfiles => Set<CharacterProfile>();
    public DbSet<SavedList> SavedLists => Set<SavedList>();
    public DbSet<FavoriteRecipe> FavoriteRecipes => Set<FavoriteRecipe>();
    public DbSet<RecipeLevelEntry> RecipeLevelEntries => Set<RecipeLevelEntry>();
    public DbSet<ConsumableBonus> ConsumableBonuses => Set<ConsumableBonus>();
    public DbSet<SharedRotation> SharedRotations => Set<SharedRotation>();
    public DbSet<RotationVote> RotationVotes => Set<RotationVote>();
    public DbSet<CraftPlan> CraftPlans => Set<CraftPlan>();
    public DbSet<AnalyticsEvent> AnalyticsEvents => Set<AnalyticsEvent>();
    public DbSet<Feedback> Feedbacks => Set<Feedback>();

    protected override void OnModelCreating(ModelBuilder builder)
    {
        base.OnModelCreating(builder);
        builder.ApplyConfigurationsFromAssembly(typeof(AppDbContext).Assembly);
    }
}
