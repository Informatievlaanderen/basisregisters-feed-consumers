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
    public const string PageCompleteHeader = "X-Page-Complete";

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

                var feedPage = await FetchFeedPageAsync(page, stoppingToken);

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

                            await ValidateJsonSchema(cloudEvent, stoppingToken);

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

    private Task ValidateJsonSchema(CloudEvent cloudEvent, CancellationToken cancellationToken)
    {
        try
        {
            if(cloudEvent.DataSchema is null || cloudEvent.Data is null)
                throw new InvalidOperationException($"CloudEvent {cloudEvent.Id} is missing DataSchema or Data, cannot validate JSON schema.");

            if (cloudEvent.Data is not JsonElement jsonElement)
                throw new InvalidOperationException($"CloudEvent {cloudEvent.Id} data is not a JsonElement. Actual type: {cloudEvent.Data?.GetType().Name ?? "null"}.");

            var validationErrors = _schemas.GetOrAdd(cloudEvent.DataSchema.ToString(), uri =>
            {
                try
                {
                    var schema = JsonSchema.FromUrlAsync(uri, cancellationToken).GetAwaiter().GetResult();
                    return schema;
                }
                catch (Exception ex)
                {
                    Logger.LogError(ex, "Failed to load JSON schema from {SchemaUri} for event type {EventType}", uri, cloudEvent.Type);
                    throw new InvalidOperationException($"Failed to load JSON schema from {uri} for event type {cloudEvent.Type}", ex);
                }
            }).Validate(jsonElement.GetRawText());

            if (validationErrors.Any())
                throw new InvalidOperationException($"Failed to validate JSON schema for event type {cloudEvent.Type}");
            return Task.CompletedTask;
        }
        catch (Exception exception)
        {
            return Task.FromException(exception);
        }
    }

    private async Task<CloudEventsResult> FetchFeedPageAsync(int page, CancellationToken stoppingToken)
    {
        using var response = await _options.FeedClient!.GetAsync(_options.FeedUrl + $"?pagina={page}", stoppingToken);
        response.EnsureSuccessStatusCode();

        await using var contentStream = await response.Content.ReadAsStreamAsync(stoppingToken);
        var events = await CloudEventReader.ReadBatchAsync(contentStream, stoppingToken);

        return new CloudEventsResult(events, response.Headers.TryGetValues(PageCompleteHeader, out var values)
                                             && values.FirstOrDefault()?.Equals("true", StringComparison.InvariantCultureIgnoreCase) == true);
    }

    protected void When(BaseRegistriesCloudEventType eventType, Func<CloudEvent, CloudEventData, FeedContext, CancellationToken, Task> handler)
        => _handlers.Add(new Handler(eventType, handler));

    public record CloudEventsResult(IReadOnlyList<CloudEvent> Events, bool IsPageComplete);
    public record Handler(BaseRegistriesCloudEventType Type, Func<CloudEvent, CloudEventData, FeedContext, CancellationToken, Task> Handle);

    public record BaseRegistriesCloudEventType(string Value);
}
