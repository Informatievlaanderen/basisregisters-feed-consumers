namespace Basisregisters.FeedConsumers.Model;

using System;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

public sealed class StreetName
{
    public string PersistentUri { get; set; } = null!;
    public int PersistentLocalId { get; set; }
    public StreetNameStatus Status { get; set; }

    public string NisCode { get; set; } = null!;

    public string? NameDutch { get; set; }
    public string? NameFrench { get; set; }
    public string? NameGerman { get; set; }
    public string? NameEnglish { get; set; }

    public string? HomonymAdditionDutch { get; set; }
    public string? HomonymAdditionFrench { get; set; }
    public string? HomonymAdditionGerman { get; set; }
    public string? HomonymAdditionEnglish { get; set; }

    public bool IsRemoved { get; set; }

    public DateTimeOffset VersionId { get; set; }

    private StreetName() { }

    public StreetName(string persistentUri, int persistentLocalId, string nisCode, StreetNameStatus status, DateTimeOffset versionId)
    {
        PersistentUri = persistentUri;
        PersistentLocalId = persistentLocalId;
        NisCode = nisCode;
        Status = status;
        VersionId = versionId;
    }
}

public sealed class StreetNameLatestItemConfiguration : IEntityTypeConfiguration<StreetName>
{
    internal const string TableName = "streetnames";

    public void Configure(EntityTypeBuilder<StreetName> builder)
    {
        builder.ToTable(TableName, FeedContext.Schema)
            .HasKey(x => x.PersistentUri);

        builder.Property(x => x.PersistentUri)
            .HasMaxLength(255)
            .HasColumnName("persistent_uri");

        builder.Property(x => x.PersistentLocalId)
            .IsRequired()
            .HasColumnName("persistent_local_id");

        builder.Property(x => x.NisCode).HasColumnName("nis_code");

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

        builder.Property(x => x.NameDutch).HasColumnName("name_dutch");
        builder.Property(x => x.NameFrench).HasColumnName("name_french");
        builder.Property(x => x.NameGerman).HasColumnName("name_german");
        builder.Property(x => x.NameEnglish).HasColumnName("name_english");

        builder.Property(x => x.HomonymAdditionDutch).HasColumnName("homonym_addition_dutch");
        builder.Property(x => x.HomonymAdditionFrench).HasColumnName("homonym_addition_french");
        builder.Property(x => x.HomonymAdditionGerman).HasColumnName("homonym_addition_german");
        builder.Property(x => x.HomonymAdditionEnglish).HasColumnName("homonym_addition_english");

        builder.Property(x => x.IsRemoved).HasColumnName("is_removed");

        builder.HasIndex(x => x.PersistentLocalId);
        builder.HasIndex(x => x.NisCode);
        builder.HasIndex(x => x.Status);

        builder.HasIndex(x => x.IsRemoved);
    }
}
