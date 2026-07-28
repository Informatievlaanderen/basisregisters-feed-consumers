namespace Basisregisters.FeedConsumers.Console.Municipality;

using System;
using System.Collections.Generic;
using System.Text.Json;
using System.Threading.Tasks;
using Common;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Model;

public class MunicipalityProjector : FeedProjectorBase
{
    public readonly static BaseRegistriesCloudEventType CreateEvent = new BaseRegistriesCloudEventType("basisregisters.municipality.create.v1");
    public readonly static BaseRegistriesCloudEventType UpdateEvent = new BaseRegistriesCloudEventType("basisregisters.municipality.update.v1");
    public readonly static BaseRegistriesCloudEventType DeleteEvent = new BaseRegistriesCloudEventType("basisregisters.municipality.delete.v1");
    public readonly static BaseRegistriesCloudEventType TransformEvent = new BaseRegistriesCloudEventType("basisregisters.municipality.transform.v1");

    private const string StatusPuriProposed = "https://data.vlaanderen.be/id/concept/gemeentestatus/voorgesteld";
    private const string StatusPuriCurrent = "https://data.vlaanderen.be/id/concept/gemeentestatus/inGebruik";
    private const string StatusPuriRetired = "https://data.vlaanderen.be/id/concept/gemeentestatus/gehistoreerd";

    public MunicipalityProjector(
        FeedProjectorOptions options,
        IDbContextFactory<FeedContext> feedContextFactory,
        IFeedPageFetcher feedPageFetcher,
        IJsonSchemaValidator jsonSchemaValidator,
        ILoggerFactory loggerFactory)
        : base(options, feedContextFactory, feedPageFetcher, jsonSchemaValidator, loggerFactory.CreateLogger<MunicipalityProjector>())
    {
        Logger.LogInformation("Starting MunicipalityProjector");

        When(CreateEvent, async (cloudEvent, data, context, cancellationToken) =>
        {
            Logger.LogInformation("Processing create event: {EventId}", cloudEvent.Id);
            var municipality = new Municipality(
                cloudEvent.GetRequiredSubject(),
                data.ObjectId,
                data.VersieId,
                data.VersieIdAsString,
                MapStatus(data.Attributen.GetRequired(MunicipalityAttributes.Status).NieuweWaarde!.ToString()!),
                false);

            ProcessMunicipalityAttributes(data, municipality);

            await context.Municipalities.AddAsync(municipality, cancellationToken);
        });

        When(UpdateEvent, async (cloudEvent, data, context, cancellationToken) =>
        {
            Logger.LogInformation("Processing update event: {EventId}", cloudEvent.Id);
            var subject = cloudEvent.GetRequiredSubject();
            var municipality = await context.Municipalities.FindAsync([subject], cancellationToken: cancellationToken);
            if (municipality == null)
                throw new InvalidOperationException($"Municipality {subject} not found");

            ProcessMunicipalityAttributes(data, municipality);
        });

        When(DeleteEvent, async (cloudEvent, data, context, cancellationToken) =>
        {
            Logger.LogInformation("Processing delete event: {EventId}", cloudEvent.Id);
            var subject = cloudEvent.GetRequiredSubject();
            var municipality = await context.Municipalities.FindAsync([subject], cancellationToken: cancellationToken);
            if (municipality == null)
                throw new InvalidOperationException($"Municipality {subject} not found");

            municipality.VersionId = data.VersieId;
            municipality.VersionIdAsString = data.VersieIdAsString;
            municipality.IsRemoved = true;
        });

        When(TransformEvent, (_, _, _, _) =>
        {
            Logger.LogInformation("Ignoring transform event");
            return Task.CompletedTask;
        });
    }

    private static void ProcessMunicipalityAttributes(CloudEventData data, Municipality municipality)
    {
        municipality.VersionId = data.VersieId;
        municipality.VersionIdAsString = data.VersieIdAsString;
        foreach (var attribute in data.Attributen)
        {
            switch (attribute.Naam)
            {
                case MunicipalityAttributes.Status:
                    municipality.Status = MapStatus(attribute.NieuweWaarde!.ToString()!);
                    break;

                case MunicipalityAttributes.OfficialLanguages:
                    var languages = attribute.NieuweWaarde is JsonElement officialElement
                        ? officialElement.Deserialize<List<string>>(CloudEventReader.JsonOptions)
                        : [];

                    if (languages is not null)
                    {
                        municipality.OfficialLanguageDutch = languages.Contains("nl");
                        municipality.OfficialLanguageFrench = languages.Contains("fr");
                        municipality.OfficialLanguageGerman = languages.Contains("de");
                        municipality.OfficialLanguageEnglish = languages.Contains("en");
                    }

                    break;

                case MunicipalityAttributes.FacilityLanguages:
                    var facilityLanguages = attribute.NieuweWaarde is JsonElement facilitiesElement
                        ? facilitiesElement.Deserialize<List<string>>(CloudEventReader.JsonOptions)
                        : [];

                    if (facilityLanguages is not null)
                    {
                        municipality.FacilityLanguageDutch = facilityLanguages.Contains("nl");
                        municipality.FacilityLanguageFrench = facilityLanguages.Contains("fr");
                        municipality.FacilityLanguageGerman = facilityLanguages.Contains("de");
                        municipality.FacilityLanguageEnglish = facilityLanguages.Contains("en");
                    }

                    break;

                case MunicipalityAttributes.Names:
                    var names = attribute.NieuweWaarde is JsonElement namesElement
                        ? namesElement.Deserialize<List<LanguageTaggedValue>>(CloudEventReader.JsonOptions)
                        : [];

                    if (names is not null)
                    {
                        foreach (var name in names)
                        {
                            switch (name.Language)
                            {
                                case "nl":
                                    municipality.NameDutch = name.Value;
                                    break;
                                case "fr":
                                    municipality.NameFrench = name.Value;
                                    break;
                                case "de":
                                    municipality.NameGerman = name.Value;
                                    break;
                                case "en":
                                    municipality.NameEnglish = name.Value;
                                    break;
                                default:
                                    throw new InvalidOperationException($"Unknown municipality name language: {name.Language}");
                            }
                        }
                    }
                    break;

                default:
                    throw new InvalidOperationException($"Unknown attribute: {attribute.Naam}");
            }
        }
    }

    private static MunicipalityStatus MapStatus(string status)
    {
        return status switch
        {
            StatusPuriProposed => MunicipalityStatus.Proposed,
            StatusPuriCurrent => MunicipalityStatus.Current,
            StatusPuriRetired => MunicipalityStatus.Retired,
            _ => throw new ArgumentException($"Unknown status: {status}")
        };
    }
}
