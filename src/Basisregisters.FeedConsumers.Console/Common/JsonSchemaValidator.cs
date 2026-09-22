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
using Microsoft.Extensions.Logging;
using JsonSchema = Corvus.Text.Json.Validator.JsonSchema;
using JsonSchemaResultsCollector = Corvus.Text.Json.JsonSchemaResultsCollector;
using JsonSchemaResultsLevel = Corvus.Text.Json.JsonSchemaResultsLevel;

public class JsonSchemaValidator : IJsonSchemaValidator
{
    private readonly ConcurrentDictionary<string, Lazy<Task<JsonSchema>>> _schemas = new();
    private readonly HttpClient _httpClient;
    private readonly ILogger _logger;

    public JsonSchemaValidator(HttpClient httpClient, ILogger logger)
    {
        _httpClient = httpClient ?? throw new ArgumentNullException(nameof(httpClient));
        _logger = logger;
    }

    public async Task ValidateAsync(CloudEvent cloudEvent, CancellationToken cancellationToken)
    {
        if (cloudEvent.DataSchema is null || cloudEvent.Data is null)
            throw new InvalidOperationException(
                $"CloudEvent {cloudEvent.Id} is missing DataSchema or Data, cannot validate JSON schema.");

        if (cloudEvent.Data is not JsonElement jsonElement)
            throw new InvalidOperationException(
                $"CloudEvent {cloudEvent.Id} data is not a JsonElement. Actual type: {cloudEvent.Data?.GetType().Name ?? "null"}.");

        var schema = await GetSchemaAsync(cloudEvent, cancellationToken);
        var json = jsonElement.GetRawText();

        // Fast path without collecting results, only collect details when validation fails.
        if (schema.Validate(json))
            return;

        var errors = CollectErrors(schema, json);
        _logger.LogError(
            "CloudEvent {EventId} of type {EventType} does not match schema {SchemaUri}: {ValidationErrors}",
            cloudEvent.Id, cloudEvent.Type, cloudEvent.DataSchema, errors);

        throw new InvalidOperationException(
            $"Failed to validate JSON schema for event {cloudEvent.Id} of type {cloudEvent.Type}: {string.Join("; ", errors)}");
    }

    private async Task<JsonSchema> GetSchemaAsync(CloudEvent cloudEvent, CancellationToken cancellationToken)
    {
        var uri = cloudEvent.DataSchema!.ToString();
        var lazySchema = _schemas.GetOrAdd(uri, key => new Lazy<Task<JsonSchema>>(() => LoadSchemaAsync(key)));

        try
        {
            return await lazySchema.Value.WaitAsync(cancellationToken);
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            // Don't cache failures, retry loading on the next event.
            _schemas.TryRemove(new KeyValuePair<string, Lazy<Task<JsonSchema>>>(uri, lazySchema));

            _logger.LogError(ex,
                "Failed to load JSON schema from {SchemaUri} for event type {EventType}", uri, cloudEvent.Type);
            throw new InvalidOperationException(
                $"Failed to load JSON schema from {uri} for event type {cloudEvent.Type}", ex);
        }
    }

    private async Task<JsonSchema> LoadSchemaAsync(string uri)
    {
        var schemaText = await _httpClient.GetStringAsync(uri);

        // refreshCache: Corvus caches compiled schemas globally by canonical uri, make sure we use what we just downloaded.
        return JsonSchema.FromText(schemaText, canonicalUri: uri, refreshCache: true);
    }

    private static List<string> CollectErrors(JsonSchema schema, string json)
    {
        using var collector = JsonSchemaResultsCollector.Create(JsonSchemaResultsLevel.Detailed);
        schema.Validate(json, collector);

        var failures = new List<(string DocumentLocation, string SchemaLocation, string Message)>();
        foreach (var result in collector.EnumerateResults())
        {
            if (result.IsMatch)
                continue;

            var schemaLocation = result.GetSchemaEvaluationLocationText();
            var message = result.GetMessageText();

            // Failing "if" branches only mean the conditional does not apply, they are not errors.
            if (schemaLocation.Contains("/if") || string.IsNullOrWhiteSpace(message))
                continue;

            failures.Add((result.GetDocumentEvaluationLocationText(), schemaLocation, message));
        }

        // Only report the most specific failures, parent subschemas just repeat that a child failed.
        return failures
            .Where(failure => !failures.Any(other => other.SchemaLocation.StartsWith(failure.SchemaLocation + "/", StringComparison.Ordinal)))
            .Select(failure => $"{failure.DocumentLocation} ({failure.SchemaLocation}): {failure.Message}")
            .Distinct()
            .ToList();
    }
}
