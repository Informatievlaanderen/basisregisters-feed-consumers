namespace Basisregisters.FeedConsumers.Console.Common;

using System;
using CloudNative.CloudEvents;

public static class CloudEventExtensions
{
    /// <summary>
    /// The canonical identifier URI of the object the event is about.
    /// </summary>
    public static string GetRequiredSubject(this CloudEvent cloudEvent)
    {
        return string.IsNullOrWhiteSpace(cloudEvent.Subject)
            ? throw new InvalidOperationException($"CloudEvent {cloudEvent.Id} is missing a subject.")
            : cloudEvent.Subject;
    }
}
