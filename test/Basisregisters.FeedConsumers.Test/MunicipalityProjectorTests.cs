namespace Basisregisters.FeedConsumers.Test;

using System;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Console.Common;
using Console.Municipality;
using FluentAssertions;
using Infrastructure;
using Microsoft.Extensions.Logging.Abstractions;
using Model;
using Xunit;

public class MunicipalityProjectorTests
{
    private readonly InMemoryFeedContextFactory _contextFactory;
    private readonly FakeFeedPageFetcher _feedPageFetcher;
    private readonly MunicipalityProjector _projector;

    private const string PuriMunicipalityHasselt = "https://data.vlaanderen.be/id/gemeente/71022";
    private const string PuriMunicipalityAnderlecht = "https://data.vlaanderen.be/id/gemeente/21001";
    private const string MunicipalityFeedName = "MunicipalityFeed";

    public MunicipalityProjectorTests()
    {
        _contextFactory = new InMemoryFeedContextFactory();
        _feedPageFetcher = new FakeFeedPageFetcher();


        var options = new FeedProjectorOptions
        {
            Name = MunicipalityFeedName,
            FeedUrl = "https://test/feed",
            PollingIntervalInMinutes = 1,
            IgnoreNoEventHandlers = false
        };

        _projector = new MunicipalityProjector(
            options,
            _contextFactory,
            _feedPageFetcher,
            new NoOpJsonSchemaValidator(),
            new NullLoggerFactory());
    }

    [Fact]
    public async Task CreateEvent_ShouldAddMunicipalityWithCorrectData()
    {
        var events = await CloudEventTestHelper.ReadEventsFromFileAsync(
            Path.Combine("TestData", "municipality-create-update-transform.json"));

        // Only take the create event (id=2337)
        var createEvents = events.Where(e => e.Id == "2337").ToList();
        _feedPageFetcher.SetupPage(1, createEvents.ToFeedPage(isPageComplete: false));

        using var cts = new CancellationTokenSource();
        cts.CancelAfter(5000);

        await RunOneCycleAsync(cts.Token);

        await using var context = _contextFactory.CreateDbContext();

        var municipality = await context.Municipalities.FindAsync([PuriMunicipalityHasselt], TestContext.Current.CancellationToken);

        municipality.Should().NotBeNull();
        municipality!.NisCode.Should().Be("71022");
        municipality.Status.Should().Be(MunicipalityStatus.Proposed);
        municipality.IsRemoved.Should().BeFalse();
        municipality.VersionIdAsString.Should().Be(createEvents[^1].GetVersionIdAsString());
    }

    [Fact]
    public async Task CreateAndUpdateEvents_ShouldApplyAllChangesInSequence()
    {
        var events = await CloudEventTestHelper.ReadEventsFromFileAsync(
            Path.Combine("TestData", "municipality-create-update-transform.json"));

        // Take create + official language update + status update + naming events (ids 2337-2355)
        var relevantEvents = events
            .Where(e => long.Parse(e.Id!) <= 2355)
            .ToList();
        _feedPageFetcher.SetupPage(1, relevantEvents.ToFeedPage(isPageComplete: false));

        using var cts = new CancellationTokenSource();
        cts.CancelAfter(5000);

        await RunOneCycleAsync(cts.Token);

        await using var context = _contextFactory.CreateDbContext();
        var municipality = await context.Municipalities.FindAsync([PuriMunicipalityHasselt], TestContext.Current.CancellationToken);

        municipality.Should().NotBeNull();
        municipality!.NisCode.Should().Be("71022");
        municipality.Status.Should().Be(MunicipalityStatus.Current);
        municipality.OfficialLanguageDutch.Should().BeTrue();
        municipality.OfficialLanguageFrench.Should().BeFalse();
        municipality.OfficialLanguageGerman.Should().BeFalse();
        municipality.OfficialLanguageEnglish.Should().BeFalse();
        municipality.NameDutch.Should().Be("Hasselt");
        municipality.IsRemoved.Should().BeFalse();
        //2002-08-13T16:33:18+02:00
        municipality.VersionId.Should().BeCloseTo(new DateTimeOffset(2002, 08, 13, 16, 33, 18, TimeSpan.FromHours(2)), TimeSpan.FromSeconds(1));
        municipality.VersionIdAsString.Should().Be(relevantEvents[^1].GetVersionIdAsString());
    }

    [Fact]
    public async Task TransformEvent_ShouldBeIgnoredWithoutError()
    {
        var events = await CloudEventTestHelper.ReadEventsFromFileAsync(
            Path.Combine("TestData", "municipality-create-update-transform.json"));

        // Take all events including transform (id=2699) and the final update (id=2700)
        _feedPageFetcher.SetupPage(1, events.ToFeedPage(isPageComplete: false));

        using var cts = new CancellationTokenSource();
        cts.CancelAfter(5000);

        await RunOneCycleAsync(cts.Token);

        await using var context = _contextFactory.CreateDbContext();
        var municipality = await context.Municipalities.FindAsync([PuriMunicipalityHasselt], TestContext.Current.CancellationToken);

        municipality.Should().NotBeNull();
        municipality!.Status.Should().Be(MunicipalityStatus.Retired);
    }

    [Fact]
    public async Task FullCreateUpdateTransformSequence_ShouldResultInRetiredMunicipality()
    {
        var events = await CloudEventTestHelper.ReadEventsFromFileAsync(
            Path.Combine("TestData", "municipality-create-update-transform.json"));

        _feedPageFetcher.SetupPage(1, events.ToFeedPage(isPageComplete: true));

        using var cts = new CancellationTokenSource();
        cts.CancelAfter(5000);

        await RunOneCycleAsync(cts.Token);

        await using var context = _contextFactory.CreateDbContext();
        var municipality = await context.Municipalities.FindAsync([PuriMunicipalityHasselt], TestContext.Current.CancellationToken);

        municipality.Should().NotBeNull();
        municipality!.NisCode.Should().Be("71022");
        municipality.Status.Should().Be(MunicipalityStatus.Retired);
        municipality.OfficialLanguageDutch.Should().BeTrue();
        municipality.NameDutch.Should().Be("Hasselt");
        municipality.IsRemoved.Should().BeFalse();
        //2025-01-01T01:04:15+01:00
        municipality.VersionId.Should().BeCloseTo(new DateTimeOffset(2025, 01, 01, 01, 04, 15, TimeSpan.FromHours(1)), TimeSpan.FromSeconds(1));
        municipality.VersionIdAsString.Should().Be(events[^1].GetVersionIdAsString());
    }

    [Fact]
    public async Task DeleteEvent_ShouldMarkMunicipalityAsRemoved()
    {
        var events = await CloudEventTestHelper.ReadEventsFromFileAsync(
            Path.Combine("TestData", "municipality-delete-with-multiple-languages.json"));

        _feedPageFetcher.SetupPage(1, events.ToFeedPage(isPageComplete: false));

        using var cts = new CancellationTokenSource();
        cts.CancelAfter(5000);

        await RunOneCycleAsync(cts.Token);

        await using var context = _contextFactory.CreateDbContext();
        var municipality = await context.Municipalities.FindAsync([PuriMunicipalityAnderlecht], TestContext.Current.CancellationToken);

        municipality.Should().NotBeNull();
        municipality!.IsRemoved.Should().BeTrue();
        municipality.VersionIdAsString.Should().Be(events[^1].GetVersionIdAsString());
    }

    [Fact]
    public async Task MultipleLanguages_ShouldBeMappedCorrectly()
    {
        var events = await CloudEventTestHelper.ReadEventsFromFileAsync(
            Path.Combine("TestData", "municipality-delete-with-multiple-languages.json"));

        // Take all events up to and including the naming events (before delete)
        var relevantEvents = events
            .Where(e => long.Parse(e.Id!) <= 477)
            .ToList();
        _feedPageFetcher.SetupPage(1, relevantEvents.ToFeedPage(isPageComplete: false));

        using var cts = new CancellationTokenSource();
        cts.CancelAfter(5000);

        await RunOneCycleAsync(cts.Token);

        await using var context = _contextFactory.CreateDbContext();
        var municipality = await context.Municipalities.FindAsync([PuriMunicipalityAnderlecht], TestContext.Current.CancellationToken);

        municipality.Should().NotBeNull();
        municipality!.NisCode.Should().Be("21001");
        municipality.Status.Should().Be(MunicipalityStatus.Current);

        // Official languages: nl and fr
        municipality.OfficialLanguageDutch.Should().BeTrue();
        municipality.OfficialLanguageFrench.Should().BeTrue();
        municipality.OfficialLanguageGerman.Should().BeFalse();
        municipality.OfficialLanguageEnglish.Should().BeFalse();

        // Names: Anderlecht in both fr and nl
        municipality.NameFrench.Should().Be("Anderlecht");
        municipality.NameDutch.Should().Be("Anderlecht");
        municipality.NameGerman.Should().BeNull();
        municipality.NameEnglish.Should().BeNull();

        municipality.IsRemoved.Should().BeFalse();
    }

    [Fact]
    public async Task FullDeleteSequence_ShouldResultInRemovedMunicipalityWithAllData()
    {
        var events = await CloudEventTestHelper.ReadEventsFromFileAsync(
            Path.Combine("TestData", "municipality-delete-with-multiple-languages.json"));

        _feedPageFetcher.SetupPage(1, events.ToFeedPage(isPageComplete: true));

        using var cts = new CancellationTokenSource();
        cts.CancelAfter(5000);

        await RunOneCycleAsync(cts.Token);

        await using var context = _contextFactory.CreateDbContext();
        var municipality = await context.Municipalities.FindAsync([PuriMunicipalityAnderlecht], TestContext.Current.CancellationToken);

        municipality.Should().NotBeNull();
        municipality!.NisCode.Should().Be("21001");
        municipality.Status.Should().Be(MunicipalityStatus.Current);
        municipality.OfficialLanguageDutch.Should().BeTrue();
        municipality.OfficialLanguageFrench.Should().BeTrue();
        municipality.NameFrench.Should().Be("Anderlecht");
        municipality.NameDutch.Should().Be("Anderlecht");
        municipality.IsRemoved.Should().BeTrue();
        municipality.VersionIdAsString.Should().Be(events[^1].GetVersionIdAsString());
    }

    [Fact]
    public async Task FeedState_ShouldTrackPositionAfterProcessingEvents()
    {
        var events = await CloudEventTestHelper.ReadEventsFromFileAsync(
            Path.Combine("TestData", "municipality-create-update-transform.json"));

        _feedPageFetcher.SetupPage(1, events.ToFeedPage(isPageComplete: false));

        using var cts = new CancellationTokenSource();
        cts.CancelAfter(5000);

        await RunOneCycleAsync(cts.Token);

        await using var context = _contextFactory.CreateDbContext();
        var feedState = await context.FeedStates.FindAsync([MunicipalityFeedName], TestContext.Current.CancellationToken);

        feedState.Should().NotBeNull();
        // Highest event id in the file is 2700
        feedState!.EventPosition.Should().Be(2700);
        // Page not complete, should remain 1
        feedState.Page.Should().Be(1);
    }

    [Fact]
    public async Task FeedState_ShouldIncrementPageWhenComplete()
    {
        var events = await CloudEventTestHelper.ReadEventsFromFileAsync(
            Path.Combine("TestData", "municipality-create-update-transform.json"));

        _feedPageFetcher.SetupPage(1, events.ToFeedPage(isPageComplete: true));

        using var cts = new CancellationTokenSource();
        cts.CancelAfter(5000);

        await RunOneCycleAsync(cts.Token);

        await using var context = _contextFactory.CreateDbContext();
        var feedState = await context.FeedStates.FindAsync([MunicipalityFeedName], TestContext.Current.CancellationToken);

        feedState.Should().NotBeNull();
        feedState!.EventPosition.Should().Be(2700);
        // Page was marked complete, should advance to 2
        feedState.Page.Should().Be(2);
    }

    private async Task RunOneCycleAsync(CancellationToken cancellationToken)
    {
        // ExecuteAsync is protected, so we use StartAsync + short delay + StopAsync
        await _projector.StartAsync(cancellationToken);
        // Give it time to process the first cycle
        await Task.Delay(500, CancellationToken.None);
        await _projector.StopAsync(CancellationToken.None);
    }
}
