namespace Basisregisters.FeedConsumers.Console.Common;

using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using CloudNative.CloudEvents;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Model;

public class FeedProjectorOptions
{
    public required string Name { get; set; }
    public required string FeedUrl { get; set; }
    public int PollingIntervalInMinutes { get; set; }
    public bool IgnoreNoEventHandlers { get; set; }
}

public abstract class FeedProjectorBase : BackgroundService
{
    public const string PageCompleteHeader = "X-Page-Complete";

    private readonly FeedProjectorOptions _options;
    private readonly IDbContextFactory<FeedContext> _feedContextFactory;
    private readonly IFeedPageFetcher _feedPageFetcher;
    private readonly IJsonSchemaValidator _jsonSchemaValidator;
    private readonly List<Handler> _handlers = [];

    protected ILogger Logger { get; }

    public FeedProjectorBase(
        FeedProjectorOptions options,
        IDbContextFactory<FeedContext> feedContextFactory,
        IFeedPageFetcher feedPageFetcher,
        IJsonSchemaValidator jsonSchemaValidator,
        ILogger logger)
    {
        _options = options ?? throw new ArgumentNullException(nameof(options));
        _feedContextFactory = feedContextFactory ?? throw new ArgumentNullException(nameof(feedContextFactory));
        _feedPageFetcher = feedPageFetcher ?? throw new ArgumentNullException(nameof(feedPageFetcher));
        _jsonSchemaValidator = jsonSchemaValidator ?? throw new ArgumentNullException(nameof(jsonSchemaValidator));
        Logger = logger ?? throw new ArgumentNullException(nameof(logger));
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        var page = 1;
        var position = 0L;

        while (!stoppingToken.IsCancellationRequested)
        {
            var processedEvents = false;

            await using (var context = await _feedContextFactory.CreateDbContextAsync(stoppingToken))
            {
                var feedState = await context.FeedStates.FindAsync([_options.Name], cancellationToken: stoppingToken);
                if (feedState is null)
                {
                    feedState = new FeedState(_options.Name, position, page);
                    context.FeedStates.Add(feedState);
                }
                else
                {
                    page = feedState.Page;
                    position = feedState.EventPosition;
                }

                var feedPage = await _feedPageFetcher.FetchAsync(page, stoppingToken);

                foreach (var cloudEvent in feedPage.Events.Where(x => Convert.ToInt64(x.Id!) > position))
                {
                    if (!_options.IgnoreNoEventHandlers && _handlers.All(x => x.Type.Value != cloudEvent.Type))
                        throw new InvalidOperationException(
                            $"No handlers found for event type {cloudEvent.Type} in {_options.Name} projector.");

                    if (_handlers.Count(x => x.Type.Value.ToString() == cloudEvent.Type) > 1)
                        Logger.LogWarning(
                            "Multiple handlers found for event type {EventType}. All handlers will be executed.", cloudEvent.Type);

                    processedEvents = true;
                    foreach (var handler in _handlers)
                    {
                        try
                        {
                            if (handler.Type.Value != cloudEvent.Type)
                                continue;

                            if (cloudEvent.Data is not JsonElement jsonElement)
                                throw new InvalidOperationException($"CloudEvent {cloudEvent.Id} data is not a JsonElement. Actual type: {cloudEvent.Data?.GetType().Name ?? "null"}.");

                            await _jsonSchemaValidator.ValidateAsync(cloudEvent, stoppingToken);

                            //deserialize the cloudevent data
                            var eventData = jsonElement.Deserialize<CloudEventData>(CloudEventReader.JsonOptions)
                                            ?? throw new InvalidOperationException($"Failed to deserialize CloudEvent data for event {cloudEvent.Id}.");
                            await handler.Handle(cloudEvent, eventData, context, stoppingToken);
                        }
                        catch (Exception ex)
                        {
                            Logger.LogError(ex, "Error processing event {EventId}", cloudEvent.Id);
                            throw;
                        }
                    }
                }

                if (feedPage.Events.Any())
                {
                    var highestEventIdOnPage = feedPage.Events.Max(x => Convert.ToInt64(x.Id!));
                    position = Math.Max(position, highestEventIdOnPage);
                }

                if (feedPage.IsPageComplete)
                    page++;

                feedState.EventPosition = position;
                feedState.Page = page;

                await context.SaveChangesAsync(stoppingToken);
            }

            //wait before next poll only when there are no more events to be polled
            if(!processedEvents)
                await Task.Delay(TimeSpan.FromMinutes(_options.PollingIntervalInMinutes), stoppingToken);
        }
    }

    protected void When(BaseRegistriesCloudEventType eventType, Func<CloudEvent, CloudEventData, FeedContext, CancellationToken, Task> handler)
        => _handlers.Add(new Handler(eventType, handler));

    public record CloudEventsResult(IReadOnlyList<CloudEvent> Events, bool IsPageComplete);
    public record Handler(BaseRegistriesCloudEventType Type, Func<CloudEvent, CloudEventData, FeedContext, CancellationToken, Task> Handle);

    public record BaseRegistriesCloudEventType(string Value);
}
