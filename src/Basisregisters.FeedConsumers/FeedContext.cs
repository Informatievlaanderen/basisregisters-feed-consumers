namespace Basisregisters.FeedConsumers;

using Microsoft.EntityFrameworkCore;
using Model;

public class FeedContext : DbContext
{
    public const string MigrationsTableName = "__EFMigrationsHistoryChangeFeed";
    public const string Schema = "changefeed";

    public DbSet<FeedState> FeedStates => Set<FeedState>();
    public DbSet<Municipality> Municipalities => Set<Municipality>();
    public DbSet<PostalInformation> PostalInformations => Set<PostalInformation>();
    public DbSet<StreetName> StreetNames => Set<StreetName>();
    public DbSet<Address> Addresses => Set<Address>();

    public DbSet<Building> Buildings => Set<Building>();

    public FeedContext(DbContextOptions<FeedContext> options) : base(options) { }

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        base.OnModelCreating(modelBuilder);
        modelBuilder.ApplyConfigurationsFromAssembly(typeof(FeedContext).Assembly);
    }
}
