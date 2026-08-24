namespace Basisregisters.FeedConsumers.Console.Common;

using System;
using System.ComponentModel.DataAnnotations;
using System.Text.Json.Serialization;
using System.Xml;
using System.Xml.Linq;

/// <summary>
/// A geometry as published by the feeds, e.g. { "@type": "Punt", "gml": "&lt;gml:Point srsName=... /&gt;" }.
/// The projection is no longer a separate attribute and is read from the GML srsName instead.
/// </summary>
public sealed class GeometryData
{
    public const string AttributeNameType = "@type";
    public const string AttributeNameGml = "gml";

    public const string SrsNameAttributeName = "srsName";

    public const string Lambert1972SrsName = "http://www.opengis.net/def/crs/EPSG/0/31370";
    public const string Lambert2008SrsName = "http://www.opengis.net/def/crs/EPSG/0/3812";

    private string? _srsName;

    [JsonPropertyName(AttributeNameType)]
    [Required]
    public string Type { get; set; }

    [JsonPropertyName(AttributeNameGml)]
    [Required]
    public string Gml { get; set; }

    /// <summary>
    /// The projection of the geometry, read from the srsName of the GML.
    /// </summary>
    [JsonIgnore]
    public string? SrsName => _srsName ??= ExtractSrsName(Gml);

    [JsonIgnore]
    public bool IsLambert2008 => string.Equals(SrsName, Lambert2008SrsName, StringComparison.OrdinalIgnoreCase);

    [JsonConstructor]
    public GeometryData(string type, string gml)
    {
        Type = type;
        Gml = gml;
    }

    private static string? ExtractSrsName(string gml)
    {
        if (string.IsNullOrWhiteSpace(gml))
            return null;

        try
        {
            return XElement.Parse(gml).Attribute(SrsNameAttributeName)?.Value;
        }
        catch (XmlException)
        {
            return null;
        }
    }
}
