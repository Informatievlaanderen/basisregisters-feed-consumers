namespace Basisregisters.FeedConsumers.Console.Address;

using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.Json;
using System.Threading.Tasks;
using Common;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Model;
using NetTopologySuite.IO.GML2;
using Geometry = NetTopologySuite.Geometries.Geometry;

public sealed class AddressProjector : FeedProjectorBase
{
    public static readonly BaseRegistriesCloudEventType CreateEvent = new("basisregisters.address.create.v1");
    public static readonly BaseRegistriesCloudEventType UpdateEvent = new("basisregisters.address.update.v1");
    public static readonly BaseRegistriesCloudEventType DeleteEvent = new("basisregisters.address.delete.v1");
    public static readonly BaseRegistriesCloudEventType TransformEvent = new("basisregisters.address.transform.v1");

    private readonly GMLReader _gmlReader = GmlReaderFactory.CreateLambert2008GmlReader();

    public AddressProjector(
        FeedProjectorOptions options,
        IDbContextFactory<FeedContext> feedContextFactory,
        IFeedPageFetcher feedPageFetcher,
        IJsonSchemaValidator jsonSchemaValidator,
        ILoggerFactory loggerFactory)
        : base(options, feedContextFactory, feedPageFetcher, jsonSchemaValidator, loggerFactory.CreateLogger<AddressProjector>())
    {
        Logger.LogInformation("Starting AddressProjector");

        When(CreateEvent, async (cloudEvent, data, context, cancellationToken) =>
        {
            Logger.LogInformation("Processing create event: {EventId}", cloudEvent.Id);
            var address = new Address
            {
                PersistentUri = data.Id.ToString(),
                PersistentLocalId = int.Parse(data.ObjectId),
                StreetNamePersistentLocalId = ExtractPersistentLocalId(data.Attributen.GetRequired(AddressAttributes.StreetNameId).NieuweWaarde.ToString()!),
                Status = MapStatus(data.Attributen.GetRequired(AddressAttributes.Status).NieuweWaarde.ToString()!),
                HouseNumber = data.Attributen.GetRequired(AddressAttributes.HouseNumber).NieuweWaarde.ToString()!,
                PostalCode = data.Attributen.GetRequired(AddressAttributes.PostalCode).NieuweWaarde.ToString()!,
                OfficiallyAssigned = MapBoolean(data.Attributen.GetRequired(AddressAttributes.OfficiallyAssigned).NieuweWaarde),
                PositionMethod = MapGeometryMethod(data.Attributen.GetRequired(AddressAttributes.PositionGeometryMethod).NieuweWaarde.ToString()!),
                PositionSpecification = MapPositionSpecification(data.Attributen.GetRequired(AddressAttributes.PositionSpecification).NieuweWaarde.ToString()!),
                IsRemoved = false,
                VersionId = data.VersieId
            };

            ProcessAddressAttributes(data, address);

            await context.Addresses.AddAsync(address, cancellationToken);
        });

        When(UpdateEvent, async (cloudEvent, data, context, cancellationToken) =>
        {
            Logger.LogInformation("Processing update event: {EventId}", cloudEvent.Id);
            var address = await context.Addresses.FindAsync([data.Id.ToString()], cancellationToken: cancellationToken);
            if (address == null)
                throw new InvalidOperationException($"Address {data.Id} not found");

            ProcessAddressAttributes(data, address);
        });

        When(DeleteEvent, async (cloudEvent, data, context, cancellationToken) =>
        {
            Logger.LogInformation("Processing delete event: {EventId}", cloudEvent.Id);
            var address = await context.Addresses.FindAsync([data.Id.ToString()], cancellationToken: cancellationToken);
            if (address == null)
                throw new InvalidOperationException($"Address {data.Id} not found");

            address.IsRemoved = true;
        });

        When(TransformEvent, (_, _, _, _) =>
        {
            Logger.LogInformation("Ignoring transform event");
            return Task.CompletedTask;
        });
    }

    private void ProcessAddressAttributes(CloudEventData data, Address address)
    {
        address.VersionId = data.VersieId;
        foreach (var attribute in data.Attributen)
        {
            switch (attribute.Naam)
            {
                case AddressAttributes.Status:
                    address.Status = MapStatus(attribute.NieuweWaarde.ToString()!);
                    break;

                case AddressAttributes.StreetNameId:
                    address.StreetNamePersistentLocalId = ExtractPersistentLocalId(attribute.NieuweWaarde.ToString()!);
                    break;

                case AddressAttributes.HouseNumber:
                    address.HouseNumber = attribute.NieuweWaarde.ToString()!;
                    break;

                case AddressAttributes.BoxNumber:
                    address.BoxNumber = attribute.NieuweWaarde?.ToString();
                    break;

                case AddressAttributes.PostalCode:
                    address.PostalCode = attribute.NieuweWaarde.ToString()!;
                    break;

                case AddressAttributes.OfficiallyAssigned:
                    address.OfficiallyAssigned = MapBoolean(attribute.NieuweWaarde);
                    break;

                case AddressAttributes.Position:
                    var geometries = attribute.NieuweWaarde is JsonElement positionElement
                        ? positionElement.Deserialize<List<GeometryData>>(CloudEventReader.JsonOptions)
                        : [];

                    var geometryData = geometries?
                        .FirstOrDefault(x => x.IsLambert2008)
                        ?? geometries?.FirstOrDefault();

                    if (geometryData is not null)
                        address.Geometry = MapGeometry(geometryData);
                    break;

                case AddressAttributes.PositionGeometryMethod:
                    address.PositionMethod = MapGeometryMethod(attribute.NieuweWaarde.ToString()!);
                    break;

                case AddressAttributes.PositionSpecification:
                    address.PositionSpecification = MapPositionSpecification(attribute.NieuweWaarde.ToString()!);
                    break;

                default:
                    throw new InvalidOperationException($"Unknown address attribute: {attribute.Naam}");
            }
        }
    }

    private static AddressStatus MapStatus(string status)
    {
        return status switch
        {
            "voorgesteld" => AddressStatus.Proposed,
            "inGebruik" => AddressStatus.Current,
            "afgekeurd" => AddressStatus.Rejected,
            "gehistoreerd" => AddressStatus.Retired,
            _ => throw new ArgumentException($"Unknown address status: {status}")
        };
    }

    private Geometry MapGeometry(GeometryData geometryData)
    {
        if (string.IsNullOrEmpty(geometryData.Gml))
            throw new ArgumentException($"Failed to parse GML geometry: GML string is null or empty");

        try
        {
            return _gmlReader.Read(geometryData.Gml);
        }
        catch (Exception ex)
        {
            throw new ArgumentException($"Failed to parse GML geometry: {ex.Message}", ex);
        }
    }

    private static AddressPositionGeometryMethod MapGeometryMethod(string geometryMethod)
    {
        return geometryMethod switch
        {
            "aangeduidDoorBeheerder" => AddressPositionGeometryMethod.AppointedByAdministrator,
            "afgeleidVanObject" => AddressPositionGeometryMethod.DerivedFromObject,
            "geïnterpoleerd" => AddressPositionGeometryMethod.Interpolated,
            _ => throw new ArgumentException($"Unknown geometry method: {geometryMethod}")
        };
    }

    private static AddressPositionSpecification MapPositionSpecification(string positionSpecification)
    {
        return positionSpecification switch
        {
            "gemeente" => AddressPositionSpecification.Municipality,
            "straat" => AddressPositionSpecification.Street,
            "perceel" => AddressPositionSpecification.Parcel,
            "lot" => AddressPositionSpecification.Lot,
            "standplaats" => AddressPositionSpecification.Stand,
            "ligplaats" => AddressPositionSpecification.Berth,
            "gebouw" => AddressPositionSpecification.Building,
            "gebouweenheid" => AddressPositionSpecification.BuildingUnit,
            "ingang" => AddressPositionSpecification.Entry,
            "wegsegment" => AddressPositionSpecification.RoadSegment,
            _ => throw new ArgumentException($"Unknown position specification: {positionSpecification}")
        };
    }

    private static int ExtractPersistentLocalId(string persistentUri)
    {
        var lastSlashIndex = persistentUri.LastIndexOf('/');
        var persistentLocalId = lastSlashIndex >= 0
            ? persistentUri[(lastSlashIndex + 1)..]
            : persistentUri;

        return int.Parse(persistentLocalId);
    }

    private static bool MapBoolean(object value)
    {
        return value switch
        {
            bool boolean => boolean,
            JsonElement jsonElement when jsonElement.ValueKind == JsonValueKind.True || jsonElement.ValueKind == JsonValueKind.False => jsonElement.GetBoolean(),
            _ => bool.Parse(value.ToString()!)
        };
    }
}
