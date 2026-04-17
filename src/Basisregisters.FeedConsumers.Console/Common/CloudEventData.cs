namespace Basisregisters.FeedConsumers.Console.Common;

using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.Text.Json.Serialization;

public sealed class CloudEventData
{
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
    [JsonPropertyName("versieId")]
    [Required]
    public DateTimeOffset VersieId { get; }

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
        DateTimeOffset @versieId,
        ICollection<string> @nisCodes,
        ICollection<CloudEventAttributeChange> @attributen)
    {
        Id = @id;
        Naamruimte = @naamruimte;
        ObjectId = @objectId;
        VersieId = @versieId;
        NisCodes = @nisCodes;
        Attributen = @attributen;
    }
}
