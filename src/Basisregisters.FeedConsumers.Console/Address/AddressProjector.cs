namespace Basisregisters.FeedConsumers.Console.Address;

using System;
using Common;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Model;
using NetTopologySuite.IO.GML2;
using Geometry = NetTopologySuite.Geometries.Geometry;

public sealed class AddressProjector : FeedProjectorBase
{
    private readonly GMLReader _gmlReader = GmlReaderFactory.CreateLambert2008GmlReader();

    public AddressProjector(
        FeedProjectorOptions options,
        IDbContextFactory<FeedContext> feedContextFactory,
        IFeedPageFetcher feedPageFetcher,
        IJsonSchemaValidator jsonSchemaValidator,
        ILogger logger)
        : base(options, feedContextFactory, feedPageFetcher, jsonSchemaValidator, logger)
    { }

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
}
