namespace Basisregisters.FeedConsumers.Console.StreetName;

using System;
using System.Collections.Generic;
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

    private const string StatusPuriProposed = "https://data.vlaanderen.be/id/concept/straatnaamstatus/voorgesteld";
    private const string StatusPuriCurrent = "https://data.vlaanderen.be/id/concept/straatnaamstatus/inGebruik";
    private const string StatusPuriRejected = "https://data.vlaanderen.be/id/concept/straatnaamstatus/afgekeurd";
    private const string StatusPuriRetired = "https://data.vlaanderen.be/id/concept/straatnaamstatus/gehistoreerd";

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
            var status = MapStatus(data.Attributen.GetRequired(StreetNameAttributes.Status).NieuweWaarde!.ToString()!);
            var nisCode = data.Attributen.GetRequired(StreetNameAttributes.AssignedBy).NieuweWaarde!.ToString()!.ExtractPersistentLocalId();
            var persistentLocalId = int.Parse(data.ObjectId);

            var streetName = new StreetName(
                cloudEvent.GetRequiredSubject(),
                persistentLocalId,
                nisCode,
                status,
                data.VersieId,
                data.VersieIdAsString);

            ProcessStreetNameAttributes(data, streetName);

            await context.StreetNames.AddAsync(streetName, cancellationToken);
        });

        When(UpdateEvent, async (cloudEvent, data, context, cancellationToken) =>
        {
            Logger.LogInformation("Processing update event: {EventId}", cloudEvent.Id);
            var subject = cloudEvent.GetRequiredSubject();
            var streetName = await context.StreetNames.FindAsync([subject], cancellationToken: cancellationToken);
            if (streetName == null)
                throw new InvalidOperationException($"StreetName {subject} not found");

            ProcessStreetNameAttributes(data, streetName);
        });

        When(DeleteEvent, async (cloudEvent, data, context, cancellationToken) =>
        {
            Logger.LogInformation("Processing delete event: {EventId}", cloudEvent.Id);
            var subject = cloudEvent.GetRequiredSubject();
            var streetName = await context.StreetNames.FindAsync([subject], cancellationToken: cancellationToken);
            if (streetName == null)
                throw new InvalidOperationException($"StreetName {subject} not found");

            streetName.VersionId = data.VersieId;
            streetName.VersionIdAsString = data.VersieIdAsString;
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
        streetName.VersionIdAsString = data.VersieIdAsString;
        foreach (var attribute in data.Attributen)
        {
            switch (attribute.Naam)
            {
                case StreetNameAttributes.Status:
                    streetName.Status = MapStatus(attribute.NieuweWaarde!.ToString()!);
                    break;

                case StreetNameAttributes.AssignedBy:
                    streetName.NisCode = attribute.NieuweWaarde!.ToString()!.ExtractPersistentLocalId();
                    break;

                case StreetNameAttributes.Names:
                    var names = attribute.NieuweWaarde is JsonElement namesElement
                        ? namesElement.Deserialize<List<LanguageTaggedValue>>(CloudEventReader.JsonOptions)
                        : [];

                    if (names is not null)
                    {
                        streetName.NameDutch = null;
                        streetName.NameFrench = null;
                        streetName.NameGerman = null;
                        streetName.NameEnglish = null;
                        foreach (var name in names)
                        {
                            switch (name.Language)
                            {
                                case "nl":
                                    streetName.NameDutch = name.Value;
                                    break;
                                case "fr":
                                    streetName.NameFrench = name.Value;
                                    break;
                                case "de":
                                    streetName.NameGerman = name.Value;
                                    break;
                                case "en":
                                    streetName.NameEnglish = name.Value;
                                    break;
                                default:
                                    throw new InvalidOperationException($"Unknown streetname name language: {name.Language}");
                            }
                        }
                    }
                    break;

                case StreetNameAttributes.HomonymAdditions:
                    var homonyms = attribute.NieuweWaarde is JsonElement homonymsElement
                        ? homonymsElement.Deserialize<List<LanguageTaggedValue>>(CloudEventReader.JsonOptions)
                        : [];

                    if (homonyms is not null)
                    {
                        streetName.HomonymAdditionDutch = null;
                        streetName.HomonymAdditionFrench = null;
                        streetName.HomonymAdditionGerman = null;
                        streetName.HomonymAdditionEnglish = null;
                        foreach (var homonym in homonyms)
                        {
                            switch (homonym.Language)
                            {
                                case "nl":
                                    streetName.HomonymAdditionDutch = homonym.Value;
                                    break;
                                case "fr":
                                    streetName.HomonymAdditionFrench = homonym.Value;
                                    break;
                                case "de":
                                    streetName.HomonymAdditionGerman = homonym.Value;
                                    break;
                                case "en":
                                    streetName.HomonymAdditionEnglish = homonym.Value;
                                    break;
                                default:
                                    throw new InvalidOperationException($"Unknown streetname homonym addition language: {homonym.Language}");
                            }
                        }
                    }
                    break;

                default:
                    throw new InvalidOperationException($"Unknown streetname attribute: {attribute.Naam}");
            }
        }
    }

    private static StreetNameStatus MapStatus(string status)
    {
        return status switch
        {
            StatusPuriProposed => StreetNameStatus.Proposed,
            StatusPuriCurrent => StreetNameStatus.Current,
            StatusPuriRejected => StreetNameStatus.Rejected,
            StatusPuriRetired => StreetNameStatus.Retired,
            _ => throw new ArgumentException($"Unknown streetname status: {status}")
        };
    }
}
