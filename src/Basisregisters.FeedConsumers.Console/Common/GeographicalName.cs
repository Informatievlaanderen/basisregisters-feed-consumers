namespace Basisregisters.FeedConsumers.Console.Common;

using System.ComponentModel.DataAnnotations;
using System.Text.Json.Serialization;

public sealed class GeographicalName
{
    public const string AttributeNameLanguage = "taal";
    public const string AttributeNameSpelling = "spelling";

    [JsonPropertyName(AttributeNameLanguage)]
    [Required]
    public string Taal { get; set; }

    [JsonPropertyName(AttributeNameSpelling)]
    [Required]
    public string Spelling { get; set; }

    [JsonConstructor]
    public GeographicalName(string taal, string spelling)
    {
        Taal = taal;
        Spelling = spelling;
    }
}
