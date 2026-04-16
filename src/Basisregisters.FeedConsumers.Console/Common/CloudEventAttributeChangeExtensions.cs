namespace Basisregisters.FeedConsumers.Console.Common;

using System.Collections.Generic;
using System.Linq;

public static class CloudEventAttributeChangeExtensions
{
    public static CloudEventAttributeChange? Get(this ICollection<CloudEventAttributeChange> attributes, string name)
    {
        return attributes.FirstOrDefault(attribute => attribute.Naam == name);
    }

    public static CloudEventAttributeChange GetRequired(this ICollection<CloudEventAttributeChange> attributes, string name)
    {
        return attributes.First(attribute => attribute.Naam == name);
    }
}
