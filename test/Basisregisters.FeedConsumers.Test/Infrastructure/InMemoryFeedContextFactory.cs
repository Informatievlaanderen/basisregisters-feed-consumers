namespace Basisregisters.FeedConsumers.Test.Infrastructure;

using System;
using Microsoft.EntityFrameworkCore;

public class InMemoryFeedContextFactory : IDbContextFactory<FeedContext>
{
    private readonly DbContextOptions<FeedContext> _options;

    public InMemoryFeedContextFactory(string? databaseName = null)
    {
        _options = new DbContextOptionsBuilder<FeedContext>()
            .UseInMemoryDatabase(databaseName ?? Guid.NewGuid().ToString())
            .Options;
    }

    public FeedContext CreateDbContext()
    {
        return new FeedContext(_options);
    }
}
