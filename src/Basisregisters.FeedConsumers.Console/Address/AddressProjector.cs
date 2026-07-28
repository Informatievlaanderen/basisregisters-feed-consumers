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

    private const string StatusPuriProposed = "https://data.vlaanderen.be/id/concept/adresstatus/voorgesteld";
    private const string StatusPuriCurrent = "https://data.vlaanderen.be/id/concept/adresstatus/inGebruik";
    private const string StatusPuriRejected = "https://data.vlaanderen.be/id/concept/adresstatus/afgekeurd";
    private const string StatusPuriRetired = "https://data.vlaanderen.be/id/concept/adresstatus/gehistoreerd";

    private const string GeometryMethodPuriAppointedByAdministrator = "https://data.vlaanderen.be/id/concept/geometriemethode/aangeduidDoorBeheerder";
    private const string GeometryMethodPuriDerivedFromObject = "https://data.vlaanderen.be/id/concept/geometriemethode/afgeleidVanObject";
    private const string GeometryMethodPuriInterpolated = "https://data.vlaanderen.be/id/concept/geometriemethode/geinterpoleerd";

    private const string SpecificationPuriMunicipality = "https://data.vlaanderen.be/id/concept/geometriespecificatie/gemeente";
    private const string SpecificationPuriStreet = "https://data.vlaanderen.be/id/concept/geometriespecificatie/straat";
    private const string SpecificationPuriParcel = "https://data.vlaanderen.be/id/concept/geometriespecificatie/perceel";
    private const string SpecificationPuriLot = "https://data.vlaanderen.be/id/concept/geometriespecificatie/lot";
    private const string SpecificationPuriStand = "https://data.vlaanderen.be/id/concept/geometriespecificatie/standplaats";
    private const string SpecificationPuriBerth = "https://data.vlaanderen.be/id/concept/geometriespecificatie/ligplaats";
    private const string SpecificationPuriBuilding = "https://data.vlaanderen.be/id/concept/geometriespecificatie/gebouw";
    private const string SpecificationPuriBuildingUnit = "https://data.vlaanderen.be/id/concept/geometriespecificatie/gebouweenheid";
    private const string SpecificationPuriEntry = "https://data.vlaanderen.be/id/concept/geometriespecificatie/ingang";
    private const string SpecificationPuriRoadSegment = "https://data.vlaanderen.be/id/concept/geometriespecificatie/wegsegment";

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
            var subject = cloudEvent.GetRequiredSubject();
            var address = await context.Addresses.FindAsync([subject], cancellationToken: cancellationToken);
            if (address is { IsRemoved: true })
            {
                address.IsRemoved = false;
            }
            else
            {
                address = new Address(
                    subject,
                    int.Parse(data.ObjectId),
                    data.Attributen.GetRequired(AddressAttributes.StreetNameId).NieuweWaarde!.ToString()!.ExtractPersistentLocalIdAsInt(),
                    data.Attributen.GetRequired(AddressAttributes.HouseNumber).NieuweWaarde!.ToString()!,
                    MapStatus(data.Attributen.GetRequired(AddressAttributes.Status).NieuweWaarde!.ToString()!),
                    data.VersieId,
                    data.VersieIdAsString
                );

                await context.Addresses.AddAsync(address, cancellationToken);
            }

            ProcessAddressAttributes(data, address);
        });

        When(UpdateEvent, async (cloudEvent, data, context, cancellationToken) =>
        {
            Logger.LogInformation("Processing update event: {EventId}", cloudEvent.Id);
            var subject = cloudEvent.GetRequiredSubject();
            var address = await context.Addresses.FindAsync([subject], cancellationToken: cancellationToken);
            if (address == null)
                throw new InvalidOperationException($"Address {subject} not found");

            ProcessAddressAttributes(data, address);
        });

        When(DeleteEvent, async (cloudEvent, data, context, cancellationToken) =>
        {
            Logger.LogInformation("Processing delete event: {EventId}", cloudEvent.Id);
            var subject = cloudEvent.GetRequiredSubject();
            var address = await context.Addresses.FindAsync([subject], cancellationToken: cancellationToken);
            if (address == null)
                throw new InvalidOperationException($"Address {subject} not found");

            address.VersionId = data.VersieId;
            address.VersionIdAsString = data.VersieIdAsString;
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
        address.VersionIdAsString = data.VersieIdAsString;
        foreach (var attribute in data.Attributen)
        {
            switch (attribute.Naam)
            {
                case AddressAttributes.Status:
                    address.Status = MapStatus(attribute.NieuweWaarde!.ToString()!);
                    break;

                case AddressAttributes.StreetNameId:
                    address.StreetNamePersistentLocalId = attribute.NieuweWaarde!.ToString()!.ExtractPersistentLocalIdAsInt();
                    break;

                case AddressAttributes.HouseNumber:
                    address.HouseNumber = attribute.NieuweWaarde!.ToString()!;
                    break;

                case AddressAttributes.BoxNumber:
                    address.BoxNumber = attribute.NieuweWaarde is null ? null : attribute.NieuweWaarde.ToString();
                    break;

                case AddressAttributes.PostalInformationId:
                    var postalInformationPuri = attribute.NieuweWaarde?.ToString();
                    address.PostalCode = string.IsNullOrEmpty(postalInformationPuri)
                        ? null
                        : postalInformationPuri.ExtractPersistentLocalId();
                    break;

                case AddressAttributes.OfficiallyAssigned:
                    address.OfficiallyAssigned = attribute.NieuweWaarde!.ToBoolean();
                    break;

                case AddressAttributes.Position:
                    address.Geometry = ExtractLambert2008Geometry(attribute.NieuweWaarde);
                    break;

                case AddressAttributes.PositionGeometryMethod:
                    address.PositionMethod = MapGeometryMethod(attribute.NieuweWaarde!.ToString()!);
                    break;

                case AddressAttributes.PositionSpecification:
                    address.PositionSpecification = MapPositionSpecification(attribute.NieuweWaarde!.ToString()!);
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
            StatusPuriProposed => AddressStatus.Proposed,
            StatusPuriCurrent => AddressStatus.Current,
            StatusPuriRejected => AddressStatus.Rejected,
            StatusPuriRetired => AddressStatus.Retired,
            _ => throw new ArgumentException($"Unknown address status: {status}")
        };
    }

    private Geometry ExtractLambert2008Geometry(object? geometry)
    {
        var geometries = geometry is JsonElement positionElement
            ? positionElement.Deserialize<List<GmlGeometry>>(CloudEventReader.JsonOptions)
            : [];

        var geometryData = geometries?
            .FirstOrDefault(x => x.IsLambert2008);

        if (geometryData is null)
            throw new ArgumentException("Address position must contain Lambert 2008 (EPSG:3812) geometry.");

        return MapGeometry(geometryData);
    }

    private Geometry MapGeometry(GmlGeometry geometryData)
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
            GeometryMethodPuriAppointedByAdministrator => AddressPositionGeometryMethod.AppointedByAdministrator,
            GeometryMethodPuriDerivedFromObject => AddressPositionGeometryMethod.DerivedFromObject,
            GeometryMethodPuriInterpolated => AddressPositionGeometryMethod.Interpolated,
            _ => throw new ArgumentException($"Unknown geometry method: {geometryMethod}")
        };
    }

    private static AddressPositionSpecification MapPositionSpecification(string positionSpecification)
    {
        return positionSpecification switch
        {
            SpecificationPuriMunicipality => AddressPositionSpecification.Municipality,
            SpecificationPuriStreet => AddressPositionSpecification.Street,
            SpecificationPuriParcel => AddressPositionSpecification.Parcel,
            SpecificationPuriLot => AddressPositionSpecification.Lot,
            SpecificationPuriStand => AddressPositionSpecification.Stand,
            SpecificationPuriBerth => AddressPositionSpecification.Berth,
            SpecificationPuriBuilding => AddressPositionSpecification.Building,
            SpecificationPuriBuildingUnit => AddressPositionSpecification.BuildingUnit,
            SpecificationPuriEntry => AddressPositionSpecification.Entry,
            SpecificationPuriRoadSegment => AddressPositionSpecification.RoadSegment,
            _ => throw new ArgumentException($"Unknown position specification: {positionSpecification}")
        };
    }
}
