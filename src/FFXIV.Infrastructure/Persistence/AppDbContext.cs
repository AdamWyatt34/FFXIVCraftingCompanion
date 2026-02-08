using FFXIV.Domain.Entities;
using Microsoft.EntityFrameworkCore;

namespace FFXIV.Infrastructure.Persistence;

public class AppDbContext : DbContext
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

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.ApplyConfigurationsFromAssembly(typeof(AppDbContext).Assembly);
    }
}
