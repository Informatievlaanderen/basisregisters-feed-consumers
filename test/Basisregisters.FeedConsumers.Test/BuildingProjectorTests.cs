namespace Basisregisters.FeedConsumers.Test;

using System;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Console.Building;
using Console.Common;
using FluentAssertions;
using Infrastructure;
using Microsoft.Extensions.Logging.Abstractions;
using Model;
using NetTopologySuite.Geometries;
using Xunit;

public class BuildingProjectorTests
{
    private readonly InMemoryFeedContextFactory _contextFactory;
    private readonly FakeFeedPageFetcher _feedPageFetcher;
    private readonly BuildingProjector _projector;

    private const string PuriBuilding5811158 = "https://data.vlaanderen.be/id/gebouw/5811158";
    private const string PuriBuilding5666604 = "https://data.vlaanderen.be/id/gebouw/5666604";
    private const string PuriBuilding5666666 = "https://data.vlaanderen.be/id/gebouw/5666666";
    private const string BuildingFeedName = "BuildingFeed";

    public BuildingProjectorTests()
    {
        _contextFactory = new InMemoryFeedContextFactory();
        _feedPageFetcher = new FakeFeedPageFetcher();

        var options = new FeedProjectorOptions
        {
            Name = BuildingFeedName,
            FeedUrl = "https://test/feed",
            PollingIntervalInMinutes = 1,
            IgnoreNoEventHandlers = false
        };

        _projector = new BuildingProjector(
            options,
            _contextFactory,
            _feedPageFetcher,
            new NoOpJsonSchemaValidator(),
            new NullLoggerFactory());
    }

    [Fact]
    public async Task CreateEvent_ShouldAddBuildingWithCorrectData()
    {
        var events = await CloudEventTestHelper.ReadEventsFromFileAsync(
            Path.Combine("TestData", "building-create-update-delete.json"));

        var createEvents = events.Where(e => e.Id == "544263").ToList();
        _feedPageFetcher.SetupPage(1, createEvents.ToFeedPage(isPageComplete: false));

        using var cts = new CancellationTokenSource();
        cts.CancelAfter(5000);

        await RunOneCycleAsync(cts.Token);

        await using var context = _contextFactory.CreateDbContext();
        var building = await context.Buildings.FindAsync([PuriBuilding5811158], TestContext.Current.CancellationToken);

        building.Should().NotBeNull();
        building!.PersistentLocalId.Should().Be(5811158);
        building.Status.Should().Be(BuildingStatus.Realized);
        building.GeometryMethod.Should().Be(BuildingGeometryMethod.Outlined);
        building.IsRemoved.Should().BeFalse();
        building.VersionId.Should().BeCloseTo(new DateTimeOffset(2023, 11, 2, 10, 42, 30, TimeSpan.FromHours(1)), TimeSpan.FromSeconds(1));

        building.Geometry.Should().BeOfType<Polygon>();
        var polygon = (Polygon)building.Geometry;
        polygon.SRID.Should().Be(3812);
        polygon.Coordinates[0].X.Should().BeApproximately(642532.58, 0.01);
        polygon.Coordinates[0].Y.Should().BeApproximately(687064.33, 0.01);
    }

    [Fact]
    public async Task UpdateEvents_ShouldApplyLatestGeometryAndVersion()
    {
        var events = await CloudEventTestHelper.ReadEventsFromFileAsync(
            Path.Combine("TestData", "building-create-many-updates.json"));

        _feedPageFetcher.SetupPage(1, events.ToFeedPage(isPageComplete: false));

        using var cts = new CancellationTokenSource();
        cts.CancelAfter(5000);

        await RunOneCycleAsync(cts.Token);

        await using var context = _contextFactory.CreateDbContext();
        var building = await context.Buildings.FindAsync([PuriBuilding5666604], TestContext.Current.CancellationToken);

        building.Should().NotBeNull();
        building!.Status.Should().Be(BuildingStatus.Realized);
        building.GeometryMethod.Should().Be(BuildingGeometryMethod.MeasuredByGrb);
        building.IsRemoved.Should().BeFalse();
        building.VersionId.Should().BeCloseTo(new DateTimeOffset(2025, 1, 25, 23, 10, 42, TimeSpan.FromHours(1)), TimeSpan.FromSeconds(1));

        building.Geometry.Should().BeOfType<Polygon>();
        var polygon = (Polygon)building.Geometry;
        polygon.SRID.Should().Be(3812);
        polygon.Coordinates[0].X.Should().BeApproximately(639571.12, 0.01);
        polygon.Coordinates[0].Y.Should().BeApproximately(683124.76, 0.01);
    }

    [Fact]
    public async Task UpdateEvent_ShouldApplyRetiredStatus()
    {
        var events = await CloudEventTestHelper.ReadEventsFromFileAsync(
            Path.Combine("TestData", "building-create-retire.json"));

        _feedPageFetcher.SetupPage(1, events.ToFeedPage(isPageComplete: false));

        using var cts = new CancellationTokenSource();
        cts.CancelAfter(5000);

        await RunOneCycleAsync(cts.Token);

        await using var context = _contextFactory.CreateDbContext();
        var building = await context.Buildings.FindAsync([PuriBuilding5666666], TestContext.Current.CancellationToken);

        building.Should().NotBeNull();
        building!.Status.Should().Be(BuildingStatus.Retired);
        building.GeometryMethod.Should().Be(BuildingGeometryMethod.MeasuredByGrb);
        building.IsRemoved.Should().BeFalse();
        building.VersionId.Should().BeCloseTo(new DateTimeOffset(2024, 11, 9, 19, 11, 6, TimeSpan.FromHours(1)), TimeSpan.FromSeconds(1));
    }

    [Fact]
    public async Task DeleteEvent_ShouldMarkBuildingAsRemoved()
    {
        var events = await CloudEventTestHelper.ReadEventsFromFileAsync(
            Path.Combine("TestData", "building-create-update-delete.json"));

        _feedPageFetcher.SetupPage(1, events.ToFeedPage(isPageComplete: false));

        using var cts = new CancellationTokenSource();
        cts.CancelAfter(5000);

        await RunOneCycleAsync(cts.Token);

        await using var context = _contextFactory.CreateDbContext();
        var building = await context.Buildings.FindAsync([PuriBuilding5811158], TestContext.Current.CancellationToken);

        building.Should().NotBeNull();
        building!.Status.Should().Be(BuildingStatus.Realized);
        building.IsRemoved.Should().BeTrue();
        building.VersionId.Should().BeCloseTo(new DateTimeOffset(2025, 2, 24, 15, 10, 26, TimeSpan.FromHours(1)), TimeSpan.FromSeconds(1));
    }

    [Fact]
    public async Task CreateEvent_WithoutLambert2008Geometry_ShouldThrow()
    {
        var events = await CloudEventTestHelper.ReadEventsFromJsonAsync(
            """
            [
              {
                "specversion": "1.0",
                "id": "9991001",
                "time": "2025-04-18T11:52:12.5955391+02:00",
                "type": "basisregisters.building.create.v1",
                "source": "https://api.basisregisters.staging-vlaanderen.be/v2/feeds/wijzigingen/gebouwen",
                "datacontenttype": "application/json",
                "dataschema": "https://docs.basisregisters.staging-vlaanderen.be/schemas/feeds/wijzigingen/gebouw/2026-01-21/gebouw.json",
                "basisregisterseventtype": "BuildingWasMigrated",
                "basisregisterscausationid": "bd0f564c-ad0e-514f-94b5-d1c889302e48",
                "data": {
                  "@id": "https://data.vlaanderen.be/id/gebouw/31319625",
                  "objectId": "31319625",
                  "naamruimte": "https://data.vlaanderen.be/id/gebouw",
                  "versieId": "2025-04-18T11:52:12+02:00",
                  "nisCodes": [ "41081" ],
                  "attributen": [
                    {
                      "naam": "gebouwStatus",
                      "oudeWaarde": null,
                      "nieuweWaarde": "gerealiseerd"
                    },
                    {
                      "naam": "geometrieMethode",
                      "oudeWaarde": null,
                      "nieuweWaarde": "ingeschetst"
                    },
                    {
                      "naam": "gebouwGeometrie",
                      "oudeWaarde": null,
                      "nieuweWaarde": [
                        {
                          "type": "Polygon",
                          "projectie": "http://www.opengis.net/def/crs/EPSG/0/31370",
                          "gml": "<gml:Polygon srsName=\"http://www.opengis.net/def/crs/EPSG/0/31370\" xmlns:gml=\"http://www.opengis.net/gml/3.2\"><gml:exterior><gml:LinearRing><gml:posList>142534.85208049999 187064.83862719999 142533.98317409307 187059.90521990135 142528.05677408725 187056.73901189864 142532.28179809451 187049.13427589461 142545.07210093999 187056.24261001000 142540.42409165000 187064.11062575001 142534.85208049999 187064.83862719999</gml:posList></gml:LinearRing></gml:exterior></gml:Polygon>"
                        }
                      ]
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
    public async Task FeedState_ShouldTrackPositionAfterProcessingEvents()
    {
        var events = await CloudEventTestHelper.ReadEventsFromFileAsync(
            Path.Combine("TestData", "building-create-update-delete.json"));

        _feedPageFetcher.SetupPage(1, events.ToFeedPage(isPageComplete: false));

        using var cts = new CancellationTokenSource();
        cts.CancelAfter(5000);

        await RunOneCycleAsync(cts.Token);

        await using var context = _contextFactory.CreateDbContext();
        var feedState = await context.FeedStates.FindAsync([BuildingFeedName], TestContext.Current.CancellationToken);

        feedState.Should().NotBeNull();
        feedState!.EventPosition.Should().Be(6610857);
        feedState.Page.Should().Be(1);
    }

    [Fact]
    public async Task FeedState_ShouldIncrementPageWhenComplete()
    {
        var events = await CloudEventTestHelper.ReadEventsFromFileAsync(
            Path.Combine("TestData", "building-create-update-delete.json"));

        _feedPageFetcher.SetupPage(1, events.ToFeedPage(isPageComplete: true));

        using var cts = new CancellationTokenSource();
        cts.CancelAfter(5000);

        await RunOneCycleAsync(cts.Token);

        await using var context = _contextFactory.CreateDbContext();
        var feedState = await context.FeedStates.FindAsync([BuildingFeedName], TestContext.Current.CancellationToken);

        feedState.Should().NotBeNull();
        feedState!.EventPosition.Should().Be(6610857);
        feedState.Page.Should().Be(2);
    }

    private async Task RunOneCycleAsync(CancellationToken cancellationToken)
    {
        await _projector.StartAsync(cancellationToken);
        await Task.Delay(500, CancellationToken.None);
        await _projector.StopAsync(CancellationToken.None);
    }
}
