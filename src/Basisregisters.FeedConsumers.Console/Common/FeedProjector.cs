namespace Basisregisters.FeedConsumers.Console.Common;

using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using System.Net.Http;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using CloudNative.CloudEvents;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Model;
using JsonSchema = NJsonSchema.JsonSchema;

public class FeedProjectorOptions
{
    public required string Name { get; set; }
    public HttpClient? FeedClient { get; set; }
    public required string FeedUrl { get; set; }
    public int PollingIntervalInMinutes { get; set; }
    public bool IgnoreNoEventHandlers { get; set; }
}

public abstract class FeedProjectorBase : BackgroundService
{
    private readonly FeedProjectorOptions _options;
    private readonly IDbContextFactory<FeedContext> _feedContextFactory;
    private readonly List<Handler> _handlers = [];
    private readonly ConcurrentDictionary<string, JsonSchema> _schemas = new ConcurrentDictionary<string, JsonSchema>();

    protected ILogger Logger { get; }

    public FeedProjectorBase(
        FeedProjectorOptions options,
        IDbContextFactory<FeedContext> feedContextFactory,
        ILogger logger)
    {
        if(options.FeedClient is null)
            throw new ArgumentNullException(nameof(options.FeedClient), "FeedClient is required in FeedProjectorOptions.");

        _options = options;
        _feedContextFactory = feedContextFactory;
        Logger = logger;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        var page = 1;
        var position = 0L;

        while (!stoppingToken.IsCancellationRequested)
        {
            var processedEvents = false;

            using (var context = await _feedContextFactory.CreateDbContextAsync(stoppingToken))
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

                var feedPage = await FetchFeedPageAsync(page, stoppingToken);

                foreach (var cloudEvent in feedPage.Events.Where(x => Convert.ToInt64(x.Id!) > position))
                {
                    if (!_options.IgnoreNoEventHandlers && _handlers.All(x => x.Type.Value != cloudEvent.Type))
                        throw new InvalidOperationException(
                            $"No handlers found for event type {cloudEvent.Type} in {_options.Name} projector.");

                    if (_handlers.Count(x => x.Type.ToString() == cloudEvent.Type) > 1)
                        Logger.LogWarning(
                            $"Multiple handlers found for event type {cloudEvent.Type}. All handlers will be executed.");

                    processedEvents = true;
                    foreach (var handler in _handlers)
                    {
                        try
                        {
                            if (handler.Type.Value != cloudEvent.Type)
                                continue;

                            await ValidateJsonSchema(cloudEvent, stoppingToken);

                            //deserialize the cloudevent data
                            var eventData = cloudEvent.Data is JsonElement jsonElement
                                ? jsonElement.Deserialize<CloudEventData>(CloudEventReader.JsonOptions)
                                  ?? throw new InvalidOperationException($"Failed to deserialize CloudEvent data for event {cloudEvent.Id}.")
                                : throw new InvalidOperationException($"CloudEvent {cloudEvent.Id} data is not a JsonElement. Actual type: {cloudEvent.Data?.GetType().Name ?? "null"}.");

                            await handler.Handle(cloudEvent, eventData, context, stoppingToken);
                        }
                        catch (Exception ex)
                        {
                            Logger.LogError(ex, $"Error processing event {cloudEvent.Id}");
                            throw;
                        }
                    }
                }

                position = feedPage.Events.LastOrDefault() is null
                    ? position
                    : Convert.ToInt64(feedPage.Events.Last().Id!);

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

    private async Task ValidateJsonSchema(CloudEvent cloudEvent, CancellationToken cancellationToken)
    {
        if(cloudEvent.DataSchema is null || cloudEvent.Data is null)
            throw new InvalidOperationException($"CloudEvent {cloudEvent.Id} is missing DataSchema or Data, cannot validate JSON schema.");

        _schemas.GetOrAdd(cloudEvent.DataSchema.ToString(), uri =>
        {
            try
            {
                var schema = JsonSchema.FromUrlAsync(uri, cancellationToken).GetAwaiter().GetResult();
                return schema;
            }
            catch (Exception ex)
            {
                Logger.LogError(ex, $"Failed to load JSON schema from {uri} for event type {cloudEvent.Type}");
                throw new InvalidOperationException($"Failed to load JSON schema from {uri} for event type {cloudEvent.Type}", ex);
            }
        }).Validate(cloudEvent.Data.ToString()!);
    }

    private async Task<CloudEventsResult> FetchFeedPageAsync(int page, CancellationToken stoppingToken)
    {
        // create http client with url
        var result = await _options.FeedClient!.GetAsync(_options.FeedUrl + $"?pagina={page}", stoppingToken);
        result.EnsureSuccessStatusCode();

        var events = await CloudEventReader.ReadBatchAsync(await result.Content.ReadAsStreamAsync(stoppingToken), stoppingToken);
        var xPageComplete = "X-Page-Complete";
        return new CloudEventsResult(events, result.Headers.TryGetValues(xPageComplete, out var values) && values.FirstOrDefault()?.Equals("true", StringComparison.InvariantCultureIgnoreCase) == true);
    }

    protected void When(BaseRegistriesCloudEventType eventType, Func<CloudEvent, CloudEventData, FeedContext, CancellationToken, Task> handler)
        => _handlers.Add(new Handler(eventType, handler));

    public record CloudEventsResult(IReadOnlyList<CloudEvent> Events, bool IsPageComplete);
    public record Handler(BaseRegistriesCloudEventType Type, Func<CloudEvent, CloudEventData, FeedContext, CancellationToken, Task> Handle);

    public record BaseRegistriesCloudEventType(string Value);
}
