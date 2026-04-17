namespace Basisregisters.FeedConsumers.Console.Common;

using System.Collections.Generic;
using System.IO;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using CloudNative.CloudEvents;
using CloudNative.CloudEvents.SystemTextJson;

public static class CloudEventReader
{
    private static readonly CloudEventAttribute EventTypeAttribute =
        CloudEventAttribute.CreateExtension("basisregisterseventtype", CloudEventAttributeType.String);
    private static readonly CloudEventAttribute CausationIdAttribute =
        CloudEventAttribute.CreateExtension("basisregisterscausationid", CloudEventAttributeType.String);

    public static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web)
    {
        PropertyNameCaseInsensitive = true,
        AllowOutOfOrderMetadataProperties = true,
    };

    public static async Task<IReadOnlyList<CloudEvent>> ReadBatchAsync(Stream jsonStream, CancellationToken ct = default)
    {
        // Your sample is a JSON array of CloudEvents
        var formatter = new JsonEventFormatter(JsonOptions, new JsonDocumentOptions());

        var cloudEvents = await formatter.DecodeBatchModeMessageAsync(jsonStream, null, [EventTypeAttribute, CausationIdAttribute]);

        return cloudEvents;
    }
}
