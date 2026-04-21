namespace Basisregisters.FeedConsumers.Console.Common;

using System;
using System.ComponentModel.DataAnnotations;
using System.Text.Json.Serialization;

public sealed class GeometryData
{
    public const string AttributeNameType = "type";
    public const string AttributeNameProjection = "projectie";
    public const string AttributeNameGml = "gml";

    public const string Lambert1972Projection = "http://www.opengis.net/def/crs/EPSG/0/31370";
    public const string Lambert2008Projection = "http://www.opengis.net/def/crs/EPSG/0/3812";

    [JsonPropertyName(AttributeNameType)]
    [Required]
    public string Type { get; set; }

    [JsonPropertyName(AttributeNameProjection)]
    [Required]
    public string Projection { get; set; }

    [JsonPropertyName(AttributeNameGml)]
    [Required]
    public string Gml { get; set; }

    [JsonIgnore]
    public bool IsLambert2008 => string.Equals(Projection, Lambert2008Projection, StringComparison.InvariantCultureIgnoreCase);

    [JsonConstructor]
    public GeometryData(string type, string projection, string gml)
    {
        Type = type;
        Projection = projection;
        Gml = gml;
    }
}
