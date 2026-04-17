namespace Basisregisters.FeedConsumers.Test.Infrastructure;

using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using CloudNative.CloudEvents;
using Console.Common;
using static Console.Common.FeedProjectorBase;

public static class CloudEventTestHelper
{
    public static async Task<IReadOnlyList<CloudEvent>> ReadEventsFromResourceAsync(string resourceName)
    {
        var assembly = Assembly.GetExecutingAssembly();
        await using var stream = assembly.GetManifestResourceStream(resourceName)
            ?? throw new FileNotFoundException($"Embedded resource '{resourceName}' not found.");

        return await CloudEventReader.ReadBatchAsync(stream, CancellationToken.None);
    }

    public static async Task<IReadOnlyList<CloudEvent>> ReadEventsFromFileAsync(string filePath)
    {
        await using var stream = File.OpenRead(filePath);
        return await CloudEventReader.ReadBatchAsync(stream, CancellationToken.None);
    }

    public static async Task<IReadOnlyList<CloudEvent>> ReadEventsFromJsonAsync(string json)
    {
        var stream = new MemoryStream(Encoding.UTF8.GetBytes(json));
        return await CloudEventReader.ReadBatchAsync(stream, CancellationToken.None);
    }

    public static CloudEventsResult ToFeedPage(this IReadOnlyList<CloudEvent> events, bool isPageComplete = false)
    {
        return new CloudEventsResult(events, isPageComplete);
    }

    public static CloudEventsResult ToFeedPage(this IEnumerable<CloudEvent> events, bool isPageComplete = false)
    {
        return new CloudEventsResult(events.ToList(), isPageComplete);
    }
}
