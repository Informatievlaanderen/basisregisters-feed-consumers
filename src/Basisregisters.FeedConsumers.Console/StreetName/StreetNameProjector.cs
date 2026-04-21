namespace Basisregisters.FeedConsumers.Console.StreetName;

using System;
using System.Collections.Generic;
<<<<<<< HEAD
=======
using System.Linq;
>>>>>>> 0494f61 (feat: complete StreetNameProjector with event handlers and tests)
using System.Text.Json;
using System.Threading.Tasks;
using Common;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Model;

public sealed class StreetNameProjector : FeedProjectorBase
{
    public static readonly BaseRegistriesCloudEventType CreateEvent = new("basisregisters.streetname.create.v1");
    public static readonly BaseRegistriesCloudEventType UpdateEvent = new("basisregisters.streetname.update.v1");
    public static readonly BaseRegistriesCloudEventType DeleteEvent = new("basisregisters.streetname.delete.v1");
    public static readonly BaseRegistriesCloudEventType TransformEvent = new("basisregisters.streetname.transform.v1");

    public StreetNameProjector(
        FeedProjectorOptions options,
        IDbContextFactory<FeedContext> feedContextFactory,
        IFeedPageFetcher feedPageFetcher,
        IJsonSchemaValidator jsonSchemaValidator,
        ILoggerFactory loggerFactory)
        : base(options, feedContextFactory, feedPageFetcher, jsonSchemaValidator, loggerFactory.CreateLogger<StreetNameProjector>())
    {
        Logger.LogInformation("Starting StreetNameProjector");

        When(CreateEvent, async (cloudEvent, data, context, cancellationToken) =>
        {
            Logger.LogInformation("Processing create event: {EventId}", cloudEvent.Id);
            var status = MapStatus(data.Attributen.GetRequired(StreetNameAttributes.Status).NieuweWaarde.ToString()!);
            var nisCode = ExtractNisCode(data.Attributen.GetRequired(StreetNameAttributes.MunicipalityId).NieuweWaarde.ToString()!);
            var persistentLocalId = int.Parse(data.ObjectId);

            var streetName = new StreetName(
                data.Id.ToString(),
                persistentLocalId,
                nisCode,
                status,
                data.VersieId);

            ProcessStreetNameAttributes(data, streetName);

            await context.StreetNames.AddAsync(streetName, cancellationToken);
        });

        When(UpdateEvent, async (cloudEvent, data, context, cancellationToken) =>
        {
            Logger.LogInformation("Processing update event: {EventId}", cloudEvent.Id);
            var streetName = await context.StreetNames.FindAsync([data.Id.ToString()], cancellationToken: cancellationToken);
            if (streetName == null)
                throw new InvalidOperationException($"StreetName {data.Id} not found");

            ProcessStreetNameAttributes(data, streetName);
        });

        When(DeleteEvent, async (cloudEvent, data, context, cancellationToken) =>
        {
            Logger.LogInformation("Processing delete event: {EventId}", cloudEvent.Id);
            var streetName = await context.StreetNames.FindAsync([data.Id.ToString()], cancellationToken: cancellationToken);
            if (streetName == null)
                throw new InvalidOperationException($"StreetName {data.Id} not found");

            streetName.IsRemoved = true;
        });

        When(TransformEvent, (_, _, _, _) =>
        {
            Logger.LogInformation("Ignoring transform event");
            return Task.CompletedTask;
        });
    }

    private static void ProcessStreetNameAttributes(CloudEventData data, StreetName streetName)
    {
        streetName.VersionId = data.VersieId;
        foreach (var attribute in data.Attributen)
        {
            switch (attribute.Naam)
            {
                case StreetNameAttributes.Status:
                    streetName.Status = MapStatus(attribute.NieuweWaarde.ToString()!);
                    break;

                case StreetNameAttributes.MunicipalityId:
                    streetName.NisCode = ExtractNisCode(attribute.NieuweWaarde.ToString()!);
                    break;

                case StreetNameAttributes.Names:
                    var names = attribute.NieuweWaarde is JsonElement namesElement
                        ? namesElement.Deserialize<List<GeographicalName>>(CloudEventReader.JsonOptions)
                        : [];

                    if (names is not null)
                    {
                        streetName.NameDutch = null;
                        streetName.NameFrench = null;
                        streetName.NameGerman = null;
                        streetName.NameEnglish = null;
                        foreach (var name in names)
                        {
                            switch (name.Taal)
                            {
                                case "nl":
                                    streetName.NameDutch = name.Spelling;
                                    break;
                                case "fr":
                                    streetName.NameFrench = name.Spelling;
                                    break;
                                case "de":
                                    streetName.NameGerman = name.Spelling;
                                    break;
                                case "en":
                                    streetName.NameEnglish = name.Spelling;
                                    break;
                                default:
                                    throw new InvalidOperationException($"Unknown streetname name language: {name.Taal}");
                            }
                        }
                    }
                    break;

                case StreetNameAttributes.HomonymAdditions:
                    var homonyms = attribute.NieuweWaarde is JsonElement homonymsElement
                        ? homonymsElement.Deserialize<List<GeographicalName>>(CloudEventReader.JsonOptions)
                        : [];

                    if (homonyms is not null)
                    {
                        streetName.HomonymAdditionDutch = null;
                        streetName.HomonymAdditionFrench = null;
                        streetName.HomonymAdditionGerman = null;
                        streetName.HomonymAdditionEnglish = null;
                        foreach (var homonym in homonyms)
                        {
                            switch (homonym.Taal)
                            {
                                case "nl":
                                    streetName.HomonymAdditionDutch = homonym.Spelling;
                                    break;
                                case "fr":
                                    streetName.HomonymAdditionFrench = homonym.Spelling;
                                    break;
                                case "de":
                                    streetName.HomonymAdditionGerman = homonym.Spelling;
                                    break;
                                case "en":
                                    streetName.HomonymAdditionEnglish = homonym.Spelling;
                                    break;
                                default:
                                    throw new InvalidOperationException($"Unknown streetname homonym addition language: {homonym.Taal}");
                            }
                        }
                    }
                    break;

                default:
                    throw new InvalidOperationException($"Unknown streetname attribute: {attribute.Naam}");
            }
        }
    }

    private static string ExtractNisCode(string municipalityPuri)
    {
        var lastSlashIndex = municipalityPuri.LastIndexOf('/');
        return lastSlashIndex >= 0
            ? municipalityPuri[(lastSlashIndex + 1)..]
            : municipalityPuri;
    }

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
