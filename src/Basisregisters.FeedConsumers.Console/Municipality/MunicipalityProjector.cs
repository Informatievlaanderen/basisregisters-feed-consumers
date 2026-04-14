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

    public MunicipalityProjector(
        FeedProjectorOptions options,
        IDbContextFactory<FeedContext> feedContextFactory,
        ILoggerFactory loggerFactory)
        : base(options, feedContextFactory, loggerFactory.CreateLogger<MunicipalityProjector>())
    {
        Logger.LogInformation("Starting MunicipalityProjector");

        When(CreateEvent, async (cloudEvent, data, context, cancellationToken) =>
        {
            Logger.LogInformation($"Processing create event: {cloudEvent.Id}");
            var municipality = new Municipality(
                data.Id.ToString(),
                data.ObjectId,
                data.VersieId,
                MapStatus(data.Attributen.GetRequired(MunicipalityAttributes.Status).NieuweWaarde.ToString()!),
                false);

            ProcessMunicipalityAttributes(data, municipality);

            await context.Municipalities.AddAsync(municipality, cancellationToken);
        });

        When(UpdateEvent, async (cloudEvent, data, context, cancellationToken) =>
        {
            Logger.LogInformation($"Processing update event: {cloudEvent.Id}");
            var municipality = await context.Municipalities.FindAsync([data.Id.ToString()], cancellationToken: cancellationToken);
            if (municipality == null)
                throw new InvalidOperationException($"Municipality {data.Id} not found");

            ProcessMunicipalityAttributes(data, municipality);
        });

        When(DeleteEvent, async (cloudEvent, data, context, cancellationToken) =>
        {
            Logger.LogInformation($"Processing delete event: {cloudEvent.Id}");
            var municipality = await context.Municipalities.FindAsync([data.Id.ToString()], cancellationToken: cancellationToken);
            if (municipality == null)
                throw new InvalidOperationException($"Municipality {data.Id} not found");

            municipality.IsRemoved = true;
        });

        When(TransformEvent, (_, _, _, _) =>
        {
            try
            {
                Logger.LogInformation($"Ignoring transform event");
                return Task.CompletedTask;
            }
            catch (Exception exception)
            {
                return Task.FromException(exception);
            }
        });
    }

    private static void ProcessMunicipalityAttributes(CloudEventData data, Municipality municipality)
    {
        foreach (var attribute in data.Attributen)
        {
            switch (attribute.Naam)
            {
                case MunicipalityAttributes.Status:
                    municipality.Status = MapStatus(attribute.NieuweWaarde.ToString()!);
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
                        ? namesElement.Deserialize<List<GeographicalName>>(CloudEventReader.JsonOptions)
                        : [];

                    if (names is not null)
                    {
                        foreach (var geographicalName in names)
                        {
                            switch (geographicalName.Taal)
                            {
                                case "nl":
                                    municipality.NameDutch = geographicalName.Spelling;
                                    break;
                                case "fr":
                                    municipality.NameFrench = geographicalName.Spelling;
                                    break;
                                case "de":
                                    municipality.NameGerman = geographicalName.Spelling;
                                    break;
                                case "en":
                                    municipality.NameEnglish = geographicalName.Spelling;
                                    break;
                                default:
                                    throw new InvalidOperationException($"Unknown municipality name language: {geographicalName.Taal}");
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
            "voorgesteld" => MunicipalityStatus.Proposed,
            "inGebruik" => MunicipalityStatus.Current,
            "gehistoreerd" => MunicipalityStatus.Retired,
            _ => throw new ArgumentException($"Unknown status: {status}")
        };
    }
}
