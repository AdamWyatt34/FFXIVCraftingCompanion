using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Design;

namespace FFXIV.Infrastructure.Persistence;

// Lets `dotnet ef` target this project on its own, so CI migrations need none of the API's
// configuration (JWT, Discord, CORS) to construct the context.
public sealed class AppDbContextFactory : IDesignTimeDbContextFactory<AppDbContext>
{
    private const string LocalDevConnection =
        "Server=localhost,1433;Database=ffxiv_crafting;User Id=sa;Password=YourStrong!Passw0rd;TrustServerCertificate=true;Encrypt=false";

    public AppDbContext CreateDbContext(string[] args)
    {
        var connectionString =
            Environment.GetEnvironmentVariable("ConnectionStrings__DefaultConnection")
            ?? LocalDevConnection;

        var options = new DbContextOptionsBuilder<AppDbContext>()
            .UseSqlServer(connectionString)
            .Options;

        return new AppDbContext(options);
    }
}
