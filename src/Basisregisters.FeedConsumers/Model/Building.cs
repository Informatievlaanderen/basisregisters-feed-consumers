namespace Basisregisters.FeedConsumers.Model;

using System;
using Microsoft.EntityFrameworkCore;
using NetTopologySuite.Geometries;

public sealed class Building
{
    public string PersistentUri { get; set; }
    public int PersistentLocalId { get; set; }
    public BuildingStatus Status { get; set; }
    public BuildingGeometryMethod GeometryMethod { get; set; }
    public Geometry Geometry { get; set; }
    public DateTimeOffset VersionId { get; set; }

    public bool IsRemoved { get; set; }

    private Building() { }

    public Building(
        string persistentUri,
        int persistentLocalId,
        BuildingStatus status,
        BuildingGeometryMethod geometryMethod,
        Geometry geometry,
        DateTimeOffset versionId)
    {
        PersistentUri = persistentUri;
        PersistentLocalId = persistentLocalId;
        Status = status;
        GeometryMethod = geometryMethod;
        Geometry = geometry;
        VersionId = versionId;
        IsRemoved = false;
    }
}

public sealed class BuildingConfiguration : IEntityTypeConfiguration<Building>
{
    public void Configure(Microsoft.EntityFrameworkCore.Metadata.Builders.EntityTypeBuilder<Building> builder)
    {
        builder
            .ToTable("buildings", FeedContext.Schema)
            .HasKey(b => b.PersistentUri);

        builder.Property(b => b.PersistentUri)
            .HasMaxLength(255)
            .IsRequired()
            .HasColumnName("persistent_uri");

        builder.Property(b => b.PersistentLocalId)
            .HasColumnName("persistent_local_id")
            .IsRequired();

        builder.Property(b => b.Status)
            .IsRequired()
            .HasConversion<string>()
            .HasColumnName("status");

        builder.Property(b => b.GeometryMethod)
            .IsRequired()
            .HasConversion<string>()
            .HasColumnName("geometry_method");

        builder.Property(b => b.Geometry)
            .IsRequired()
            .HasColumnName("geometry");

        builder.Property(b => b.VersionId)
            .HasColumnName("version_id")
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

        builder.HasIndex(x => x.PersistentLocalId);

        builder.HasIndex(x => x.Geometry).HasMethod("GIST");
        builder.HasIndex(x => x.IsRemoved);
        builder.HasIndex(x => x.Status);
    }
}
