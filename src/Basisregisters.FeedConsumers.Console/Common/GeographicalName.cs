namespace Basisregisters.FeedConsumers.Console.Common;

using System.ComponentModel.DataAnnotations;
using System.Text.Json.Serialization;

/// <summary>
/// JSON-LD language tagged name, e.g. { "@value": "Hasselt", "@language": "nl" }
/// </summary>
public sealed class GeographicalName
{
    public const string AttributeNameValue = "@value";
    public const string AttributeNameLanguage = "@language";

    [JsonPropertyName(AttributeNameValue)]
    [Required]
    public string Value { get; set; }

    [JsonPropertyName(AttributeNameLanguage)]
    [Required]
    public string Language { get; set; }

    [JsonConstructor]
    public GeographicalName(string value, string language)
    {
        Value = value;
        Language = language;
    }
}
