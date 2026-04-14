namespace Basisregisters.FeedConsumers.Model;

using System;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

public sealed class Municipality
{
    public string PersistentUri { get; set; }
    public string NisCode { get; set; }
    public DateTimeOffset VersionId { get; set; }
    public MunicipalityStatus Status { get; set; }

    public bool? OfficialLanguageDutch { get; set; }
    public bool? OfficialLanguageFrench { get; set; }
    public bool? OfficialLanguageGerman { get; set; }
    public bool? OfficialLanguageEnglish { get; set; }

    public bool? FacilityLanguageDutch { get; set; }
    public bool? FacilityLanguageFrench { get; set; }
    public bool? FacilityLanguageGerman { get; set; }
    public bool? FacilityLanguageEnglish { get; set; }

    public string? NameDutch { get; set; }
    public string? NameFrench { get; set; }
    public string? NameGerman { get; set; }
    public string? NameEnglish { get; set; }

    public bool IsRemoved { get; set; }

    private Municipality() { }

    public Municipality(string persistentUri, string nisCode, DateTimeOffset versionId, MunicipalityStatus status, bool isRemoved)
    {
        PersistentUri = persistentUri;
        NisCode = nisCode;
        VersionId = versionId;
        Status = status;
        IsRemoved = isRemoved;
    }
}

public sealed class MunicipalityConfiguration : IEntityTypeConfiguration<Municipality>
{
    public void Configure(EntityTypeBuilder<Municipality> builder)
    {
        builder.ToTable("municipalities", FeedContext.Schema)
            .HasKey(x => x.PersistentUri);

        builder
            .Property(x => x.PersistentUri)
            .HasColumnName("persistent_uri")
            .HasMaxLength(255);

        builder.Property(x => x.VersionId)
            .HasColumnName("version_id")
            .HasConversion(
                v => v.ToUniversalTime(),
                v => v.ToUniversalTime())
            .IsRequired();

        builder.Property(x => x.NisCode)
            .HasColumnName("nis_code")
            .IsRequired();

        builder.Property(x => x.Status)
            .HasConversion<string>()
            .HasColumnName("status")
            .IsRequired();

        builder
            .Property(x => x.OfficialLanguageDutch)
            .HasColumnName("official_language_dutch");
        builder
            .Property(x => x.OfficialLanguageFrench)
            .HasColumnName("official_language_french");
        builder
            .Property(x => x.OfficialLanguageGerman)
            .HasColumnName("official_language_german");
        builder
            .Property(x => x.OfficialLanguageEnglish)
            .HasColumnName("official_language_english");

        builder
            .Property(x => x.FacilityLanguageDutch)
            .HasColumnName("facility_language_dutch");
        builder
            .Property(x => x.FacilityLanguageFrench)
            .HasColumnName("facility_language_french");
        builder
            .Property(x => x.FacilityLanguageGerman)
            .HasColumnName("facility_language_german");
        builder
            .Property(x => x.FacilityLanguageEnglish)
            .HasColumnName("facility_language_english");

        builder
            .Property(x => x.NameDutch)
            .HasColumnName("name_dutch");
        builder
            .Property(x => x.NameFrench)
            .HasColumnName("name_french");
        builder
            .Property(x => x.NameGerman)
            .HasColumnName("name_german");
        builder
            .Property(x => x.NameEnglish)
            .HasColumnName("name_english");

        builder.Property(x => x.IsRemoved)
            .HasColumnName("is_removed")
            .IsRequired();
    }
}
