namespace Basisregisters.FeedConsumers.Test;

using System;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Console.BuildingUnit;
using Console.Common;
using FluentAssertions;
using Infrastructure;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;
using Model;
using NetTopologySuite.Geometries;
using Xunit;

public class BuildingUnitProjectorTests
{
    private readonly InMemoryFeedContextFactory _contextFactory;
    private readonly FakeFeedPageFetcher _feedPageFetcher;
    private readonly BuildingUnitProjector _projector;

    private const string PuriBuildingUnit5812645 = "https://data.vlaanderen.be/id/gebouweenheid/5812645";
    private const string PuriBuildingUnit5674659 = "https://data.vlaanderen.be/id/gebouweenheid/5674659";
    private const string PuriBuildingUnit5682336 = "https://data.vlaanderen.be/id/gebouweenheid/5682336";
    private const string PuriBuildingUnit31779857 = "https://data.vlaanderen.be/id/gebouweenheid/31779857";
    private const string BuildingUnitFeedName = "BuildingUnitFeed";

    public BuildingUnitProjectorTests()
    {
        _contextFactory = new InMemoryFeedContextFactory();
        _feedPageFetcher = new FakeFeedPageFetcher();

        var options = new FeedProjectorOptions
        {
            Name = BuildingUnitFeedName,
            FeedUrl = "https://test/feed",
            PollingIntervalInMinutes = 1,
            IgnoreNoEventHandlers = false
        };

        _projector = new BuildingUnitProjector(
            options,
            _contextFactory,
            _feedPageFetcher,
            new NoOpJsonSchemaValidator(),
            new NullLoggerFactory());
    }

    [Fact]
    public async Task CreateEvent_ShouldAddBuildingUnitWithCorrectData()
    {
        var events = await CloudEventTestHelper.ReadEventsFromFileAsync(
            Path.Combine("TestData", "buildingunit-create-update-addresses.json"));

        var createEvents = events.Where(e => e.Id == "1348366").ToList();
        _feedPageFetcher.SetupPage(1, createEvents.ToFeedPage(isPageComplete: false));

        using var cts = new CancellationTokenSource();
        cts.CancelAfter(5000);

        await RunOneCycleAsync(cts.Token);

        await using var context = _contextFactory.CreateDbContext();
        var buildingUnit = await context.BuildingUnits.FindAsync([PuriBuildingUnit5674659], TestContext.Current.CancellationToken);
        var addressLinks = await context.BuildingUnitAddresses
            .Where(x => x.BuildingUnitPersistentLocalId == 5674659)
            .ToListAsync(TestContext.Current.CancellationToken);

        buildingUnit.Should().NotBeNull();
        buildingUnit!.PersistentLocalId.Should().Be(5674659);
        buildingUnit.BuildingPersistentLocalId.Should().Be(5673542);
        buildingUnit.Status.Should().Be(BuildingUnitStatus.Realized);
        buildingUnit.Function.Should().Be(BuildingUnitFunction.Unknown);
        buildingUnit.GeometryMethod.Should().Be(BuildingUnitGeometryMethod.AppointedByAdministrator);
        buildingUnit.HasDeviation.Should().BeFalse();
        buildingUnit.IsRemoved.Should().BeFalse();
        buildingUnit.VersionId.Should().BeCloseTo(new DateTimeOffset(2023, 11, 2, 13, 17, 3, TimeSpan.FromHours(1)), TimeSpan.FromSeconds(1));
        buildingUnit.VersionIdAsString.Should().Be(createEvents[^1].GetVersionIdAsString());

        buildingUnit.Position.Should().BeOfType<Point>();
        var point = (Point)buildingUnit.Position;
        point.SRID.Should().Be(3812);
        point.X.Should().BeApproximately(575039.36, 0.01);
        point.Y.Should().BeApproximately(680980.16, 0.01);

        addressLinks.Should().ContainSingle();
        addressLinks[0].AddressPersistentLocalId.Should().Be(698483);
    }

    [Fact]
    public async Task UpdateEvent_ShouldReplaceAddresses()
    {
        var events = await CloudEventTestHelper.ReadEventsFromFileAsync(
            Path.Combine("TestData", "buildingunit-create-update-addresses.json"));

        _feedPageFetcher.SetupPage(1, events.ToFeedPage(isPageComplete: false));

        using var cts = new CancellationTokenSource();
        cts.CancelAfter(5000);

        await RunOneCycleAsync(cts.Token);

        await using var context = _contextFactory.CreateDbContext();
        var buildingUnit = await context.BuildingUnits.FindAsync([PuriBuildingUnit5674659], TestContext.Current.CancellationToken);
        var addressLinks = await context.BuildingUnitAddresses
            .Where(x => x.BuildingUnitPersistentLocalId == 5674659)
            .OrderBy(x => x.AddressPersistentLocalId)
            .ToListAsync(TestContext.Current.CancellationToken);

        buildingUnit.Should().NotBeNull();
        buildingUnit!.VersionId.Should().BeCloseTo(new DateTimeOffset(2025, 1, 1, 2, 17, 0, TimeSpan.FromHours(1)), TimeSpan.FromSeconds(1));
        buildingUnit.VersionIdAsString.Should().Be(events[^1].GetVersionIdAsString());
        addressLinks.Should().ContainSingle();
        addressLinks[0].AddressPersistentLocalId.Should().Be(30720540);
    }

    [Fact]
    public async Task UpdateEvents_ShouldAccumulateMultipleAddresses()
    {
        var events = await CloudEventTestHelper.ReadEventsFromFileAsync(
            Path.Combine("TestData", "buildingunit-multiple-addresses.json"));

        _feedPageFetcher.SetupPage(1, events.ToFeedPage(isPageComplete: false));

        using var cts = new CancellationTokenSource();
        cts.CancelAfter(5000);

        await RunOneCycleAsync(cts.Token);

        await using var context = _contextFactory.CreateDbContext();
        var buildingUnit = await context.BuildingUnits.FindAsync([PuriBuildingUnit31779857], TestContext.Current.CancellationToken);
        var addressLinks = await context.BuildingUnitAddresses
            .Where(x => x.BuildingUnitPersistentLocalId == 31779857)
            .OrderBy(x => x.AddressPersistentLocalId)
            .ToListAsync(TestContext.Current.CancellationToken);

        buildingUnit.Should().NotBeNull();
        buildingUnit!.PersistentLocalId.Should().Be(31779857);
        buildingUnit.BuildingPersistentLocalId.Should().Be(31732080);
        buildingUnit.Status.Should().Be(BuildingUnitStatus.Realized);
        buildingUnit.Function.Should().Be(BuildingUnitFunction.Unknown);
        buildingUnit.GeometryMethod.Should().Be(BuildingUnitGeometryMethod.AppointedByAdministrator);
        buildingUnit.HasDeviation.Should().BeFalse();
        buildingUnit.IsRemoved.Should().BeFalse();
        buildingUnit.VersionId.Should().BeCloseTo(new DateTimeOffset(2026, 1, 27, 11, 23, 30, TimeSpan.FromHours(1)), TimeSpan.FromSeconds(1));
        buildingUnit.VersionIdAsString.Should().Be(events[^1].GetVersionIdAsString());

        buildingUnit.Position.Should().BeOfType<Point>();
        var point = (Point)buildingUnit.Position;
        point.SRID.Should().Be(3812);
        point.X.Should().BeApproximately(558076.16, 0.01);
        point.Y.Should().BeApproximately(676333.22, 0.01);

        addressLinks.Should().HaveCount(4);
        addressLinks.Select(x => x.AddressPersistentLocalId).Should().Equal(31320594, 31320595, 31320596, 31320597);
    }

    [Fact]
    public async Task UpdateEvents_ShouldApplyBuildingAndPositionChanges()
    {
        var events = await CloudEventTestHelper.ReadEventsFromFileAsync(
            Path.Combine("TestData", "buildingunit-create-change-building.json"));

        var relevantEvents = events
            .Where(e => long.Parse(e.Id!) <= 6277570)
            .ToList();
        _feedPageFetcher.SetupPage(1, relevantEvents.ToFeedPage(isPageComplete: false));

        using var cts = new CancellationTokenSource();
        cts.CancelAfter(5000);

        await RunOneCycleAsync(cts.Token);

        await using var context = _contextFactory.CreateDbContext();
        var buildingUnit = await context.BuildingUnits.FindAsync([PuriBuildingUnit5682336], TestContext.Current.CancellationToken);

        buildingUnit.Should().NotBeNull();
        buildingUnit!.Status.Should().Be(BuildingUnitStatus.Retired);
        buildingUnit.BuildingPersistentLocalId.Should().Be(6117910);
        buildingUnit.Function.Should().Be(BuildingUnitFunction.Unknown);
        buildingUnit.GeometryMethod.Should().Be(BuildingUnitGeometryMethod.DerivedFromObject);
        buildingUnit.HasDeviation.Should().BeFalse();
        buildingUnit.IsRemoved.Should().BeFalse();
        buildingUnit.VersionId.Should().BeCloseTo(new DateTimeOffset(2025, 1, 14, 11, 21, 5, TimeSpan.FromHours(1)), TimeSpan.FromSeconds(1));
        buildingUnit.VersionIdAsString.Should().Be(relevantEvents[^1].GetVersionIdAsString());

        buildingUnit.Position.Should().BeOfType<Point>();
        var point = (Point)buildingUnit.Position;
        point.SRID.Should().Be(3812);
        point.X.Should().BeApproximately(658066.75, 0.01);
        point.Y.Should().BeApproximately(721532.42, 0.01);
    }

    [Fact]
    public async Task DeleteEvent_ShouldMarkBuildingUnitAsRemoved()
    {
        var events = await CloudEventTestHelper.ReadEventsFromFileAsync(
            Path.Combine("TestData", "buildingunit-create-retire-remove.json"));

        _feedPageFetcher.SetupPage(1, events.ToFeedPage(isPageComplete: false));

        using var cts = new CancellationTokenSource();
        cts.CancelAfter(5000);

        await RunOneCycleAsync(cts.Token);

        await using var context = _contextFactory.CreateDbContext();
        var buildingUnit = await context.BuildingUnits.FindAsync([PuriBuildingUnit5812645], TestContext.Current.CancellationToken);
        var addressLinks = await context.BuildingUnitAddresses
            .Where(x => x.BuildingUnitPersistentLocalId == 5812645)
            .ToListAsync(TestContext.Current.CancellationToken);

        buildingUnit.Should().NotBeNull();
        buildingUnit!.Status.Should().Be(BuildingUnitStatus.Retired);
        buildingUnit.IsRemoved.Should().BeTrue();
        buildingUnit.VersionId.Should().Be(events[^1].GetVersionId());
        buildingUnit.VersionIdAsString.Should().Be(events[^1].GetVersionIdAsString());
        addressLinks.Should().BeEmpty();
    }

    [Fact]
    public async Task CreateEvent_WithoutLambert2008Geometry_ShouldThrow()
    {
        var events = await CloudEventTestHelper.ReadEventsFromJsonAsync(
            """
            [
              {
                "specversion": "1.0",
                "id": "9992001",
                "time": "2025-04-18T11:52:12.5955391+02:00",
                "type": "basisregisters.buildingunit.create.v1",
                "source": "https://api.basisregisters.staging-vlaanderen.be/v2/feeds/wijzigingen/gebouweenheden",
                "datacontenttype": "application/json",
                "dataschema": "https://docs.basisregisters.staging-vlaanderen.be/schemas/feeds/wijzigingen/gebouweenheid/2026-01-21/gebouweenheid.json",
                "basisregisterseventtype": "BuildingWasMigrated",
                "basisregisterscausationid": "bd0f564c-ad0e-514f-94b5-d1c889302e48",
                "data": {
                  "@id": "https://data.vlaanderen.be/id/gebouweenheid/31319625",
                  "objectId": "31319625",
                  "naamruimte": "https://data.vlaanderen.be/id/gebouweenheid",
                  "versieId": "2025-04-18T11:52:12+02:00",
                  "nisCodes": [ "41081" ],
                  "attributen": [
                    {
                      "naam": "gebouweenheidStatus",
                      "oudeWaarde": null,
                      "nieuweWaarde": "gerealiseerd"
                    },
                    {
                      "naam": "gebouweenheidFunctie",
                      "oudeWaarde": null,
                      "nieuweWaarde": "nietGekend"
                    },
                    {
                      "naam": "positieGeometrieMethode",
                      "oudeWaarde": null,
                      "nieuweWaarde": "afgeleidVanObject"
                    },
                    {
                      "naam": "gebouweenheidPositie",
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
                      "naam": "adresIds",
                      "oudeWaarde": null,
                      "nieuweWaarde": []
                    },
                    {
                      "naam": "gebouw.id",
                      "oudeWaarde": null,
                      "nieuweWaarde": "https://data.vlaanderen.be/id/gebouw/31319625"
                    },
                    {
                      "naam": "afwijkingVastgesteld",
                      "oudeWaarde": null,
                      "nieuweWaarde": false
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
            Path.Combine("TestData", "buildingunit-create-retire-remove.json"));

        _feedPageFetcher.SetupPage(1, events.ToFeedPage(isPageComplete: false));

        using var cts = new CancellationTokenSource();
        cts.CancelAfter(5000);

        await RunOneCycleAsync(cts.Token);

        await using var context = _contextFactory.CreateDbContext();
        var feedState = await context.FeedStates.FindAsync([BuildingUnitFeedName], TestContext.Current.CancellationToken);

        feedState.Should().NotBeNull();
        feedState!.EventPosition.Should().Be(6343588);
        feedState.Page.Should().Be(1);
    }

    [Fact]
    public async Task FeedState_ShouldIncrementPageWhenComplete()
    {
        var events = await CloudEventTestHelper.ReadEventsFromFileAsync(
            Path.Combine("TestData", "buildingunit-create-retire-remove.json"));

        _feedPageFetcher.SetupPage(1, events.ToFeedPage(isPageComplete: true));

        using var cts = new CancellationTokenSource();
        cts.CancelAfter(5000);

        await RunOneCycleAsync(cts.Token);

        await using var context = _contextFactory.CreateDbContext();
        var feedState = await context.FeedStates.FindAsync([BuildingUnitFeedName], TestContext.Current.CancellationToken);

        feedState.Should().NotBeNull();
        feedState!.EventPosition.Should().Be(6343588);
        feedState.Page.Should().Be(2);
    }

    private async Task RunOneCycleAsync(CancellationToken cancellationToken)
    {
        await _projector.StartAsync(cancellationToken);
        await Task.Delay(500, CancellationToken.None);
        await _projector.StopAsync(CancellationToken.None);
    }
}
