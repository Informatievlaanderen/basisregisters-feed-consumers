namespace Basisregisters.FeedConsumers.Console.Common;

using System.ComponentModel.DataAnnotations;
using System.Text.Json.Serialization;

public sealed class CloudEventAttributeChange : object
{
    /// <summary>
    /// Name of the changed attribute
    /// </summary>
    [JsonPropertyName("naam")]
    [Required]
    public string Naam { get; }

    [JsonPropertyName("oudeWaarde")]
    [Required]
    public object OudeWaarde { get; }

    [JsonPropertyName("nieuweWaarde")]
    [Required]
    public object? NieuweWaarde { get; }

    [JsonConstructor]
    public CloudEventAttributeChange(string @naam, object @nieuweWaarde, object @oudeWaarde)
    {
        Naam = @naam;
        OudeWaarde = @oudeWaarde;
        NieuweWaarde = @nieuweWaarde;
    }
}
