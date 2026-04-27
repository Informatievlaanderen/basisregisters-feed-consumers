namespace Basisregisters.FeedConsumers.Console.Common;

using System.Text.Json;

public static class ChangeAttributeValueExtensions
{
    public static int ExtractPersistentLocalIdAsInt(this string persistentUri)
    {
        var lastSlashIndex = persistentUri.LastIndexOf('/');
        var persistentLocalId = lastSlashIndex >= 0
            ? persistentUri[(lastSlashIndex + 1)..]
            : persistentUri;

        return int.Parse(persistentLocalId);
    }

    public static string ExtractPersistentLocalId(this string persistentUri)
    {
        var lastSlashIndex = persistentUri.LastIndexOf('/');
        var persistentLocalId = lastSlashIndex >= 0
            ? persistentUri[(lastSlashIndex + 1)..]
            : persistentUri;

        return persistentLocalId;
    }

    public static bool ToBoolean(this object value)
    {
        return value switch
        {
            bool boolean => boolean,
            JsonElement jsonElement when jsonElement.ValueKind == JsonValueKind.True || jsonElement.ValueKind == JsonValueKind.False => jsonElement.GetBoolean(),
            _ => bool.Parse(value.ToString()!)
        };
    }
}
