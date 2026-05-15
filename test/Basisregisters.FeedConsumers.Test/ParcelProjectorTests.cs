namespace Basisregisters.FeedConsumers.Test;

using System;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Console.Common;
using Console.Parcel;
using FluentAssertions;
using Infrastructure;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;
using Model;
using Xunit;

public class ParcelProjectorTests
{
    private readonly InMemoryFeedContextFactory _contextFactory;
    private readonly FakeFeedPageFetcher _feedPageFetcher;
    private readonly ParcelProjector _projector;

    private const string PuriParcel72015B051700B002 = "https://data.vlaanderen.be/id/perceel/72015B0517-00B002";
    private const string PuriParcel11001B002600A004 = "https://data.vlaanderen.be/id/perceel/11001B0026-00A004";
    private const string ParcelFeedName = "ParcelFeed";

    public ParcelProjectorTests()
    {
        _contextFactory = new InMemoryFeedContextFactory();
        _feedPageFetcher = new FakeFeedPageFetcher();

        var options = new FeedProjectorOptions
        {
            Name = ParcelFeedName,
            FeedUrl = "https://test/feed",
            PollingIntervalInMinutes = 1,
            IgnoreNoEventHandlers = false
        };

        _projector = new ParcelProjector(
            options,
            _contextFactory,
            _feedPageFetcher,
            new NoOpJsonSchemaValidator(),
            new NullLoggerFactory());
    }

    [Fact]
    public async Task CreateEvent_ShouldAddParcelWithCorrectDataAndAddresses()
    {
        var events = await CloudEventTestHelper.ReadEventsFromFileAsync(
            Path.Combine("TestData", "parcel-create-multiple-addresses-only-versionchanges.json"));

        var createEvents = events.Where(e => e.Id == "1093704").ToList();
        _feedPageFetcher.SetupPage(1, createEvents.ToFeedPage(isPageComplete: false));

        using var cts = new CancellationTokenSource();
        cts.CancelAfter(5000);

        await RunOneCycleAsync(cts.Token);

        await using var context = _contextFactory.CreateDbContext();
        var parcel = await context.Parcels.FindAsync([PuriParcel72015B051700B002], TestContext.Current.CancellationToken);
        var addressLinks = await context.ParcelAddresses
            .Where(x => x.VbrCaPaKey == "72015B0517-00B002")
            .OrderBy(x => x.AddressPersistentLocalId)
            .ToListAsync(TestContext.Current.CancellationToken);

        parcel.Should().NotBeNull();
        parcel!.VbrCaPaKey.Should().Be("72015B0517-00B002");
        parcel.CaPaKey.Should().Be("72015B0517/00B002");
        parcel.Status.Should().Be(ParcelStatus.Realized);
        parcel.VersionId.Should().BeCloseTo(new DateTimeOffset(2023, 11, 2, 11, 2, 22, TimeSpan.FromHours(1)), TimeSpan.FromSeconds(1));
        parcel.VersionIdAsString.Should().Be(createEvents[^1].GetVersionIdAsString());

        addressLinks.Select(x => x.AddressPersistentLocalId).Should().Equal(1097598, 4354739, 5266486);
    }

    [Fact]
    public async Task UpdateEvents_WithOnlyVersionChanges_ShouldUpdateVersionAndKeepAddresses()
    {
        var events = await CloudEventTestHelper.ReadEventsFromFileAsync(
            Path.Combine("TestData", "parcel-create-multiple-addresses-only-versionchanges.json"));

        _feedPageFetcher.SetupPage(1, events.ToFeedPage(isPageComplete: false));

        using var cts = new CancellationTokenSource();
        cts.CancelAfter(5000);

        await RunOneCycleAsync(cts.Token);

        await using var context = _contextFactory.CreateDbContext();
        var parcel = await context.Parcels.FindAsync([PuriParcel72015B051700B002], TestContext.Current.CancellationToken);
        var addressLinks = await context.ParcelAddresses
            .Where(x => x.VbrCaPaKey == "72015B0517-00B002")
            .OrderBy(x => x.AddressPersistentLocalId)
            .ToListAsync(TestContext.Current.CancellationToken);

        parcel.Should().NotBeNull();
        parcel!.Status.Should().Be(ParcelStatus.Realized);
        parcel.VersionId.Should().BeCloseTo(new DateTimeOffset(2025, 9, 3, 8, 3, 25, TimeSpan.FromHours(2)), TimeSpan.FromSeconds(1));
        parcel.VersionIdAsString.Should().Be(events[^1].GetVersionIdAsString());
        addressLinks.Select(x => x.AddressPersistentLocalId).Should().Equal(1097598, 4354739, 5266486);
    }

    [Fact]
    public async Task FullCreateUpdateRetireSequence_ShouldRetireParcelAndRemoveAddresses()
    {
        var events = await CloudEventTestHelper.ReadEventsFromFileAsync(
            Path.Combine("TestData", "parcel-create-retire.json"));

        _feedPageFetcher.SetupPage(1, events.ToFeedPage(isPageComplete: false));

        using var cts = new CancellationTokenSource();
        cts.CancelAfter(5000);

        await RunOneCycleAsync(cts.Token);

        await using var context = _contextFactory.CreateDbContext();
        var parcel = await context.Parcels.FindAsync([PuriParcel11001B002600A004], TestContext.Current.CancellationToken);
        var addressLinks = await context.ParcelAddresses
            .Where(x => x.VbrCaPaKey == "11001B0026-00A004")
            .ToListAsync(TestContext.Current.CancellationToken);

        parcel.Should().NotBeNull();
        parcel!.Status.Should().Be(ParcelStatus.Retired);
        parcel.VersionId.Should().BeCloseTo(new DateTimeOffset(2025, 9, 18, 8, 3, 17, TimeSpan.FromHours(2)), TimeSpan.FromSeconds(1));
        parcel.VersionIdAsString.Should().Be(events[^1].GetVersionIdAsString());
        addressLinks.Should().BeEmpty();
    }

    [Fact]
    public async Task UpdateEvents_ShouldRestorePersistedAddressesAfterDetachAndReattachInSameBatch()
    {
        var initialEvents = await CloudEventTestHelper.ReadEventsFromJsonAsync(
            """
            [
              {
                "specversion": "1.0",
                "id": "9100001",
                "time": "2025-09-18T08:03:00+02:00",
                "type": "basisregisters.parcel.create.v1",
                "source": "https://api.basisregisters.staging-vlaanderen.be/v2/feeds/wijzigingen/percelen",
                "datacontenttype": "application/json",
                "dataschema": "https://docs.basisregisters.staging-vlaanderen.be/schemas/feeds/wijzigingen/perceel/2026-01-21/perceel.json",
                "basisregisterseventtype": "ParcelWasMigrated",
                "basisregisterscausationid": "55555555-5555-5555-5555-555555555555",
                "data": {
                  "@id": "https://data.vlaanderen.be/id/perceel/72015B0517-00B002",
                  "objectId": "72015B0517-00B002",
                  "naamruimte": "https://data.vlaanderen.be/id/perceel",
                  "versieId": "2025-09-18T08:03:00+02:00",
                  "nisCodes": [ "72015" ],
                  "attributen": [
                    { "naam": "perceelStatus", "oudeWaarde": null, "nieuweWaarde": "gerealiseerd" },
                    {
                      "naam": "adresIds",
                      "oudeWaarde": null,
                      "nieuweWaarde": [
                        "https://data.vlaanderen.be/id/adres/1097598",
                        "https://data.vlaanderen.be/id/adres/4354739"
                      ]
                    }
                  ]
                }
              }
            ]
            """);

        _feedPageFetcher.SetupPage(1, initialEvents.ToFeedPage(isPageComplete: true));

        using (var initialCts = new CancellationTokenSource())
        {
            initialCts.CancelAfter(5000);
            await RunOneCycleAsync(initialCts.Token);
        }

        var events = await CloudEventTestHelper.ReadEventsFromJsonAsync(
            """
            [
              {
                "specversion": "1.0",
                "id": "9100002",
                "time": "2025-09-18T08:03:01+02:00",
                "type": "basisregisters.parcel.update.v1",
                "source": "https://api.basisregisters.staging-vlaanderen.be/v2/feeds/wijzigingen/percelen",
                "datacontenttype": "application/json",
                "dataschema": "https://docs.basisregisters.staging-vlaanderen.be/schemas/feeds/wijzigingen/perceel/2026-01-21/perceel.json",
                "basisregisterseventtype": "ParcelAddressWasDetachedV2",
                "basisregisterscausationid": "66666666-6666-6666-6666-666666666666",
                "data": {
                  "@id": "https://data.vlaanderen.be/id/perceel/72015B0517-00B002",
                  "objectId": "72015B0517-00B002",
                  "naamruimte": "https://data.vlaanderen.be/id/perceel",
                  "versieId": "2025-09-18T08:03:01+02:00",
                  "nisCodes": [ "72015" ],
                  "attributen": [
                    {
                      "naam": "adresIds",
                      "oudeWaarde": [
                        "https://data.vlaanderen.be/id/adres/1097598",
                        "https://data.vlaanderen.be/id/adres/4354739"
                      ],
                      "nieuweWaarde": [ "https://data.vlaanderen.be/id/adres/4354739" ]
                    }
                  ]
                }
              },
              {
                "specversion": "1.0",
                "id": "9100003",
                "time": "2025-09-18T08:03:02+02:00",
                "type": "basisregisters.parcel.update.v1",
                "source": "https://api.basisregisters.staging-vlaanderen.be/v2/feeds/wijzigingen/percelen",
                "datacontenttype": "application/json",
                "dataschema": "https://docs.basisregisters.staging-vlaanderen.be/schemas/feeds/wijzigingen/perceel/2026-01-21/perceel.json",
                "basisregisterseventtype": "ParcelAddressWasAttachedV2",
                "basisregisterscausationid": "77777777-7777-7777-7777-777777777777",
                "data": {
                  "@id": "https://data.vlaanderen.be/id/perceel/72015B0517-00B002",
                  "objectId": "72015B0517-00B002",
                  "naamruimte": "https://data.vlaanderen.be/id/perceel",
                  "versieId": "2025-09-18T08:03:02+02:00",
                  "nisCodes": [ "72015" ],
                  "attributen": [
                    {
                      "naam": "adresIds",
                      "oudeWaarde": [ "https://data.vlaanderen.be/id/adres/4354739" ],
                      "nieuweWaarde": [
                        "https://data.vlaanderen.be/id/adres/1097598",
                        "https://data.vlaanderen.be/id/adres/4354739"
                      ]
                    }
                  ]
                }
              }
            ]
            """);

        _feedPageFetcher.SetupPage(2, events.ToFeedPage(isPageComplete: false));

        using var cts = new CancellationTokenSource();
        cts.CancelAfter(5000);

        await RunOneCycleAsync(cts.Token);

        await using var context = _contextFactory.CreateDbContext();
        var parcel = await context.Parcels.FindAsync([PuriParcel72015B051700B002], TestContext.Current.CancellationToken);
        var addressLinks = await context.ParcelAddresses
            .Where(x => x.VbrCaPaKey == "72015B0517-00B002")
            .OrderBy(x => x.AddressPersistentLocalId)
            .ToListAsync(TestContext.Current.CancellationToken);

        parcel.Should().NotBeNull();
        parcel!.VersionIdAsString.Should().Be(events[^1].GetVersionIdAsString());
        addressLinks.Should().HaveCount(2);
        addressLinks.Select(x => x.AddressPersistentLocalId).Should().Equal(1097598, 4354739);
    }

    [Fact]
    public async Task FeedState_ShouldTrackPositionAfterProcessingEvents()
    {
        var events = await CloudEventTestHelper.ReadEventsFromFileAsync(
            Path.Combine("TestData", "parcel-create-retire.json"));

        _feedPageFetcher.SetupPage(1, events.ToFeedPage(isPageComplete: false));

        using var cts = new CancellationTokenSource();
        cts.CancelAfter(5000);

        await RunOneCycleAsync(cts.Token);

        await using var context = _contextFactory.CreateDbContext();
        var feedState = await context.FeedStates.FindAsync([ParcelFeedName], TestContext.Current.CancellationToken);

        feedState.Should().NotBeNull();
        feedState!.EventPosition.Should().Be(7893337);
        feedState.Page.Should().Be(1);
    }

    [Fact]
    public async Task FeedState_ShouldIncrementPageWhenComplete()
    {
        var events = await CloudEventTestHelper.ReadEventsFromFileAsync(
            Path.Combine("TestData", "parcel-create-retire.json"));

        _feedPageFetcher.SetupPage(1, events.ToFeedPage(isPageComplete: true));

        using var cts = new CancellationTokenSource();
        cts.CancelAfter(5000);

        await RunOneCycleAsync(cts.Token);

        await using var context = _contextFactory.CreateDbContext();
        var feedState = await context.FeedStates.FindAsync([ParcelFeedName], TestContext.Current.CancellationToken);

        feedState.Should().NotBeNull();
        feedState!.EventPosition.Should().Be(7893337);
        feedState.Page.Should().Be(2);
    }

    private async Task RunOneCycleAsync(CancellationToken cancellationToken)
    {
        await _projector.StartAsync(cancellationToken);
        await Task.Delay(500, CancellationToken.None);
        await _projector.StopAsync(CancellationToken.None);
    }
}
