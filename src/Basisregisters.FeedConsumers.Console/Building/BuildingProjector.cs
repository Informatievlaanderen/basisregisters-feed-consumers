namespace Basisregisters.FeedConsumers.Console.Building;

using System;
using Common;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Model;
using NetTopologySuite.Geometries;
using NetTopologySuite.IO.GML2;

public sealed class BuildingProjector : FeedProjectorBase
{
    private readonly GMLReader _gmlReader = GmlReaderFactory.CreateLambert2008GmlReader();

    public BuildingProjector(
        FeedProjectorOptions options,
        IDbContextFactory<FeedContext> feedContextFactory,
        IFeedPageFetcher feedPageFetcher,
        IJsonSchemaValidator jsonSchemaValidator,
        ILogger logger)
        : base(options, feedContextFactory, feedPageFetcher, jsonSchemaValidator, logger)
    { }

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
            "ingeschetst" => BuildingGeometryMethod.Outlined,
            "ingemetenGRB" => BuildingGeometryMethod.MeasuredByGrb,
            _ => throw new ArgumentException($"Unknown building geometry method: {geometryMethod}")
        };
    }
}
