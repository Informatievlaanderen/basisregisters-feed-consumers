namespace Basisregisters.FeedConsumers.Test.Infrastructure;

using FeedConsumers.Console.Common;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

public class TestFeedProjector : FeedProjectorBase
{
    public TestFeedProjector(
        FeedProjectorOptions options,
        IDbContextFactory<FeedContext> feedContextFactory,
        IFeedPageFetcher feedPageFetcher,
        IJsonSchemaValidator jsonSchemaValidator,
        ILogger logger)
        : base(options, feedContextFactory, feedPageFetcher, jsonSchemaValidator, logger)
    {
        // Register a handler for a known test event type
        When(new BaseRegistriesCloudEventType("test.event.v1"), (_, _, _, _) =>
        {
            return System.Threading.Tasks.Task.CompletedTask;
        });
    }
}
