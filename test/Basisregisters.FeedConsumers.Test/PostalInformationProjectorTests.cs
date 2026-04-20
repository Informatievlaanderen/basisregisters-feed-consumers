namespace Basisregisters.FeedConsumers.Test;

using System;
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

    private const string PuriPostalInfo9050 = "https://data.vlaanderen.be/id/postinfo/9050";
    private const string PuriPostalInfo5020 = "https://data.vlaanderen.be/id/postinfo/5020";
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

        var createEvents = events.Where(e => e.Id == "3596").ToList();
        _feedPageFetcher.SetupPage(1, createEvents.ToFeedPage(isPageComplete: false));

        using var cts = new CancellationTokenSource();
        cts.CancelAfter(5000);

        await RunOneCycleAsync(cts.Token);

        await using var context = _contextFactory.CreateDbContext();

        var postalInformation = await context.PostalInformations.FindAsync([PuriPostalInfo9050], TestContext.Current.CancellationToken);

        postalInformation.Should().NotBeNull();
        postalInformation!.PostalCode.Should().Be("9050");
        postalInformation.Status.Should().Be(PostalInformationStatus.Realized);
        postalInformation.NisCode.Should().BeNull();
        postalInformation.IsRemoved.Should().BeFalse();
    }

    [Fact]
    public async Task CreateAndUpdateEvents_ShouldApplyStatusAndNames()
    {
        var events = await CloudEventTestHelper.ReadEventsFromFileAsync(
            Path.Combine("TestData", "postalinformation-create-update.json"));

        // Take create + status + first naming + second naming events (ids 3596-3601)
        var relevantEvents = events
            .Where(e => long.Parse(e.Id!) <= 3601)
            .ToList();
        _feedPageFetcher.SetupPage(1, relevantEvents.ToFeedPage(isPageComplete: false));

        using var cts = new CancellationTokenSource();
        cts.CancelAfter(5000);

        await RunOneCycleAsync(cts.Token);

        await using var context = _contextFactory.CreateDbContext();
        var postalInformation = await context.PostalInformations
            .Include(p => p.PostalNames)
            .FirstOrDefaultAsync(p => p.PersistentUri == PuriPostalInfo9050, TestContext.Current.CancellationToken);

        postalInformation.Should().NotBeNull();
        postalInformation!.PostalCode.Should().Be("9050");
        postalInformation.NisCode.Should().BeNull();
        postalInformation.Status.Should().Be(PostalInformationStatus.Realized);
        postalInformation.PostalNames.Should().HaveCount(2);
        postalInformation.PostalNames.Should().Contain(n => n.Name == "Gentbrugge" && n.Language == Language.Nl);
        postalInformation.PostalNames.Should().Contain(n => n.Name == "Ledeberg" && n.Language == Language.Nl);
        postalInformation.IsRemoved.Should().BeFalse();
    }

    [Fact]
    public async Task MultiplePostalNames_ShouldBeMappedCorrectly()
    {
        var events = await CloudEventTestHelper.ReadEventsFromFileAsync(
            Path.Combine("TestData", "postalinformation-create-update.json"));

        // Take all events
        _feedPageFetcher.SetupPage(1, events.ToFeedPage(isPageComplete: false));

        using var cts = new CancellationTokenSource();
        cts.CancelAfter(5000);

        await RunOneCycleAsync(cts.Token);

        await using var context = _contextFactory.CreateDbContext();
        var postalInformation = await context.PostalInformations
            .Include(p => p.PostalNames)
            .FirstOrDefaultAsync(p => p.PersistentUri == PuriPostalInfo9050, TestContext.Current.CancellationToken);

        postalInformation.Should().NotBeNull();
        postalInformation!.PostalNames.Should().HaveCount(2);
        postalInformation.PostalNames.Should().Contain(n => n.Name == "Gentbrugge" && n.Language == Language.Nl);
        postalInformation.PostalNames.Should().Contain(n => n.Name == "Ledeberg" && n.Language == Language.Nl);
    }

    [Fact]
    public async Task FullCreateUpdateSequence_ShouldResultInPostalInformationWithMunicipality()
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
            .FirstOrDefaultAsync(p => p.PersistentUri == PuriPostalInfo9050, TestContext.Current.CancellationToken);

        postalInformation.Should().NotBeNull();
        postalInformation!.PostalCode.Should().Be("9050");
        postalInformation.NisCode.Should().Be("44021");
        postalInformation.Status.Should().Be(PostalInformationStatus.Realized);
        postalInformation.PostalNames.Should().HaveCount(2);
        postalInformation.PostalNames.Should().Contain(n => n.Name == "Gentbrugge" && n.Language == Language.Nl);
        postalInformation.PostalNames.Should().Contain(n => n.Name == "Ledeberg" && n.Language == Language.Nl);
        postalInformation.IsRemoved.Should().BeFalse();
        //2020-02-10T12:44:14+01:00
        postalInformation.VersionId.Should().BeCloseTo(new DateTimeOffset(2020, 2, 10, 12, 44, 14, TimeSpan.FromHours(1)), TimeSpan.FromSeconds(1));
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
        var postalInformation = await context.PostalInformations.FindAsync([PuriPostalInfo5020], TestContext.Current.CancellationToken);

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
            .FirstOrDefaultAsync(p => p.PersistentUri == PuriPostalInfo5020, TestContext.Current.CancellationToken);

        postalInformation.Should().NotBeNull();
        postalInformation!.PostalCode.Should().Be("5020");
        postalInformation.NisCode.Should().Be("92094");
        postalInformation.Status.Should().Be(PostalInformationStatus.Realized);
        postalInformation.PostalNames.Should().HaveCount(7);
        postalInformation.PostalNames.Should().Contain(n => n.Name == "Champion" && n.Language == Language.Fr);
        postalInformation.PostalNames.Should().Contain(n => n.Name == "Daussoulx" && n.Language == Language.Fr);
        postalInformation.PostalNames.Should().Contain(n => n.Name == "Flawinne" && n.Language == Language.Fr);
        postalInformation.PostalNames.Should().Contain(n => n.Name == "Malonne" && n.Language == Language.Fr);
        postalInformation.PostalNames.Should().Contain(n => n.Name == "Suarlée" && n.Language == Language.Fr);
        postalInformation.PostalNames.Should().Contain(n => n.Name == "Temploux" && n.Language == Language.Fr);
        postalInformation.PostalNames.Should().Contain(n => n.Name == "Vedrin" && n.Language == Language.Fr);
        postalInformation.IsRemoved.Should().BeTrue();
    }

    [Fact]
    public async Task NisCode_ShouldBeExtractedFromMunicipalityPuri()
    {
        var events = await CloudEventTestHelper.ReadEventsFromFileAsync(
            Path.Combine("TestData", "postalinformation-create-update.json"));

        // Take all events including municipality attachment
        _feedPageFetcher.SetupPage(1, events.ToFeedPage(isPageComplete: false));

        using var cts = new CancellationTokenSource();
        cts.CancelAfter(5000);

        await RunOneCycleAsync(cts.Token);

        await using var context = _contextFactory.CreateDbContext();
        var postalInformation = await context.PostalInformations.FindAsync([PuriPostalInfo9050], TestContext.Current.CancellationToken);

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
        // Highest event id in the file is 6243
        feedState!.EventPosition.Should().Be(6243);
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
        feedState!.EventPosition.Should().Be(6243);
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
