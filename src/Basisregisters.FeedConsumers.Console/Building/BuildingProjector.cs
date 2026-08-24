namespace Basisregisters.FeedConsumers.Console.Building;

using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.Json;
using Common;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Model;
using NetTopologySuite.Geometries;
using NetTopologySuite.IO.GML2;

public sealed class BuildingProjector : FeedProjectorBase
{
    public static readonly BaseRegistriesCloudEventType CreateEvent = new("basisregisters.building.create.v1");
    public static readonly BaseRegistriesCloudEventType UpdateEvent = new("basisregisters.building.update.v1");
    public static readonly BaseRegistriesCloudEventType DeleteEvent = new("basisregisters.building.delete.v1");

    private const string StatusPuriPlanned = "https://data.vlaanderen.be/id/concept/gebouwstatus/gepland";
    private const string StatusPuriUnderConstruction = "https://data.vlaanderen.be/id/concept/gebouwstatus/inAanbouw";
    private const string StatusPuriNotRealized = "https://data.vlaanderen.be/id/concept/gebouwstatus/nietGerealiseerd";
    private const string StatusPuriRealized = "https://data.vlaanderen.be/id/concept/gebouwstatus/gerealiseerd";
    private const string StatusPuriRetired = "https://data.vlaanderen.be/id/concept/gebouwstatus/gehistoreerd";

    private const string GeometryMethodPuriOutlined = "https://data.vlaanderen.be/id/concept/2Dgeometriemethode/ingeschetst";
    private const string GeometryMethodPuriMeasuredByGrb = "https://data.vlaanderen.be/id/concept/2Dgeometriemethode/ingemetenGRB";

    private readonly GMLReader _gmlReader = GmlReaderFactory.CreateLambert2008GmlReader();

    public BuildingProjector(
        FeedProjectorOptions options,
        IDbContextFactory<FeedContext> feedContextFactory,
        IFeedPageFetcher feedPageFetcher,
        IJsonSchemaValidator jsonSchemaValidator,
        ILoggerFactory loggerFactory)
        : base(options, feedContextFactory, feedPageFetcher, jsonSchemaValidator, loggerFactory.CreateLogger<BuildingProjector>())
    {
        Logger.LogInformation("Starting BuildingProjector");

        When(CreateEvent, async (cloudEvent, data, context, cancellationToken) =>
        {
            Logger.LogInformation("Processing create event: {EventId}", cloudEvent.Id);
            var building = new Building(
                cloudEvent.GetRequiredSubject(),
                int.Parse(data.ObjectId),
                MapStatus(data.Attributen.GetRequired(BuildingAttributes.Status).NieuweWaarde!.ToString()!),
                MapGeometryMethod(data.Attributen.GetRequired(BuildingAttributes.GeometryMethod).NieuweWaarde!.ToString()!),
                ExtractLambert2008Geometry(data.Attributen.GetRequired(BuildingAttributes.Geometry).NieuweWaarde),
                data.VersieId,
                data.VersieIdAsString);

            ProcessBuildingAttributes(data, building);

            await context.Buildings.AddAsync(building, cancellationToken);
        });

        When(UpdateEvent, async (cloudEvent, data, context, cancellationToken) =>
        {
            Logger.LogInformation("Processing update event: {EventId}", cloudEvent.Id);
            var subject = cloudEvent.GetRequiredSubject();
            var building = await context.Buildings.FindAsync([subject], cancellationToken: cancellationToken);
            if (building == null)
                throw new InvalidOperationException($"Building {subject} not found");

            ProcessBuildingAttributes(data, building);
        });

        When(DeleteEvent, async (cloudEvent, data, context, cancellationToken) =>
        {
            Logger.LogInformation("Processing delete event: {EventId}", cloudEvent.Id);
            var subject = cloudEvent.GetRequiredSubject();
            var building = await context.Buildings.FindAsync([subject], cancellationToken: cancellationToken);
            if (building == null)
                throw new InvalidOperationException($"Building {subject} not found");

            building.VersionId = data.VersieId;
            building.VersionIdAsString = data.VersieIdAsString;
            building.IsRemoved = true;
        });
    }

    private void ProcessBuildingAttributes(CloudEventData data, Building building)
    {
        building.VersionId = data.VersieId;
        building.VersionIdAsString = data.VersieIdAsString;
        foreach (var attribute in data.Attributen)
        {
            switch (attribute.Naam)
            {
                case BuildingAttributes.Status:
                    building.Status = MapStatus(attribute.NieuweWaarde!.ToString()!);
                    break;

                case BuildingAttributes.GeometryMethod:
                    building.GeometryMethod = MapGeometryMethod(attribute.NieuweWaarde!.ToString()!);
                    break;

                case BuildingAttributes.Geometry:
                    building.Geometry = ExtractLambert2008Geometry(attribute.NieuweWaarde);
                    break;

                default:
                    throw new InvalidOperationException($"Unknown building attribute '{attribute.Naam}' for building {building.PersistentLocalId} ({building.PersistentUri})");
            }
        }
    }

    private static BuildingStatus MapStatus(string status)
    {
        return status switch
        {
            StatusPuriPlanned => BuildingStatus.Planned,
            StatusPuriUnderConstruction => BuildingStatus.UnderConstruction,
            StatusPuriNotRealized => BuildingStatus.NotRealized,
            StatusPuriRealized => BuildingStatus.Realized,
            StatusPuriRetired => BuildingStatus.Retired,
            _ => throw new ArgumentException($"Unknown building status: {status}")
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

    private static BuildingGeometryMethod MapGeometryMethod(string geometryMethod)
    {
        return geometryMethod switch
        {
            GeometryMethodPuriOutlined => BuildingGeometryMethod.Outlined,
            GeometryMethodPuriMeasuredByGrb => BuildingGeometryMethod.MeasuredByGrb,
            _ => throw new ArgumentException($"Unknown building geometry method: {geometryMethod}")
        };
    }
}
