namespace Basisregisters.FeedConsumers.Model;

using System;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

public sealed class Parcel
{
    public string PersistentUri { get; set; }
    public string VbrCaPaKey { get; set; }
    public string CaPaKey { get; set; }

    public ParcelStatus Status { get; set; }
    public DateTimeOffset VersionId { get; set; }
    public string VersionIdAsString { get; set; } = null!;

    private Parcel() { }

    public Parcel(
        string persistentUri,
        string vbrCaPaKey,
        string caPaKey,
        ParcelStatus status,
        DateTimeOffset versionId,
        string versionIdAsString)
    {
        PersistentUri = persistentUri;
        VbrCaPaKey = vbrCaPaKey;
        CaPaKey = caPaKey;
        Status = status;
        VersionId = versionId;
        VersionIdAsString = versionIdAsString;
    }
}

public sealed class ParcelConfiguration : IEntityTypeConfiguration<Parcel>
{
    public void Configure(EntityTypeBuilder<Parcel> builder)
    {
        const string tableName = "parcels";

        builder
            .ToTable(tableName, FeedContext.Schema) // to schema per type
            .HasKey(x => x.PersistentUri);

        builder.Property(x => x.PersistentUri)
            .HasMaxLength(255)
            .HasColumnName("persistent_uri");

        builder.Property(x => x.VbrCaPaKey)
            .IsRequired()
            .HasMaxLength(24)
            .HasColumnName("vbr_capakey");

        builder.Property(x => x.CaPaKey)
            .IsRequired()
            .HasMaxLength(24)
            .HasColumnName("capakey");

        builder.Property(x => x.Status)
            .HasConversion<string>()
            .HasColumnName("status")
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
    }
}
