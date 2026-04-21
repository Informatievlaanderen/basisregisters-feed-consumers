namespace Basisregisters.FeedConsumers.Console.StreetName;

using System;
using Common;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Model;

public sealed class StreetNameProjector : FeedProjectorBase
{
    public StreetNameProjector(
        FeedProjectorOptions options,
        IDbContextFactory<FeedContext> feedContextFactory,
        IFeedPageFetcher feedPageFetcher,
        IJsonSchemaValidator jsonSchemaValidator,
        ILogger logger)
        : base(options, feedContextFactory, feedPageFetcher, jsonSchemaValidator, logger)
    { }

    private static StreetNameStatus MapStatus(string status)
    {
        return status switch
        {
            "voorgesteld" => StreetNameStatus.Proposed,
            "inGebruik" => StreetNameStatus.Current,
            "afgekeurd" => StreetNameStatus.Rejected,
            "gehistoreerd" => StreetNameStatus.Retired,
            _ => throw new ArgumentException($"Unknown streetname status: {status}")
        };
    }
}
