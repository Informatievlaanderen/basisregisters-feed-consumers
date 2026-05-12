namespace Basisregisters.FeedConsumers.Console.PostalInformation;

using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using Common;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Model;

public class PostalInformationProjector : FeedProjectorBase
{
    public readonly static BaseRegistriesCloudEventType CreateEvent = new BaseRegistriesCloudEventType("basisregisters.postalinformation.create.v1");
    public readonly static BaseRegistriesCloudEventType UpdateEvent = new BaseRegistriesCloudEventType("basisregisters.postalinformation.update.v1");
    public readonly static BaseRegistriesCloudEventType DeleteEvent = new BaseRegistriesCloudEventType("basisregisters.postalinformation.delete.v1");

    public PostalInformationProjector(
        FeedProjectorOptions options,
        IDbContextFactory<FeedContext> feedContextFactory,
        IFeedPageFetcher feedPageFetcher,
        IJsonSchemaValidator jsonSchemaValidator,
        ILoggerFactory loggerFactory)
        : base(options, feedContextFactory, feedPageFetcher, jsonSchemaValidator, loggerFactory.CreateLogger<PostalInformationProjector>())
    {
        Logger.LogInformation("Starting PostalInformationProjector");

        When(CreateEvent, async (cloudEvent, data, context, cancellationToken) =>
        {
            Logger.LogInformation("Processing create event: {EventId}", cloudEvent.Id);
            var statusAttribute = data.Attributen.FirstOrDefault(a => a.Naam == PostalInformationAttributes.Status);
            var status = statusAttribute is not null
                ? MapStatus(statusAttribute.NieuweWaarde!.ToString()!)
                : PostalInformationStatus.Realized;

            var postalInformation = new PostalInformation(
                data.Id.ToString(),
                data.ObjectId,
                null,
                status,
                data.VersieId,
                data.VersieIdAsString);

            await ProcessPostalInformationAttributes(data, postalInformation, context, cancellationToken);

            await context.PostalInformations.AddAsync(postalInformation, cancellationToken);
        });

        When(UpdateEvent, async (cloudEvent, data, context, cancellationToken) =>
        {
            Logger.LogInformation("Processing update event: {EventId}", cloudEvent.Id);
            var postalInformation = await context.PostalInformations
                .FindAsync([data.Id.ToString()], cancellationToken: cancellationToken);

            if (postalInformation == null)
                throw new InvalidOperationException($"PostalInformation {data.Id} not found");

            await ProcessPostalInformationAttributes(data, postalInformation, context, cancellationToken);
        });

        When(DeleteEvent, async (cloudEvent, data, context, cancellationToken) =>
        {
            Logger.LogInformation("Processing delete event: {EventId}", cloudEvent.Id);
            var postalInformation = await context.PostalInformations.FindAsync([data.Id.ToString()], cancellationToken: cancellationToken);
            if (postalInformation == null)
                throw new InvalidOperationException($"PostalInformation {data.Id} not found");

            postalInformation.VersionId = data.VersieId;
            postalInformation.VersionIdAsString = data.VersieIdAsString;
            postalInformation.IsRemoved = true;
        });
    }

    private static async Task ProcessPostalInformationAttributes(
        CloudEventData data,
        PostalInformation postalInformation,
        FeedContext context,
        CancellationToken cancellationToken)
    {
        postalInformation.VersionId = data.VersieId;
        postalInformation.VersionIdAsString = data.VersieIdAsString;
        foreach (var attribute in data.Attributen)
        {
            switch (attribute.Naam)
            {
                case PostalInformationAttributes.Status:
                    postalInformation.Status = MapStatus(attribute.NieuweWaarde!.ToString()!);
                    break;

                case PostalInformationAttributes.MunicipalityId:
                    var municipalityPuri = attribute.NieuweWaarde?.ToString();
                    postalInformation.NisCode = municipalityPuri?.ExtractPersistentLocalId();
                    break;

                case PostalInformationAttributes.Names:
                    var names = attribute.NieuweWaarde is JsonElement namesElement
                        ? namesElement.Deserialize<List<GeographicalName>>(CloudEventReader.JsonOptions)
                        : [];

                    if (names is not null)
                    {
                        await SyncPostalNamesAsync(
                            postalInformation.PostalCode,
                            names,
                            context,
                            cancellationToken);
                    }
                    break;

                default:
                    throw new InvalidOperationException($"Unknown postal information attribute: {attribute.Naam}");
            }
        }
    }

    private static Language MapLanguage(string language)
    {
        return language.ToLowerInvariant() switch
        {
            "nl" => Language.Nl,
            "fr" => Language.Fr,
            "de" => Language.De,
            "en" => Language.En,
            _ => throw new ArgumentException($"Unknown language: {language}")
        };
    }

    private static PostalInformationStatus MapStatus(string status)
    {
        return status switch
        {
            "gerealiseerd" => PostalInformationStatus.Realized,
            "gehistoreerd" => PostalInformationStatus.Retired,
            _ => throw new ArgumentException($"Unknown status: {status}")
        };
    }

    private static async Task SyncPostalNamesAsync(
        string postalCode,
        IReadOnlyCollection<GeographicalName> names,
        FeedContext context,
        CancellationToken cancellationToken)
    {
        var updatedNames = names
            .Select(name => new { Name = name.Spelling, Lang = MapLanguage(name.Taal)})
            .ToHashSet();

        var existingPostalNames = (await context.Set<PostalInformationName>()
                .Where(x => x.PostalCode == postalCode)
                .ToListAsync(cancellationToken))
            .UnionBy(
                context.Set<PostalInformationName>().Local.Where(x => x.PostalCode == postalCode),
                x => new{x.Name, Lang = x.Language})
            .ToList();

        foreach (var existingPostalName in existingPostalNames
                     .Where(x => !updatedNames.Contains(new {x.Name, Lang = x.Language})))
        {
            context.Remove(existingPostalName);
        }

        var existingNames = existingPostalNames
            .Select(x => new {x.Name, Lang = x.Language })
            .ToHashSet();

        foreach (var updatedName in updatedNames.Where(x => !existingNames.Contains(x)))
        {
            await context.AddAsync(
                new PostalInformationName(updatedName.Name, updatedName.Lang, postalCode),
                cancellationToken);
        }
    }
}
