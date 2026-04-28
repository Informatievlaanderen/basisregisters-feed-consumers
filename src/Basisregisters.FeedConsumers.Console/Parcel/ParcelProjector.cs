namespace Basisregisters.FeedConsumers.Console.Parcel;

using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using Common;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Model;

public sealed class ParcelProjector : FeedProjectorBase
{
    public static readonly BaseRegistriesCloudEventType CreateEvent = new("basisregisters.parcel.create.v1");
    public static readonly BaseRegistriesCloudEventType UpdateEvent = new("basisregisters.parcel.update.v1");

    public ParcelProjector(
        FeedProjectorOptions options,
        IDbContextFactory<FeedContext> feedContextFactory,
        IFeedPageFetcher feedPageFetcher,
        IJsonSchemaValidator jsonSchemaValidator,
        ILoggerFactory loggerFactory)
        : base(options, feedContextFactory, feedPageFetcher, jsonSchemaValidator, loggerFactory.CreateLogger<ParcelProjector>())
    {
        Logger.LogInformation("Starting ParcelProjector");

        When(CreateEvent, async (cloudEvent, data, context, cancellationToken) =>
        {
            Logger.LogInformation("Processing create event: {EventId}", cloudEvent.Id);
            var parcel = new Model.Parcel(
                data.Id.ToString(),
                data.ObjectId,
                data.ObjectId,
                MapStatus(data.Attributen.GetRequired(ParcelAttributes.Status).NieuweWaarde!.ToString()!),
                data.VersieId);

            await ProcessParcelAttributes(data, parcel, context, cancellationToken);

            await context.Parcels.AddAsync(parcel, cancellationToken);
        });

        When(UpdateEvent, async (cloudEvent, data, context, cancellationToken) =>
        {
            Logger.LogInformation("Processing update event: {EventId}", cloudEvent.Id);
            var parcel = await context.Parcels.FindAsync([data.Id.ToString()], cancellationToken: cancellationToken);
            if (parcel == null)
                throw new InvalidOperationException($"Parcel {data.Id} not found");

            await ProcessParcelAttributes(data, parcel, context, cancellationToken);
        });
    }

    private async Task ProcessParcelAttributes(
        CloudEventData data,
        Model.Parcel parcel,
        FeedContext context,
        CancellationToken cancellationToken)
    {
        parcel.VersionId = data.VersieId;
        foreach (var attribute in data.Attributen)
        {
            switch (attribute.Naam)
            {
                case ParcelAttributes.Status:
                    parcel.Status = MapStatus(attribute.NieuweWaarde!.ToString()!);
                    break;

                case ParcelAttributes.AddressIds:
                    await SyncAddressesAsync(parcel.VbrCaPaKey, attribute.NieuweWaarde, context, cancellationToken);
                    break;

                default:
                    throw new InvalidOperationException($"Unknown parcel attribute '{attribute.Naam}' for parcel {parcel.VbrCaPaKey} ({parcel.PersistentUri})");
            }
        }
    }

    private async Task SyncAddressesAsync(
        string vbrCaPaKey,
        object? addressIds,
        FeedContext context,
        CancellationToken cancellationToken)
    {
        var updatedAddressIds = addressIds is JsonElement addressIdsElement
            ? addressIdsElement.Deserialize<List<string>>(CloudEventReader.JsonOptions) ?? []
            : [];

        var updatedAddressPersistentLocalIds = updatedAddressIds
            .Select(addressId => addressId.ExtractPersistentLocalIdAsInt())
            .ToHashSet();

        var existingAddresses = (await context.ParcelAddresses
                .Where(x => x.VbrCaPaKey == vbrCaPaKey)
                .ToListAsync(cancellationToken))
            .UnionBy(
                context.ParcelAddresses.Local.Where(x => x.VbrCaPaKey == vbrCaPaKey),
                x => (x.VbrCaPaKey, x.AddressPersistentLocalId))
            .ToList();

        foreach (var existingAddress in existingAddresses.Where(x => !updatedAddressPersistentLocalIds.Contains(x.AddressPersistentLocalId)))
            context.ParcelAddresses.Remove(existingAddress);

        var existingAddressPersistentLocalIds = existingAddresses
            .Select(x => x.AddressPersistentLocalId)
            .ToHashSet();

        foreach (var addressPersistentLocalId in updatedAddressPersistentLocalIds.Where(x => !existingAddressPersistentLocalIds.Contains(x)))
        {
            await context.ParcelAddresses.AddAsync(
                new ParcelAddress(vbrCaPaKey, addressPersistentLocalId),
                cancellationToken);
        }
    }

    private static ParcelStatus MapStatus(string status)
    {
        return status switch
        {
            "gerealiseerd" => ParcelStatus.Current,
            "gehistoreerd" => ParcelStatus.Retired,
            _ => throw new ArgumentException($"Unknown parcel status: {status}")
        };
    }
}
