namespace Basisregisters.FeedConsumers.Model;

using System;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using NetTopologySuite.Geometries;

public sealed class Address
{
    public string PersistentUri { get; set; }

    public int PersistentLocalId { get; set; }
    public string? PostalCode { get; set; }
    public int StreetNamePersistentLocalId { get; set; }
    public AddressStatus Status { get; set; }
    public string HouseNumber { get; set; }
    public string? BoxNumber { get; set; }
    public Geometry Geometry { get; set; } = null!;
    public AddressPositionGeometryMethod PositionMethod { get; set; }
    public AddressPositionSpecification PositionSpecification { get; set; }
    public bool OfficiallyAssigned { get; set; }
    public bool IsRemoved { get; set; }

    public DateTimeOffset VersionId { get; set; }

    private Address() { }

    public Address(
        string persistentUri,
        int persistentLocalId,
        int streetNamePersistentLocalId,
        string houseNumber,
        AddressStatus status,
        DateTimeOffset versionId)
    {
        PersistentUri = persistentUri;
        PersistentLocalId = persistentLocalId;
        StreetNamePersistentLocalId = streetNamePersistentLocalId;
        HouseNumber = houseNumber;
        Status = status;
        VersionId = versionId;
        IsRemoved = false;
    }
}

public sealed class AddressConfiguration : IEntityTypeConfiguration<Address>
{
    public void Configure(EntityTypeBuilder<Address> builder)
    {
        const string tableName = "addresses";

        builder
            .ToTable(tableName, FeedContext.Schema) // to schema per type
            .HasKey(x => x.PersistentUri);

        builder.Property(x => x.PersistentUri)
            .HasMaxLength(255)
            .HasColumnName("persistent_uri");

        builder.Property(x => x.PersistentLocalId)
            .IsRequired()
            .HasColumnName("persistent_local_id")
            .ValueGeneratedNever();

        builder.Property(x => x.PostalCode)
            .HasMaxLength(4)
            .HasColumnName("postal_code");

        builder.Property(x => x.StreetNamePersistentLocalId)
            .IsRequired()
            .HasColumnName("street_name_persistent_local_id");

        builder.Property(x => x.Status)
            .HasConversion<string>()
            .HasColumnName("status")
            .IsRequired();

        builder.Property(x => x.HouseNumber)
            .IsRequired()
            .HasColumnName("house_number");

        builder.Property(x => x.BoxNumber)
            .HasColumnName("box_number");

        builder.Property(x => x.Geometry)
            .IsRequired()
            .HasColumnName("geometry");

        builder.Property(x => x.PositionMethod)
            .IsRequired()
            .HasConversion<string>()
            .HasColumnName("position_method");

        builder.Property(x => x.PositionSpecification)
            .IsRequired()
            .HasConversion<string>()
            .HasColumnName("position_specification");

        builder.Property(x => x.OfficiallyAssigned)
            .IsRequired()
            .HasColumnName("officially_assigned");

        builder.Property(x => x.IsRemoved)
            .IsRequired()
            .HasColumnName("removed");

        builder.Property(x => x.VersionId)
            .HasColumnName("version_id")
            .HasConversion(
                v => v.ToUniversalTime(),
                v => v.ToUniversalTime())
            .IsRequired();

        builder.HasIndex(x => x.PersistentLocalId);

        builder.HasIndex(x => x.Geometry).HasMethod("GIST");

        builder.HasIndex(x => x.StreetNamePersistentLocalId);
        builder.HasIndex(x => x.Status);
        builder.HasIndex(x => x.PostalCode);
        builder.HasIndex(x => x.HouseNumber);
        builder.HasIndex(x => x.BoxNumber);
        builder.HasIndex(x => x.IsRemoved);
        builder.HasIndex(x => new { x.IsRemoved, x.Status });
    }
}
