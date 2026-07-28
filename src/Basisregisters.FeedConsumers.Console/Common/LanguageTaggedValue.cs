namespace Basisregisters.FeedConsumers.Console.Common;

using System.ComponentModel.DataAnnotations;
using System.Text.Json.Serialization;

//TODO: rename to GeographicalName and remove old one after refactors
/// <summary>
/// JSON-LD language tagged string, e.g. { "@value": "Hasselt", "@language": "nl" }
/// </summary>
public sealed class LanguageTaggedValue
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
    public LanguageTaggedValue(string value, string language)
    {
        Value = value;
        Language = language;
    }
}
