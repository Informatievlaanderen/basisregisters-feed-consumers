namespace Basisregisters.FeedConsumers.Console.BuildingUnit;

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

public sealed class BuildingUnitProjector : FeedProjectorBase
{
    private readonly GMLReader _gmlReader = GmlReaderFactory.CreateLambert2008GmlReader();

    public BuildingUnitProjector(
        FeedProjectorOptions options,
        IDbContextFactory<FeedContext> feedContextFactory,
        IFeedPageFetcher feedPageFetcher,
        IJsonSchemaValidator jsonSchemaValidator,
        ILoggerFactory loggerFactory)
        : base(options, feedContextFactory, feedPageFetcher, jsonSchemaValidator, loggerFactory.CreateLogger<BuildingUnitProjector>())
    {

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
}
