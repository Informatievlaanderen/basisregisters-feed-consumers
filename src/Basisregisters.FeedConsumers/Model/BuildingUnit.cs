namespace Basisregisters.FeedConsumers.Model;

using System;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using NetTopologySuite.Geometries;

public sealed class BuildingUnit
{
    public string PersistentUri { get; set; }
    public int PersistentLocalId { get; set; }
    public int BuildingPersistentLocalId { get; set; }
    public BuildingUnitStatus Status { get; set; }
    public Geometry Position { get; set; }
    public BuildingUnitGeometryMethod GeometryMethod { get; set; }
    public BuildingUnitFunction Function { get; set; }
    public bool HasDeviation { get; set; }
    public DateTimeOffset VersionId { get; set; }
    public string VersionIdAsString { get; set; } = null!;
    public bool IsRemoved { get; set; }

    private BuildingUnit() { }

    public BuildingUnit(
        string persistentUri,
        int persistentLocalId,
        int buildingPersistentLocalId,
        BuildingUnitStatus status,
        Geometry position,
        BuildingUnitGeometryMethod geometryMethod,
        BuildingUnitFunction function,
        bool hasDeviation,
        DateTimeOffset versionId,
        string versionIdAsString)
    {
        PersistentUri = persistentUri;
        PersistentLocalId = persistentLocalId;
        BuildingPersistentLocalId = buildingPersistentLocalId;
        Status = status;
        Position = position;
        GeometryMethod = geometryMethod;
        Function = function;
        HasDeviation = hasDeviation;
        VersionId = versionId;
        VersionIdAsString = versionIdAsString;
        IsRemoved = false;
    }
}

public sealed class BuildingUnitConfiguration : IEntityTypeConfiguration<BuildingUnit>
{
    public void Configure(EntityTypeBuilder<BuildingUnit> builder)
    {
        builder
            .ToTable("buildingunits", FeedContext.Schema)
            .HasKey(b => b.PersistentUri);

        builder.Property(b => b.PersistentUri)
            .HasMaxLength(255)
            .IsRequired()
            .HasColumnName("persistent_uri");

        builder.Property(b => b.PersistentLocalId)
            .HasColumnName("persistent_local_id")
            .IsRequired();

        builder.Property(b => b.BuildingPersistentLocalId)
            .HasColumnName("building_persistent_local_id")
            .IsRequired();

        builder.Property(b => b.Status)
            .IsRequired()
            .HasConversion<string>()
            .HasColumnName("status");

        builder.Property(b => b.GeometryMethod)
            .IsRequired()
            .HasConversion<string>()
            .HasColumnName("geometry_method");

        builder.Property(b => b.Position)
            .IsRequired()
            .HasColumnName("position");

        builder.Property(b => b.Function)
            .IsRequired()
            .HasConversion<string>()
            .HasColumnName("function");

        builder.Property(b => b.HasDeviation)
            .HasColumnName("has_deviation")
            .IsRequired();

        builder.Property(b => b.IsRemoved)
            .HasColumnName("is_removed")
            .IsRequired();

        builder.Property(x => x.VersionId)
            .HasColumnName("version_id")
            .HasConversion(
                v => v.ToUniversalTime(),
                v => v.ToUniversalTime())
            .IsRequired();

        builder.Property(x => x.VersionIdAsString)
            .HasColumnName("version_id_as_string")
            .IsRequired();

        builder.HasIndex(x => x.PersistentLocalId);
        builder.HasIndex(x => x.BuildingPersistentLocalId);

        builder.HasIndex(x => x.Position).HasMethod("GIST");
        builder.HasIndex(x => x.IsRemoved);
        builder.HasIndex(x => x.Status);
    }
}
