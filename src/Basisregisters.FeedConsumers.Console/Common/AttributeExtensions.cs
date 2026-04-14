namespace Basisregisters.FeedConsumers.Console.Common;

using System.Collections.Generic;
using System.Linq;

public static class AttributeExtensions
{
    public static Attribute? Get(this ICollection<Attribute> attributes, string name)
    {
        return attributes.FirstOrDefault(attribute => attribute.Naam == name);
    }

    public static Attribute GetRequired(this ICollection<Attribute> attributes, string name)
    {
        return attributes.First(attribute => attribute.Naam == name);
    }
}
