namespace Basisregisters.FeedConsumers.Console.BuildingUnit;

using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.Json;
using System.Threading.Tasks;
using Common;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Model;
using NetTopologySuite.Geometries;
using NetTopologySuite.IO.GML2;

public sealed class BuildingUnitProjector : FeedProjectorBase
{
    public static readonly BaseRegistriesCloudEventType CreateEvent = new("basisregisters.buildingunit.create.v1");
    public static readonly BaseRegistriesCloudEventType UpdateEvent = new("basisregisters.buildingunit.update.v1");
    public static readonly BaseRegistriesCloudEventType DeleteEvent = new("basisregisters.buildingunit.delete.v1");

    private readonly GMLReader _gmlReader = GmlReaderFactory.CreateLambert2008GmlReader();

    public BuildingUnitProjector(
        FeedProjectorOptions options,
        IDbContextFactory<FeedContext> feedContextFactory,
        IFeedPageFetcher feedPageFetcher,
        IJsonSchemaValidator jsonSchemaValidator,
        ILoggerFactory loggerFactory)
        : base(options, feedContextFactory, feedPageFetcher, jsonSchemaValidator, loggerFactory.CreateLogger<BuildingUnitProjector>())
    {
        Logger.LogInformation("Starting BuildingUnitProjector");

        When(CreateEvent, async (cloudEvent, data, context, cancellationToken) =>
        {
            Logger.LogInformation("Processing create event: {EventId}", cloudEvent.Id);
            var buildingUnit = new Model.BuildingUnit(
                data.Id.ToString(),
                int.Parse(data.ObjectId),
                data.Attributen.GetRequired(BuildingUnitAttributes.BuildingId).NieuweWaarde!.ToString()!.ExtractPersistentLocalIdAsInt(),
                MapStatus(data.Attributen.GetRequired(BuildingUnitAttributes.Status).NieuweWaarde!.ToString()!),
                ExtractLambert2008Geometry(data.Attributen.GetRequired(BuildingUnitAttributes.Position).NieuweWaarde),
                MapGeometryMethod(data.Attributen.GetRequired(BuildingUnitAttributes.GeometryMethod).NieuweWaarde!.ToString()!),
                MapFunction(data.Attributen.GetRequired(BuildingUnitAttributes.BuildingUnitFunction).NieuweWaarde!.ToString()!),
                data.Attributen.GetRequired(BuildingUnitAttributes.HasDeviation).NieuweWaarde!.ToBoolean(),
                data.VersieId);

            ProcessBuildingUnitAttributes(data, buildingUnit, context, cancellationToken);

            await context.BuildingUnits.AddAsync(buildingUnit, cancellationToken);
        });

        When(UpdateEvent, async (cloudEvent, data, context, cancellationToken) =>
        {
            Logger.LogInformation("Processing update event: {EventId}", cloudEvent.Id);
            var buildingUnit = await context.BuildingUnits.FindAsync([data.Id.ToString()], cancellationToken: cancellationToken);
            if (buildingUnit == null)
                throw new InvalidOperationException($"BuildingUnit {data.Id} not found");

            ProcessBuildingUnitAttributes(data, buildingUnit, context, cancellationToken);
        });

        When(DeleteEvent, async (cloudEvent, data, context, cancellationToken) =>
        {
            Logger.LogInformation("Processing delete event: {EventId}", cloudEvent.Id);
            var buildingUnit = await context.BuildingUnits.FindAsync([data.Id.ToString()], cancellationToken: cancellationToken);
            if (buildingUnit == null)
                throw new InvalidOperationException($"BuildingUnit {data.Id} not found");

            buildingUnit.IsRemoved = true;
        });
    }

    private void ProcessBuildingUnitAttributes(CloudEventData data, Model.BuildingUnit buildingUnit, FeedContext context, CancellationToken cancellationToken)
    {
        buildingUnit.VersionId = data.VersieId;
        foreach (var attribute in data.Attributen)
        {
            switch (attribute.Naam)
            {
                case BuildingUnitAttributes.Status:
                    buildingUnit.Status = MapStatus(attribute.NieuweWaarde!.ToString()!);
                    break;

                case BuildingUnitAttributes.BuildingId:
                    buildingUnit.BuildingPersistentLocalId = attribute.NieuweWaarde!.ToString()!.ExtractPersistentLocalIdAsInt();
                    break;

                case BuildingUnitAttributes.BuildingUnitFunction:
                    buildingUnit.Function = MapFunction(attribute.NieuweWaarde!.ToString()!);
                    break;

                case BuildingUnitAttributes.GeometryMethod:
                    buildingUnit.GeometryMethod = MapGeometryMethod(attribute.NieuweWaarde!.ToString()!);
                    break;

                case BuildingUnitAttributes.Position:
                    buildingUnit.Position = ExtractLambert2008Geometry(attribute.NieuweWaarde);
                    break;

                case BuildingUnitAttributes.HasDeviation:
                    buildingUnit.HasDeviation = attribute.NieuweWaarde!.ToBoolean();
                    break;

                case BuildingUnitAttributes.AddressIds:
                    SyncAddressesAsync(buildingUnit.PersistentLocalId, attribute.NieuweWaarde, context, cancellationToken)
                        .GetAwaiter()
                        .GetResult();
                    break;

                default:
                    throw new InvalidOperationException($"Unknown building unit attribute '{attribute.Naam}' for building unit {buildingUnit.PersistentLocalId} ({buildingUnit.PersistentUri})");
            }
        }
    }

    private static BuildingUnitStatus MapStatus(string status)
    {
        return status switch
        {
            "gepland" => BuildingUnitStatus.Planned,
            "nietGerealiseerd" => BuildingUnitStatus.NotRealized,
            "gerealiseerd" => BuildingUnitStatus.Realized,
            "gehistoreerd" => BuildingUnitStatus.Retired,
            _ => throw new ArgumentException($"Unknown buildingunit status: {status}")
        };
    }

    private Geometry ExtractLambert2008Geometry(object? geometry)
    {
        var geometries = geometry is JsonElement geometryElement
            ? geometryElement.Deserialize<List<GeometryData>>(CloudEventReader.JsonOptions)
            : [];

        var geometryData = geometries?
            .FirstOrDefault(x => x.IsLambert2008);

        if (geometryData is null)
            throw new ArgumentException("Building geometry must contain Lambert 2008 (EPSG:3812) geometry.");

        return MapGeometry(geometryData);
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

    private static BuildingUnitGeometryMethod MapGeometryMethod(string geometryMethod)
    {
        return geometryMethod switch
        {
            "aangeduidDoorBeheerder" => BuildingUnitGeometryMethod.AppointedByAdministrator,
            "afgeleidVanObject" => BuildingUnitGeometryMethod.DerivedFromObject,
            "geïnterpoleerd" => BuildingUnitGeometryMethod.Interpolated,
            _ => throw new ArgumentException($"Unknown building unit geometry method: {geometryMethod}")
        };
    }

    private static BuildingUnitFunction MapFunction(string function)
    {
        return function switch
        {
            "nietGekend" => BuildingUnitFunction.Unknown,
            "gemeenschappelijkDeel" => BuildingUnitFunction.Common,
            _ => throw new ArgumentException($"Unknown building unit function: {function}")
        };
    }

    private async Task SyncAddressesAsync(
        int buildingUnitPersistentLocalId,
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

        var existingAddresses = context.BuildingUnitAddresses.Local
            .Where(x => x.BuildingUnitPersistentLocalId == buildingUnitPersistentLocalId)
            .Concat(await context.BuildingUnitAddresses
                .Where(x => x.BuildingUnitPersistentLocalId == buildingUnitPersistentLocalId)
                .ToListAsync(cancellationToken))
            .GroupBy(x => new { x.BuildingUnitPersistentLocalId, x.AddressPersistentLocalId })
            .Select(x => x.First())
            .ToList();

        foreach (var existingAddress in existingAddresses.Where(x => !updatedAddressPersistentLocalIds.Contains(x.AddressPersistentLocalId)))
            context.BuildingUnitAddresses.Remove(existingAddress);

        var existingAddressPersistentLocalIds = existingAddresses
            .Select(x => x.AddressPersistentLocalId)
            .ToHashSet();

        foreach (var addressPersistentLocalId in updatedAddressPersistentLocalIds.Where(x => !existingAddressPersistentLocalIds.Contains(x)))
        {
            await context.BuildingUnitAddresses.AddAsync(
                new BuildingUnitAddress(buildingUnitPersistentLocalId, addressPersistentLocalId),
                cancellationToken);
        }
    }
}
