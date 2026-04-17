namespace Basisregisters.FeedConsumers.Console.Common;

using System;
using System.Collections.Concurrent;
using System.Linq;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using CloudNative.CloudEvents;
using Microsoft.Extensions.Logging;
using JsonSchema = NJsonSchema.JsonSchema;

public class JsonSchemaValidator : IJsonSchemaValidator
{
    private readonly ConcurrentDictionary<string, JsonSchema> _schemas = new();
    private readonly ILogger _logger;

    public JsonSchemaValidator(ILogger logger)
    {
        _logger = logger;
    }

    public Task ValidateAsync(CloudEvent cloudEvent, CancellationToken cancellationToken)
    {
        try
        {
            if (cloudEvent.DataSchema is null || cloudEvent.Data is null)
                throw new InvalidOperationException(
                    $"CloudEvent {cloudEvent.Id} is missing DataSchema or Data, cannot validate JSON schema.");

            if (cloudEvent.Data is not JsonElement jsonElement)
                throw new InvalidOperationException(
                    $"CloudEvent {cloudEvent.Id} data is not a JsonElement. Actual type: {cloudEvent.Data?.GetType().Name ?? "null"}.");

            var validationErrors = _schemas.GetOrAdd(cloudEvent.DataSchema.ToString(), uri =>
            {
                try
                {
                    var schema = JsonSchema.FromUrlAsync(uri, cancellationToken).GetAwaiter().GetResult();
                    return schema;
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex,
                        "Failed to load JSON schema from {SchemaUri} for event type {EventType}", uri,
                        cloudEvent.Type);
                    throw new InvalidOperationException(
                        $"Failed to load JSON schema from {uri} for event type {cloudEvent.Type}", ex);
                }
            }).Validate(jsonElement.GetRawText());

            if (validationErrors.Any())
                throw new InvalidOperationException(
                    $"Failed to validate JSON schema for event type {cloudEvent.Type}");
            return Task.CompletedTask;
        }
        catch (Exception exception)
        {
            return Task.FromException(exception);
        }
    }
}
