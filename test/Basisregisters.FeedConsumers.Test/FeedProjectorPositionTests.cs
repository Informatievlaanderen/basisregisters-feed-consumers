namespace Basisregisters.FeedConsumers.Test;

using System;
using System.Collections.Generic;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using CloudNative.CloudEvents;
using Console.Common;
using FluentAssertions;
using Infrastructure;
using Microsoft.Extensions.Logging.Abstractions;
using Model;
using Xunit;

public class FeedProjectorPositionTests
{
    private readonly InMemoryFeedContextFactory _contextFactory;
    private readonly FakeFeedPageFetcher _feedPageFetcher;
    private readonly FeedProjectorOptions _options;

    private const string FeedName = "TestFeed";

    public FeedProjectorPositionTests()
    {
        _contextFactory = new InMemoryFeedContextFactory();
        _feedPageFetcher = new FakeFeedPageFetcher();
        _options = new FeedProjectorOptions
        {
            Name = FeedName,
            FeedUrl = "https://test/feed",
            PollingIntervalInMinutes = 60,
            IgnoreNoEventHandlers = true
        };
    }

    [Fact]
    public async Task InitialState_ShouldCreateFeedStateWithPositionAndPage()
    {
        var events = CreateTestCloudEvents(1, 2, 3);
        _feedPageFetcher.SetupPage(1, new FeedProjectorBase.CloudEventsResult(events, false));

        var projector = CreateProjector();
        await RunOneCycleAsync(projector);

        await using var context = _contextFactory.CreateDbContext();
        var feedState = await context.FeedStates.FindAsync([FeedName], TestContext.Current.CancellationToken);

        feedState.Should().NotBeNull();
        feedState!.EventPosition.Should().Be(3);
        feedState.Page.Should().Be(1);
    }

    [Fact]
    public async Task PageComplete_ShouldIncrementPage()
    {
        var events = CreateTestCloudEvents(1, 2);
        _feedPageFetcher.SetupPage(1, new FeedProjectorBase.CloudEventsResult(events, true));

        var projector = CreateProjector();
        await RunOneCycleAsync(projector);

        await using var context = _contextFactory.CreateDbContext();
        var feedState = await context.FeedStates.FindAsync([FeedName], TestContext.Current.CancellationToken);

        feedState.Should().NotBeNull();
        feedState!.EventPosition.Should().Be(2);
        feedState.Page.Should().Be(2);
    }

    [Fact]
    public async Task PageNotComplete_ShouldNotIncrementPage()
    {
        var events = CreateTestCloudEvents(1, 2, 3);
        _feedPageFetcher.SetupPage(1, new FeedProjectorBase.CloudEventsResult(events, false));

        var projector = CreateProjector();
        await RunOneCycleAsync(projector);

        await using var context = _contextFactory.CreateDbContext();
        var feedState = await context.FeedStates.FindAsync([FeedName], TestContext.Current.CancellationToken);

        feedState.Should().NotBeNull();
        feedState!.EventPosition.Should().Be(3);
        feedState.Page.Should().Be(1);
    }

    [Fact]
    public async Task ResumeFromSavedState_ShouldOnlyProcessEventsAfterPosition()
    {
        // Seed an existing feed state at position 100, page 5
        await using (var context = _contextFactory.CreateDbContext())
        {
            context.FeedStates.Add(new FeedState(FeedName, 100, 5));
            await context.SaveChangesAsync(TestContext.Current.CancellationToken);
        }

        // Page 5 returns events 98-102 - only 101 and 102 should be processed
        var events = CreateTestCloudEvents(98, 99, 100, 101, 102);
        _feedPageFetcher.SetupPage(5, new FeedProjectorBase.CloudEventsResult(events, true));

        var projector = CreateProjector();
        await RunOneCycleAsync(projector);

        await using var verifyContext = _contextFactory.CreateDbContext();
        var feedState = await verifyContext.FeedStates.FindAsync([FeedName], TestContext.Current.CancellationToken);

        feedState.Should().NotBeNull();
        feedState!.EventPosition.Should().Be(102);
        feedState.Page.Should().Be(6);
    }

    [Fact]
    public async Task EmptyPage_ShouldNotChangePositionOrPage()
    {
        _feedPageFetcher.SetupPage(1, new FeedProjectorBase.CloudEventsResult([], false));

        var projector = CreateProjector();
        await RunOneCycleAsync(projector);

        await using var context = _contextFactory.CreateDbContext();
        var feedState = await context.FeedStates.FindAsync([FeedName], TestContext.Current.CancellationToken);

        feedState.Should().NotBeNull();
        feedState!.EventPosition.Should().Be(0);
        feedState.Page.Should().Be(1);
    }

    [Fact]
    public async Task EmptyPageComplete_ShouldIncrementPage()
    {
        // Edge case: empty page marked as complete (can happen if events were deleted)
        // Since there are no events, position stays 0, but page should still increment
        // Actually looking at the code: if (feedPage.Events.Any()) guard means position won't change
        // But IsPageComplete check still runs
        _feedPageFetcher.SetupPage(1, new FeedProjectorBase.CloudEventsResult([], true));

        var projector = CreateProjector();
        await RunOneCycleAsync(projector);

        await using var context = _contextFactory.CreateDbContext();
        var feedState = await context.FeedStates.FindAsync([FeedName], TestContext.Current.CancellationToken);

        feedState.Should().NotBeNull();
        feedState!.EventPosition.Should().Be(0);
        feedState.Page.Should().Be(2);
    }

    [Fact]
    public async Task PositionTracking_ShouldUseHighestEventIdOnPage()
    {
        // Events can be out of order on a page; position should track the highest
        var events = CreateTestCloudEvents(5, 3, 7, 1, 4);
        _feedPageFetcher.SetupPage(1, new FeedProjectorBase.CloudEventsResult(events, false));

        var projector = CreateProjector();
        await RunOneCycleAsync(projector);

        await using var context = _contextFactory.CreateDbContext();
        var feedState = await context.FeedStates.FindAsync([FeedName], TestContext.Current.CancellationToken);

        feedState.Should().NotBeNull();
        feedState!.EventPosition.Should().Be(7);
    }

    [Fact]
    public async Task NoHandlerForEvent_WithIgnoreFlag_ShouldContinueProcessing()
    {
        // Create events with an unknown type - the projector has IgnoreNoEventHandlers = true
        var events = new List<CloudEvent>
        {
            CreateCloudEvent("1", "unknown.event.type.v1"),
            CreateCloudEvent("2", "test.event.v1"),
        };
        _feedPageFetcher.SetupPage(1, new FeedProjectorBase.CloudEventsResult(events, false));

        var projector = CreateProjector();
        await RunOneCycleAsync(projector);

        await using var context = _contextFactory.CreateDbContext();
        var feedState = await context.FeedStates.FindAsync([FeedName], TestContext.Current.CancellationToken);

        feedState.Should().NotBeNull();
        feedState!.EventPosition.Should().Be(2);
    }

    [Fact]
    public async Task NoHandlerForEvent_WithoutIgnoreFlag_ShouldThrow()
    {
        var strictOptions = new FeedProjectorOptions
        {
            Name = "StrictFeed",
            FeedUrl = "https://test/feed",
            PollingIntervalInMinutes = 60,
            IgnoreNoEventHandlers = false
        };

        var events = new List<CloudEvent>
        {
            CreateCloudEvent("1", "unknown.event.type.v1"),
        };
        _feedPageFetcher.SetupPage(1, new FeedProjectorBase.CloudEventsResult(events, false));

        var projector = new TestFeedProjector(
            strictOptions,
            _contextFactory,
            _feedPageFetcher,
            new NoOpJsonSchemaValidator(),
            NullLogger.Instance);

        // StartAsync fires ExecuteAsync which fails fast; exception surfaces via StartAsync or StopAsync
        var act = async () =>
        {
            await projector.StartAsync(CancellationToken.None);
            await Task.Delay(500);
            await projector.StopAsync(CancellationToken.None);
        };

        await act.Should().ThrowAsync<InvalidOperationException>()
            .WithMessage("*No handlers found for event type*");
    }

    private TestFeedProjector CreateProjector()
    {
        return new TestFeedProjector(
            _options,
            _contextFactory,
            _feedPageFetcher,
            new NoOpJsonSchemaValidator(),
            NullLogger.Instance);
    }

    private async Task RunOneCycleAsync(TestFeedProjector projector)
    {
        await projector.StartAsync(CancellationToken.None);
        await Task.Delay(500);
        await projector.StopAsync(CancellationToken.None);
    }

    private static List<CloudEvent> CreateTestCloudEvents(params long[] ids)
    {
        var events = new List<CloudEvent>();
        foreach (var id in ids)
        {
            events.Add(CreateCloudEvent(id.ToString(), "test.event.v1"));
        }
        return events;
    }

    private static CloudEvent CreateCloudEvent(string id, string type)
    {
        var data = new
        {
            @id = "https://data.vlaanderen.be/id/test/1",
            objectId = "1",
            naamruimte = "https://data.vlaanderen.be/id/test",
            versieId = "2002-08-13T17:32:32+02:00",
            nisCodes = new[] { "10000" },
            attributen = Array.Empty<object>()
        };

        var jsonData = JsonSerializer.SerializeToElement(data);

        return new CloudEvent
        {
            Id = id,
            Type = type,
            Source = new Uri("https://test/feed"),
            DataContentType = "application/json",
            Data = jsonData,
            DataSchema = new Uri("https://test/schema.json"),
            Time = DateTimeOffset.UtcNow
        };
    }
}
