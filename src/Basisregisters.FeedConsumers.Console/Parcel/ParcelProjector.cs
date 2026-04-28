namespace Basisregisters.FeedConsumers.Console.Parcel;

using System;
using Common;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Model;

public sealed class ParcelProjector : FeedProjectorBase
{
    public ParcelProjector(
        FeedProjectorOptions options,
        IDbContextFactory<FeedContext> feedContextFactory,
        IFeedPageFetcher feedPageFetcher,
        IJsonSchemaValidator jsonSchemaValidator,
        ILogger<ParcelProjector> logger)
        : base(options, feedContextFactory, feedPageFetcher, jsonSchemaValidator, logger)
    {

    }

    private static ParcelStatus MapStatus(string status)
    {
        return status switch
        {
            "inGebruik" => ParcelStatus.Current,
            "gehistoreerd" => ParcelStatus.Retired,
            _ => throw new ArgumentException($"Unknown parcel status: {status}")
        };
    }
}
