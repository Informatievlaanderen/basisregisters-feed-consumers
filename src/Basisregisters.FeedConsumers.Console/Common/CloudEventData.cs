namespace Basisregisters.FeedConsumers.Console.Common;

using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.Globalization;
using System.Text.Json.Serialization;

public sealed class CloudEventData
{
    private DateTimeOffset? _versieId;

    /// <summary>
    /// Canonical identifier URI of the object
    /// </summary>
    [JsonPropertyName("@id")]
    [Required]
    public Uri Id { get; }

    /// <summary>
    /// Object identifier
    /// </summary>
    [JsonPropertyName("objectId")]
    [Required]
    public string ObjectId { get; }

    /// <summary>
    /// Namespace URI
    /// </summary>
    [JsonPropertyName("naamruimte")]
    [Required]
    public Uri Naamruimte { get; }

    /// <summary>
    /// Version timestamp of the object state
    /// </summary>
    [JsonIgnore]
    public DateTimeOffset VersieId => _versieId ??= ParseVersionId(VersieIdAsString);

    [JsonPropertyName("versieId")]
    [Required]
    public string VersieIdAsString { get; init; } = null!;

    /// <summary>
    /// List of NIS codes associated with the object
    /// </summary>
    [JsonPropertyName("nisCodes")]
    [Required]
    [MinLength(1)]
    public ICollection<string> NisCodes { get; }

    /// <summary>
    /// List of attribute changes
    /// </summary>
    [JsonPropertyName("attributen")]
    [Required]
    public ICollection<CloudEventAttributeChange> Attributen { get; }

    [JsonConstructor]
    public CloudEventData(
        Uri @id,
        Uri @naamruimte,
        string @objectId,
        string versieIdAsString,
        ICollection<string> @nisCodes,
        ICollection<CloudEventAttributeChange> @attributen)
    {
        Id = @id;
        Naamruimte = @naamruimte;
        ObjectId = @objectId;
        VersieIdAsString = versieIdAsString;
        NisCodes = @nisCodes;
        Attributen = @attributen;
    }

    private static DateTimeOffset ParseVersionId(string versieIdAsString)
    {
        return DateTimeOffset.Parse(versieIdAsString, CultureInfo.InvariantCulture, DateTimeStyles.RoundtripKind);
    }
}
