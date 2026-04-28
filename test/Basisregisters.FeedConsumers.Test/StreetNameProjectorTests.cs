namespace Basisregisters.FeedConsumers.Test;

using System;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Console.Common;
using Console.StreetName;
using FluentAssertions;
using Infrastructure;
using Microsoft.Extensions.Logging.Abstractions;
using Model;
using Xunit;

public class StreetNameProjectorTests
{
    private readonly InMemoryFeedContextFactory _contextFactory;
    private readonly FakeFeedPageFetcher _feedPageFetcher;
    private readonly StreetNameProjector _projector;

    private const string PuriStreetName18618 = "https://data.vlaanderen.be/id/straatnaam/18618";
    private const string PuriStreetName228493 = "https://data.vlaanderen.be/id/straatnaam/228493";
    private const string PuriStreetName227570 = "https://data.vlaanderen.be/id/straatnaam/227570";
    private const string StreetNameFeedName = "StreetNameFeed";

    public StreetNameProjectorTests()
    {
        _contextFactory = new InMemoryFeedContextFactory();
        _feedPageFetcher = new FakeFeedPageFetcher();

        var options = new FeedProjectorOptions
        {
            Name = StreetNameFeedName,
            FeedUrl = "https://test/feed",
            PollingIntervalInMinutes = 1,
            IgnoreNoEventHandlers = false
        };

        _projector = new StreetNameProjector(
            options,
            _contextFactory,
            _feedPageFetcher,
            new NoOpJsonSchemaValidator(),
            new NullLoggerFactory());
    }

    [Fact]
    public async Task CreateEvent_ShouldAddStreetNameWithCorrectData()
    {
        var events = await CloudEventTestHelper.ReadEventsFromFileAsync(
            Path.Combine("TestData", "streetname-create-multiple-names.json"));

        _feedPageFetcher.SetupPage(1, events.ToFeedPage(isPageComplete: false));

        using var cts = new CancellationTokenSource();
        cts.CancelAfter(5000);

        await RunOneCycleAsync(cts.Token);

        await using var context = _contextFactory.CreateDbContext();
        var streetName = await context.StreetNames.FindAsync([PuriStreetName18618], TestContext.Current.CancellationToken);

        streetName.Should().NotBeNull();
        streetName!.PersistentLocalId.Should().Be(18618);
        streetName.NisCode.Should().Be("21001");
        streetName.Status.Should().Be(StreetNameStatus.Current);
        streetName.IsRemoved.Should().BeFalse();
    }

    [Fact]
    public async Task CreateEvent_WithMultipleNames_ShouldMapAllLanguages()
    {
        var events = await CloudEventTestHelper.ReadEventsFromFileAsync(
            Path.Combine("TestData", "streetname-create-multiple-names.json"));

        _feedPageFetcher.SetupPage(1, events.ToFeedPage(isPageComplete: false));

        using var cts = new CancellationTokenSource();
        cts.CancelAfter(5000);

        await RunOneCycleAsync(cts.Token);

        await using var context = _contextFactory.CreateDbContext();
        var streetName = await context.StreetNames.FindAsync([PuriStreetName18618], TestContext.Current.CancellationToken);

        streetName.Should().NotBeNull();
        streetName!.NameDutch.Should().Be("Aakaai");
        streetName.NameFrench.Should().Be("Quai d'Aa");
        streetName.NameGerman.Should().BeNull();
        streetName.NameEnglish.Should().BeNull();
        // 2023-11-01T08:23:50+01:00
        streetName.VersionId.Should().BeCloseTo(new DateTimeOffset(2023, 11, 1, 8, 23, 50, TimeSpan.FromHours(1)), TimeSpan.FromSeconds(1));
        streetName.VersionIdAsString.Should().Be(events[^1].GetVersionIdAsString());
    }

    [Fact]
    public async Task CreateAndUpdateEvents_ShouldApplyNameCorrections()
    {
        var events = await CloudEventTestHelper.ReadEventsFromFileAsync(
            Path.Combine("TestData", "streetname-create-update-transform.json"));

        // Take create + two name correction updates (ids 159388-159390)
        var relevantEvents = events
            .Where(e => long.Parse(e.Id!) <= 159390)
            .ToList();
        _feedPageFetcher.SetupPage(1, relevantEvents.ToFeedPage(isPageComplete: false));

        using var cts = new CancellationTokenSource();
        cts.CancelAfter(5000);

        await RunOneCycleAsync(cts.Token);

        await using var context = _contextFactory.CreateDbContext();
        var streetName = await context.StreetNames.FindAsync([PuriStreetName228493], TestContext.Current.CancellationToken);

        streetName.Should().NotBeNull();
        streetName!.NisCode.Should().Be("73083");
        streetName.Status.Should().Be(StreetNameStatus.Proposed);
        streetName.NameDutch.Should().Be("Betenbroek");
        streetName.IsRemoved.Should().BeFalse();
        // 2023-11-21T10:28:42+01:00
        streetName.VersionId.Should().BeCloseTo(new DateTimeOffset(2023, 11, 21, 10, 28, 42, TimeSpan.FromHours(1)), TimeSpan.FromSeconds(1));
        streetName.VersionIdAsString.Should().Be(relevantEvents[^1].GetVersionIdAsString());
    }

    [Fact]
    public async Task TransformEvent_ShouldBeIgnoredWithoutError()
    {
        var events = await CloudEventTestHelper.ReadEventsFromFileAsync(
            Path.Combine("TestData", "streetname-create-update-transform.json"));

        // Take all events including the transform event
        var relevantEvents = events
            .Where(e => long.Parse(e.Id!) <= 184164)
            .ToList();
        _feedPageFetcher.SetupPage(1, relevantEvents.ToFeedPage(isPageComplete: false));

        using var cts = new CancellationTokenSource();
        cts.CancelAfter(5000);

        await RunOneCycleAsync(cts.Token);

        await using var context = _contextFactory.CreateDbContext();
        var streetName = await context.StreetNames.FindAsync([PuriStreetName228493], TestContext.Current.CancellationToken);

        // Transform event should be ignored; streetname should still exist with its last state
        streetName.Should().NotBeNull();
        streetName!.Status.Should().Be(StreetNameStatus.Proposed);
    }

    [Fact]
    public async Task FullCreateUpdateTransformSequence_ShouldResultInRejectedStreetName()
    {
        var events = await CloudEventTestHelper.ReadEventsFromFileAsync(
            Path.Combine("TestData", "streetname-create-update-transform.json"));

        _feedPageFetcher.SetupPage(1, events.ToFeedPage(isPageComplete: true));

        using var cts = new CancellationTokenSource();
        cts.CancelAfter(5000);

        await RunOneCycleAsync(cts.Token);

        await using var context = _contextFactory.CreateDbContext();
        var streetName = await context.StreetNames.FindAsync([PuriStreetName228493], TestContext.Current.CancellationToken);

        streetName.Should().NotBeNull();
        streetName!.NisCode.Should().Be("73083");
        streetName.Status.Should().Be(StreetNameStatus.Rejected);
        streetName.NameDutch.Should().Be("Betenbroek");
        streetName.IsRemoved.Should().BeFalse();
        // 2025-01-01T01:05:17+01:00
        streetName.VersionId.Should().BeCloseTo(new DateTimeOffset(2025, 1, 1, 1, 5, 17, TimeSpan.FromHours(1)), TimeSpan.FromSeconds(1));
        streetName.VersionIdAsString.Should().Be(events[^1].GetVersionIdAsString());
    }

    [Fact]
    public async Task CreateEvent_WithHomonymAdditions_ShouldMapCorrectly()
    {
        var events = await CloudEventTestHelper.ReadEventsFromFileAsync(
            Path.Combine("TestData", "streetname-homonym-delete.json"));

        // Only take the create event
        var createEvents = events.Where(e => e.Id == "159199").ToList();
        _feedPageFetcher.SetupPage(1, createEvents.ToFeedPage(isPageComplete: false));

        using var cts = new CancellationTokenSource();
        cts.CancelAfter(5000);

        await RunOneCycleAsync(cts.Token);

        await using var context = _contextFactory.CreateDbContext();
        var streetName = await context.StreetNames.FindAsync([PuriStreetName227570], TestContext.Current.CancellationToken);

        streetName.Should().NotBeNull();
        streetName!.NisCode.Should().Be("44083");
        streetName.Status.Should().Be(StreetNameStatus.Proposed);
        streetName.NameDutch.Should().Be("Kouterslag");
        streetName.NameFrench.Should().BeNull();
        streetName.HomonymAdditionDutch.Should().Be("zij");
        streetName.HomonymAdditionFrench.Should().BeNull();
        streetName.IsRemoved.Should().BeFalse();
    }

    [Fact]
    public async Task DeleteEvent_ShouldMarkStreetNameAsRemoved()
    {
        var events = await CloudEventTestHelper.ReadEventsFromFileAsync(
            Path.Combine("TestData", "streetname-homonym-delete.json"));

        _feedPageFetcher.SetupPage(1, events.ToFeedPage(isPageComplete: false));

        using var cts = new CancellationTokenSource();
        cts.CancelAfter(5000);

        await RunOneCycleAsync(cts.Token);

        await using var context = _contextFactory.CreateDbContext();
        var streetName = await context.StreetNames.FindAsync([PuriStreetName227570], TestContext.Current.CancellationToken);

        streetName.Should().NotBeNull();
        streetName!.IsRemoved.Should().BeTrue();
        streetName.VersionIdAsString.Should().Be(events[^1].GetVersionIdAsString());
    }

    [Fact]
    public async Task FullDeleteSequence_ShouldResultInRemovedStreetNameWithAllData()
    {
        var events = await CloudEventTestHelper.ReadEventsFromFileAsync(
            Path.Combine("TestData", "streetname-homonym-delete.json"));

        _feedPageFetcher.SetupPage(1, events.ToFeedPage(isPageComplete: true));

        using var cts = new CancellationTokenSource();
        cts.CancelAfter(5000);

        await RunOneCycleAsync(cts.Token);

        await using var context = _contextFactory.CreateDbContext();
        var streetName = await context.StreetNames.FindAsync([PuriStreetName227570], TestContext.Current.CancellationToken);

        streetName.Should().NotBeNull();
        streetName!.NisCode.Should().Be("44083");
        streetName.Status.Should().Be(StreetNameStatus.Proposed);
        streetName.NameDutch.Should().Be("Kouterslag_zij");
        streetName.HomonymAdditionDutch.Should().Be("zij");
        streetName.IsRemoved.Should().BeTrue();
        streetName.VersionIdAsString.Should().Be(events[^1].GetVersionIdAsString());
    }

    [Fact]
    public async Task FeedState_ShouldTrackPositionAfterProcessingEvents()
    {
        var events = await CloudEventTestHelper.ReadEventsFromFileAsync(
            Path.Combine("TestData", "streetname-create-update-transform.json"));

        _feedPageFetcher.SetupPage(1, events.ToFeedPage(isPageComplete: false));

        using var cts = new CancellationTokenSource();
        cts.CancelAfter(5000);

        await RunOneCycleAsync(cts.Token);

        await using var context = _contextFactory.CreateDbContext();
        var feedState = await context.FeedStates.FindAsync([StreetNameFeedName], TestContext.Current.CancellationToken);

        feedState.Should().NotBeNull();
        // Highest event id in the file is 184165
        feedState!.EventPosition.Should().Be(184165);
        // Page not complete, should remain 1
        feedState.Page.Should().Be(1);
    }

    [Fact]
    public async Task FeedState_ShouldIncrementPageWhenComplete()
    {
        var events = await CloudEventTestHelper.ReadEventsFromFileAsync(
            Path.Combine("TestData", "streetname-create-update-transform.json"));

        _feedPageFetcher.SetupPage(1, events.ToFeedPage(isPageComplete: true));

        using var cts = new CancellationTokenSource();
        cts.CancelAfter(5000);

        await RunOneCycleAsync(cts.Token);

        await using var context = _contextFactory.CreateDbContext();
        var feedState = await context.FeedStates.FindAsync([StreetNameFeedName], TestContext.Current.CancellationToken);

        feedState.Should().NotBeNull();
        feedState!.EventPosition.Should().Be(184165);
        // Page was marked complete, should advance to 2
        feedState.Page.Should().Be(2);
    }

    private async Task RunOneCycleAsync(CancellationToken cancellationToken)
    {
        await _projector.StartAsync(cancellationToken);
        await Task.Delay(500, CancellationToken.None);
        await _projector.StopAsync(CancellationToken.None);
    }
}
