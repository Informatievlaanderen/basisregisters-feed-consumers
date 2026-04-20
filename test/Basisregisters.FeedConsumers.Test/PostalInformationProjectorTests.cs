namespace Basisregisters.FeedConsumers.Test;

using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Console.Common;
using Console.PostalInformation;
using FluentAssertions;
using Infrastructure;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;
using Model;
using Xunit;

public class PostalInformationProjectorTests
{
    private readonly InMemoryFeedContextFactory _contextFactory;
    private readonly FakeFeedPageFetcher _feedPageFetcher;
    private readonly PostalInformationProjector _projector;

    private const string PuriPostalInfo9000 = "https://data.vlaanderen.be/id/postinfo/9000";
    private const string PuriPostalInfo1000 = "https://data.vlaanderen.be/id/postinfo/1000";
    private const string PostalInformationFeedName = "PostalInformationFeed";

    public PostalInformationProjectorTests()
    {
        _contextFactory = new InMemoryFeedContextFactory();
        _feedPageFetcher = new FakeFeedPageFetcher();

        var options = new FeedProjectorOptions
        {
            Name = PostalInformationFeedName,
            FeedUrl = "https://test/feed",
            PollingIntervalInMinutes = 1,
            IgnoreNoEventHandlers = false
        };

        _projector = new PostalInformationProjector(
            options,
            _contextFactory,
            _feedPageFetcher,
            new NoOpJsonSchemaValidator(),
            new NullLoggerFactory());
    }

    [Fact]
    public async Task CreateEvent_ShouldAddPostalInformationWithCorrectData()
    {
        var events = await CloudEventTestHelper.ReadEventsFromFileAsync(
            Path.Combine("TestData", "postalinformation-create-update.json"));

        var createEvents = events.Where(e => e.Id == "100").ToList();
        _feedPageFetcher.SetupPage(1, createEvents.ToFeedPage(isPageComplete: false));

        using var cts = new CancellationTokenSource();
        cts.CancelAfter(5000);

        await RunOneCycleAsync(cts.Token);

        await using var context = _contextFactory.CreateDbContext();

        var postalInformation = await context.PostalInformations.FindAsync([PuriPostalInfo9000], TestContext.Current.CancellationToken);

        postalInformation.Should().NotBeNull();
        postalInformation!.PostalCode.Should().Be("9000");
        postalInformation.Status.Should().Be(PostalInformationStatus.Realized);
        postalInformation.NisCode.Should().BeNull();
        postalInformation.IsRemoved.Should().BeFalse();
    }

    [Fact]
    public async Task CreateAndUpdateEvents_ShouldApplyMunicipalityAndNames()
    {
        var events = await CloudEventTestHelper.ReadEventsFromFileAsync(
            Path.Combine("TestData", "postalinformation-create-update.json"));

        // Take create + municipality + first naming events (ids 100-102)
        var relevantEvents = events
            .Where(e => long.Parse(e.Id!) <= 102)
            .ToList();
        _feedPageFetcher.SetupPage(1, relevantEvents.ToFeedPage(isPageComplete: false));

        using var cts = new CancellationTokenSource();
        cts.CancelAfter(5000);

        await RunOneCycleAsync(cts.Token);

        await using var context = _contextFactory.CreateDbContext();
        var postalInformation = await context.PostalInformations
            .Include(p => p.PostalNames)
            .FirstOrDefaultAsync(p => p.PersistentUri == PuriPostalInfo9000, TestContext.Current.CancellationToken);

        postalInformation.Should().NotBeNull();
        postalInformation!.PostalCode.Should().Be("9000");
        postalInformation.NisCode.Should().Be("44021");
        postalInformation.Status.Should().Be(PostalInformationStatus.Realized);
        postalInformation.PostalNames.Should().HaveCount(1);
        postalInformation.PostalNames.Should().Contain(n => n.Name == "Gent" && n.Language == Language.Nl);
        postalInformation.IsRemoved.Should().BeFalse();
    }

    [Fact]
    public async Task MultiplePostalNames_ShouldBeMappedCorrectly()
    {
        var events = await CloudEventTestHelper.ReadEventsFromFileAsync(
            Path.Combine("TestData", "postalinformation-create-update.json"));

        // Take create + municipality + both naming events (ids 100-103)
        var relevantEvents = events
            .Where(e => long.Parse(e.Id!) <= 103)
            .ToList();
        _feedPageFetcher.SetupPage(1, relevantEvents.ToFeedPage(isPageComplete: false));

        using var cts = new CancellationTokenSource();
        cts.CancelAfter(5000);

        await RunOneCycleAsync(cts.Token);

        await using var context = _contextFactory.CreateDbContext();
        var postalInformation = await context.PostalInformations
            .Include(p => p.PostalNames)
            .FirstOrDefaultAsync(p => p.PersistentUri == PuriPostalInfo9000, TestContext.Current.CancellationToken);

        postalInformation.Should().NotBeNull();
        postalInformation!.PostalNames.Should().HaveCount(2);
        postalInformation.PostalNames.Should().Contain(n => n.Name == "Gent" && n.Language == Language.Nl);
        postalInformation.PostalNames.Should().Contain(n => n.Name == "Gand" && n.Language == Language.Fr);
    }

    [Fact]
    public async Task FullCreateUpdateSequence_ShouldResultInRetiredPostalInformation()
    {
        var events = await CloudEventTestHelper.ReadEventsFromFileAsync(
            Path.Combine("TestData", "postalinformation-create-update.json"));

        _feedPageFetcher.SetupPage(1, events.ToFeedPage(isPageComplete: true));

        using var cts = new CancellationTokenSource();
        cts.CancelAfter(5000);

        await RunOneCycleAsync(cts.Token);

        await using var context = _contextFactory.CreateDbContext();
        var postalInformation = await context.PostalInformations
            .Include(p => p.PostalNames)
            .FirstOrDefaultAsync(p => p.PersistentUri == PuriPostalInfo9000, TestContext.Current.CancellationToken);

        postalInformation.Should().NotBeNull();
        postalInformation!.PostalCode.Should().Be("9000");
        postalInformation.NisCode.Should().Be("44021");
        postalInformation.Status.Should().Be(PostalInformationStatus.Retired);
        postalInformation.PostalNames.Should().HaveCount(2);
        postalInformation.PostalNames.Should().Contain(n => n.Name == "Gent" && n.Language == Language.Nl);
        postalInformation.PostalNames.Should().Contain(n => n.Name == "Gand" && n.Language == Language.Fr);
        postalInformation.IsRemoved.Should().BeFalse();
    }

    [Fact]
    public async Task DeleteEvent_ShouldMarkPostalInformationAsRemoved()
    {
        var events = await CloudEventTestHelper.ReadEventsFromFileAsync(
            Path.Combine("TestData", "postalinformation-delete.json"));

        _feedPageFetcher.SetupPage(1, events.ToFeedPage(isPageComplete: false));

        using var cts = new CancellationTokenSource();
        cts.CancelAfter(5000);

        await RunOneCycleAsync(cts.Token);

        await using var context = _contextFactory.CreateDbContext();
        var postalInformation = await context.PostalInformations.FindAsync([PuriPostalInfo1000], TestContext.Current.CancellationToken);

        postalInformation.Should().NotBeNull();
        postalInformation!.IsRemoved.Should().BeTrue();
    }

    [Fact]
    public async Task FullDeleteSequence_ShouldResultInRemovedPostalInformationWithAllData()
    {
        var events = await CloudEventTestHelper.ReadEventsFromFileAsync(
            Path.Combine("TestData", "postalinformation-delete.json"));

        _feedPageFetcher.SetupPage(1, events.ToFeedPage(isPageComplete: true));

        using var cts = new CancellationTokenSource();
        cts.CancelAfter(5000);

        await RunOneCycleAsync(cts.Token);

        await using var context = _contextFactory.CreateDbContext();
        var postalInformation = await context.PostalInformations
            .Include(p => p.PostalNames)
            .FirstOrDefaultAsync(p => p.PersistentUri == PuriPostalInfo1000, TestContext.Current.CancellationToken);

        postalInformation.Should().NotBeNull();
        postalInformation!.PostalCode.Should().Be("1000");
        postalInformation.NisCode.Should().Be("21004");
        postalInformation.Status.Should().Be(PostalInformationStatus.Realized);
        postalInformation.PostalNames.Should().HaveCount(2);
        postalInformation.PostalNames.Should().Contain(n => n.Name == "Brussel" && n.Language == Language.Nl);
        postalInformation.PostalNames.Should().Contain(n => n.Name == "Bruxelles" && n.Language == Language.Fr);
        postalInformation.IsRemoved.Should().BeTrue();
    }

    [Fact]
    public async Task NisCode_ShouldBeExtractedFromMunicipalityPuri()
    {
        var events = await CloudEventTestHelper.ReadEventsFromFileAsync(
            Path.Combine("TestData", "postalinformation-create-update.json"));

        // Take create + municipality attachment (ids 100-101)
        var relevantEvents = events
            .Where(e => long.Parse(e.Id!) <= 101)
            .ToList();
        _feedPageFetcher.SetupPage(1, relevantEvents.ToFeedPage(isPageComplete: false));

        using var cts = new CancellationTokenSource();
        cts.CancelAfter(5000);

        await RunOneCycleAsync(cts.Token);

        await using var context = _contextFactory.CreateDbContext();
        var postalInformation = await context.PostalInformations.FindAsync([PuriPostalInfo9000], TestContext.Current.CancellationToken);

        postalInformation.Should().NotBeNull();
        // NIS code should be extracted from "https://data.vlaanderen.be/id/gemeente/44021"
        postalInformation!.NisCode.Should().Be("44021");
    }

    [Fact]
    public async Task FeedState_ShouldTrackPositionAfterProcessingEvents()
    {
        var events = await CloudEventTestHelper.ReadEventsFromFileAsync(
            Path.Combine("TestData", "postalinformation-create-update.json"));

        _feedPageFetcher.SetupPage(1, events.ToFeedPage(isPageComplete: false));

        using var cts = new CancellationTokenSource();
        cts.CancelAfter(5000);

        await RunOneCycleAsync(cts.Token);

        await using var context = _contextFactory.CreateDbContext();
        var feedState = await context.FeedStates.FindAsync([PostalInformationFeedName], TestContext.Current.CancellationToken);

        feedState.Should().NotBeNull();
        // Highest event id in the file is 104
        feedState!.EventPosition.Should().Be(104);
        // Page not complete, should remain 1
        feedState.Page.Should().Be(1);
    }

    [Fact]
    public async Task FeedState_ShouldIncrementPageWhenComplete()
    {
        var events = await CloudEventTestHelper.ReadEventsFromFileAsync(
            Path.Combine("TestData", "postalinformation-create-update.json"));

        _feedPageFetcher.SetupPage(1, events.ToFeedPage(isPageComplete: true));

        using var cts = new CancellationTokenSource();
        cts.CancelAfter(5000);

        await RunOneCycleAsync(cts.Token);

        await using var context = _contextFactory.CreateDbContext();
        var feedState = await context.FeedStates.FindAsync([PostalInformationFeedName], TestContext.Current.CancellationToken);

        feedState.Should().NotBeNull();
        feedState!.EventPosition.Should().Be(104);
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
