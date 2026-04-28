namespace Basisregisters.FeedConsumers.Test;

using System;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Console.Address;
using Console.Common;
using FluentAssertions;
using Infrastructure;
using Microsoft.Extensions.Logging.Abstractions;
using Model;
using NetTopologySuite.Geometries;
using Xunit;

public class AddressProjectorTests
{
    private readonly InMemoryFeedContextFactory _contextFactory;
    private readonly FakeFeedPageFetcher _feedPageFetcher;
    private readonly AddressProjector _projector;

    private const string PuriAddress30411696 = "https://data.vlaanderen.be/id/adres/30411696";
    private const string PuriAddress31319625 = "https://data.vlaanderen.be/id/adres/31319625";
    private const string AddressFeedName = "AddressFeed";

    public AddressProjectorTests()
    {
        _contextFactory = new InMemoryFeedContextFactory();
        _feedPageFetcher = new FakeFeedPageFetcher();

        var options = new FeedProjectorOptions
        {
            Name = AddressFeedName,
            FeedUrl = "https://test/feed",
            PollingIntervalInMinutes = 1,
            IgnoreNoEventHandlers = false
        };

        _projector = new AddressProjector(
            options,
            _contextFactory,
            _feedPageFetcher,
            new NoOpJsonSchemaValidator(),
            new NullLoggerFactory());
    }

    [Fact]
    public async Task CreateEvent_ShouldAddAddressWithCorrectData()
    {
        var events = await CloudEventTestHelper.ReadEventsFromFileAsync(
            Path.Combine("TestData", "address-create-update-transform.json"));

        var createEvents = events.Where(e => e.Id == "4419551").ToList();
        _feedPageFetcher.SetupPage(1, createEvents.ToFeedPage(isPageComplete: false));

        using var cts = new CancellationTokenSource();
        cts.CancelAfter(5000);

        await RunOneCycleAsync(cts.Token);

        await using var context = _contextFactory.CreateDbContext();
        var address = await context.Addresses.FindAsync([PuriAddress30411696], TestContext.Current.CancellationToken);

        address.Should().NotBeNull();
        address!.PersistentLocalId.Should().Be(30411696);
        address.StreetNamePersistentLocalId.Should().Be(76256);
        address.Status.Should().Be(AddressStatus.Proposed);
        address.HouseNumber.Should().Be("30");
        address.BoxNumber.Should().BeNull();
        address.PostalCode.Should().Be("9120");
        address.OfficiallyAssigned.Should().BeTrue();
        address.PositionMethod.Should().Be(AddressPositionGeometryMethod.AppointedByAdministrator);
        address.PositionSpecification.Should().Be(AddressPositionSpecification.BuildingUnit);
        address.IsRemoved.Should().BeFalse();
        address.VersionId.Should().BeCloseTo(new DateTimeOffset(2024, 11, 28, 10, 17, 18, TimeSpan.FromHours(1)), TimeSpan.FromSeconds(1));
        address.VersionIdAsString.Should().Be(createEvents[^1].GetVersionIdAsString());

        address.Geometry.Should().BeOfType<Point>();
        var point = (Point)address.Geometry!;
        point.SRID.Should().Be(3812);
        point.X.Should().BeApproximately(643786.19, 0.01);
        point.Y.Should().BeApproximately(711105.37, 0.01);
    }

    [Fact]
    public async Task CreateAndUpdateEvents_ShouldApplyStatusChangesInSequence()
    {
        var events = await CloudEventTestHelper.ReadEventsFromFileAsync(
            Path.Combine("TestData", "address-create-update-transform.json"));

        var relevantEvents = events
            .Where(e => long.Parse(e.Id!) <= 4419556)
            .ToList();
        _feedPageFetcher.SetupPage(1, relevantEvents.ToFeedPage(isPageComplete: false));

        using var cts = new CancellationTokenSource();
        cts.CancelAfter(5000);

        await RunOneCycleAsync(cts.Token);

        await using var context = _contextFactory.CreateDbContext();
        var address = await context.Addresses.FindAsync([PuriAddress30411696], TestContext.Current.CancellationToken);

        address.Should().NotBeNull();
        address!.Status.Should().Be(AddressStatus.Current);
        address.IsRemoved.Should().BeFalse();
        address.VersionId.Should().BeCloseTo(new DateTimeOffset(2024, 11, 28, 10, 17, 18, TimeSpan.FromHours(1)), TimeSpan.FromSeconds(1));
        address.VersionIdAsString.Should().Be(relevantEvents[^1].GetVersionIdAsString());
    }

    [Fact]
    public async Task TransformEvent_ShouldBeIgnoredWithoutError()
    {
        var events = await CloudEventTestHelper.ReadEventsFromFileAsync(
            Path.Combine("TestData", "address-create-update-transform.json"));

        var relevantEvents = events
            .Where(e => long.Parse(e.Id!) <= 5086475)
            .ToList();
        _feedPageFetcher.SetupPage(1, relevantEvents.ToFeedPage(isPageComplete: false));

        using var cts = new CancellationTokenSource();
        cts.CancelAfter(5000);

        await RunOneCycleAsync(cts.Token);

        await using var context = _contextFactory.CreateDbContext();
        var address = await context.Addresses.FindAsync([PuriAddress30411696], TestContext.Current.CancellationToken);

        address.Should().NotBeNull();
        address!.Status.Should().Be(AddressStatus.Current);
    }

    [Fact]
    public async Task FullCreateUpdateTransformSequence_ShouldResultInRetiredAddress()
    {
        var events = await CloudEventTestHelper.ReadEventsFromFileAsync(
            Path.Combine("TestData", "address-create-update-transform.json"));

        _feedPageFetcher.SetupPage(1, events.ToFeedPage(isPageComplete: true));

        using var cts = new CancellationTokenSource();
        cts.CancelAfter(5000);

        await RunOneCycleAsync(cts.Token);

        await using var context = _contextFactory.CreateDbContext();
        var address = await context.Addresses.FindAsync([PuriAddress30411696], TestContext.Current.CancellationToken);

        address.Should().NotBeNull();
        address!.Status.Should().Be(AddressStatus.Retired);
        address.IsRemoved.Should().BeFalse();
        address.VersionId.Should().BeCloseTo(new DateTimeOffset(2025, 1, 1, 1, 18, 19, TimeSpan.FromHours(1)), TimeSpan.FromSeconds(1));
        address.VersionIdAsString.Should().Be(events[^1].GetVersionIdAsString());
    }

    [Fact]
    public async Task CreateEvent_WithBoxNumber_ShouldMapAllAddressFields()
    {
        var events = await CloudEventTestHelper.ReadEventsFromFileAsync(
            Path.Combine("TestData", "address-create-update-retire-delete-boxnumber.json"));

        var createEvents = events.Where(e => e.Id == "5610931").ToList();
        _feedPageFetcher.SetupPage(1, createEvents.ToFeedPage(isPageComplete: false));

        using var cts = new CancellationTokenSource();
        cts.CancelAfter(5000);

        await RunOneCycleAsync(cts.Token);

        await using var context = _contextFactory.CreateDbContext();
        var address = await context.Addresses.FindAsync([PuriAddress31319625], TestContext.Current.CancellationToken);

        address.Should().NotBeNull();
        address!.StreetNamePersistentLocalId.Should().Be(64260);
        address.Status.Should().Be(AddressStatus.Proposed);
        address.HouseNumber.Should().Be("30");
        address.BoxNumber.Should().Be("002");
        address.PostalCode.Should().Be("9620");
        address.OfficiallyAssigned.Should().BeTrue();
        address.PositionMethod.Should().Be(AddressPositionGeometryMethod.DerivedFromObject);
        address.PositionSpecification.Should().Be(AddressPositionSpecification.Parcel);

        address.Geometry.Should().BeOfType<Point>();
        var point = (Point)address.Geometry!;
        point.SRID.Should().Be(3812);
        point.X.Should().BeApproximately(609561.16, 0.01);
        point.Y.Should().BeApproximately(668977.48, 0.01);
    }

    [Fact]
    public async Task CreateEvent_WithoutLambert2008Geometry_ShouldThrow()
    {
        var events = await CloudEventTestHelper.ReadEventsFromJsonAsync(
            """
            [
              {
                "specversion": "1.0",
                "id": "9990001",
                "time": "2025-04-18T11:52:12.5955391+02:00",
                "type": "basisregisters.address.create.v1",
                "source": "https://api.basisregisters.staging-vlaanderen.be/v2/feeds/wijzigingen/adressen",
                "datacontenttype": "application/json",
                "dataschema": "https://docs.basisregisters.staging-vlaanderen.be/schemas/feeds/wijzigingen/adres/2026-01-21/adres.json",
                "basisregisterseventtype": "AddressWasProposedV2",
                "basisregisterscausationid": "bd0f564c-ad0e-514f-94b5-d1c889302e48",
                "data": {
                  "@id": "https://data.vlaanderen.be/id/adres/31319625",
                  "objectId": "31319625",
                  "naamruimte": "https://data.vlaanderen.be/id/adres",
                  "versieId": "2025-04-18T11:52:12+02:00",
                  "nisCodes": [ "41081" ],
                  "attributen": [
                    {
                      "naam": "straatnaam.id",
                      "oudeWaarde": null,
                      "nieuweWaarde": "https://data.vlaanderen.be/id/straatnaam/64260"
                    },
                    {
                      "naam": "adresStatus",
                      "oudeWaarde": null,
                      "nieuweWaarde": "voorgesteld"
                    },
                    {
                      "naam": "huisnummer",
                      "oudeWaarde": null,
                      "nieuweWaarde": "30"
                    },
                    {
                      "naam": "postcode",
                      "oudeWaarde": null,
                      "nieuweWaarde": "9620"
                    },
                    {
                      "naam": "officieelToegekend",
                      "oudeWaarde": null,
                      "nieuweWaarde": true
                    },
                    {
                      "naam": "positieGeometrieMethode",
                      "oudeWaarde": null,
                      "nieuweWaarde": "afgeleidVanObject"
                    },
                    {
                      "naam": "positieSpecificatie",
                      "oudeWaarde": null,
                      "nieuweWaarde": "perceel"
                    },
                    {
                      "naam": "adresPositie",
                      "oudeWaarde": null,
                      "nieuweWaarde": [
                        {
                          "type": "Point",
                          "projectie": "http://www.opengis.net/def/crs/EPSG/0/31370",
                          "gml": "<gml:Point srsName=\"http://www.opengis.net/def/crs/EPSG/0/31370\" xmlns:gml=\"http://www.opengis.net/gml/3.2\"><gml:pos>109560.95 168981.82</gml:pos></gml:Point>"
                        }
                      ]
                    },
                    {
                      "naam": "busnummer",
                      "oudeWaarde": null,
                      "nieuweWaarde": "002"
                    }
                  ]
                }
              }
            ]
            """);

        _feedPageFetcher.SetupPage(1, events.ToFeedPage(isPageComplete: false));

        var act = async () => await RunOneCycleAsync(CancellationToken.None);

        await act.Should().ThrowAsync<ArgumentException>()
            .WithMessage("*Lambert 2008 (EPSG:3812)*");
    }

    [Fact]
    public async Task DeleteEvent_ShouldMarkAddressAsRemoved()
    {
        var events = await CloudEventTestHelper.ReadEventsFromFileAsync(
            Path.Combine("TestData", "address-create-update-retire-delete-boxnumber.json"));

        _feedPageFetcher.SetupPage(1, events.ToFeedPage(isPageComplete: false));

        using var cts = new CancellationTokenSource();
        cts.CancelAfter(5000);

        await RunOneCycleAsync(cts.Token);

        await using var context = _contextFactory.CreateDbContext();
        var address = await context.Addresses.FindAsync([PuriAddress31319625], TestContext.Current.CancellationToken);

        address.Should().NotBeNull();
        address!.IsRemoved.Should().BeTrue();
        address.VersionIdAsString.Should().Be(events[^1].GetVersionIdAsString());
    }

    [Fact]
    public async Task FullRetireDeleteSequence_ShouldResultInRemovedRetiredAddress()
    {
        var events = await CloudEventTestHelper.ReadEventsFromFileAsync(
            Path.Combine("TestData", "address-create-update-retire-delete-boxnumber.json"));

        _feedPageFetcher.SetupPage(1, events.ToFeedPage(isPageComplete: true));

        using var cts = new CancellationTokenSource();
        cts.CancelAfter(5000);

        await RunOneCycleAsync(cts.Token);

        await using var context = _contextFactory.CreateDbContext();
        var address = await context.Addresses.FindAsync([PuriAddress31319625], TestContext.Current.CancellationToken);

        address.Should().NotBeNull();
        address!.Status.Should().Be(AddressStatus.Retired);
        address.BoxNumber.Should().Be("002");
        address.IsRemoved.Should().BeTrue();
        address.VersionId.Should().Be(events[^1].GetVersionId());
        address.VersionIdAsString.Should().Be(events[^1].GetVersionIdAsString());
    }

    [Fact]
    public async Task FeedState_ShouldTrackPositionAfterProcessingEvents()
    {
        var events = await CloudEventTestHelper.ReadEventsFromFileAsync(
            Path.Combine("TestData", "address-create-update-transform.json"));

        _feedPageFetcher.SetupPage(1, events.ToFeedPage(isPageComplete: false));

        using var cts = new CancellationTokenSource();
        cts.CancelAfter(5000);

        await RunOneCycleAsync(cts.Token);

        await using var context = _contextFactory.CreateDbContext();
        var feedState = await context.FeedStates.FindAsync([AddressFeedName], TestContext.Current.CancellationToken);

        feedState.Should().NotBeNull();
        feedState!.EventPosition.Should().Be(5086476);
        feedState.Page.Should().Be(1);
    }

    [Fact]
    public async Task FeedState_ShouldIncrementPageWhenComplete()
    {
        var events = await CloudEventTestHelper.ReadEventsFromFileAsync(
            Path.Combine("TestData", "address-create-update-transform.json"));

        _feedPageFetcher.SetupPage(1, events.ToFeedPage(isPageComplete: true));

        using var cts = new CancellationTokenSource();
        cts.CancelAfter(5000);

        await RunOneCycleAsync(cts.Token);

        await using var context = _contextFactory.CreateDbContext();
        var feedState = await context.FeedStates.FindAsync([AddressFeedName], TestContext.Current.CancellationToken);

        feedState.Should().NotBeNull();
        feedState!.EventPosition.Should().Be(5086476);
        feedState.Page.Should().Be(2);
    }

    private async Task RunOneCycleAsync(CancellationToken cancellationToken)
    {
        await _projector.StartAsync(cancellationToken);
        await Task.Delay(500, CancellationToken.None);
        await _projector.StopAsync(CancellationToken.None);
    }
}
