namespace Basisregisters.FeedConsumers.Model;

using System;
using System.Collections.Generic;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

public sealed class PostalInformation
{
    public string PersistentUri { get; set; } = null!;

    public string PostalCode { get; set; } = null!;
    public string? NisCode { get; set; }
    public PostalInformationStatus Status { get; set; }
    public List<PostalInformationName> PostalNames { get; set; }
    public bool IsRemoved { get; set; }

    public DateTimeOffset VersionId { get; set; }
    public string VersionIdAsString { get; set; } = null!;

    private PostalInformation() { }

    public PostalInformation(
        string persistentUri,
        string postalCode,
        string? nisCode,
        PostalInformationStatus status,
        DateTimeOffset versionId,
        string versionIdAsString)
    {
        PersistentUri = persistentUri;
        PostalCode = postalCode;
        NisCode = nisCode;
        Status = status;
        VersionId = versionId;
        VersionIdAsString = versionIdAsString;
        PostalNames = [];
        IsRemoved = false;
    }
}

public sealed class PostalInformationName
{
    public Guid Id { get; set; }
    public string Name { get; set; } = null!;
    public Language Language { get; set; }
    public string PostalCode { get; set; } = null!;

    public PostalInformationName(string name, Language language, string postalCode)
    {
        Name = name;
        Language = language;
        PostalCode = postalCode;
    }

    private PostalInformationName() { }
}

public sealed class PostalInformationConfiguration : IEntityTypeConfiguration<PostalInformation>
{
    public void Configure(EntityTypeBuilder<PostalInformation> builder)
    {
        builder
            .ToTable("postal_information", FeedContext.Schema)
            .HasKey(x => x.PersistentUri);

        builder.Property(x => x.PersistentUri)
            .HasMaxLength(255)
            .HasColumnName("persistent_uri");

        builder.Property(p => p.PostalCode)
            .IsRequired()
            .HasColumnName("postal_code");

        builder.Property(p => p.NisCode)
            .HasMaxLength(5)
            .HasColumnName("nis_code");

        builder.Property(x => x.Status)
            .HasConversion<string>()
            .HasColumnName("status")
            .IsRequired();

        builder.HasMany(p => p.PostalNames)
            .WithOne()
            .HasPrincipalKey(p => p.PostalCode)
            .HasForeignKey(p => p.PostalCode);

        builder.Property(x => x.VersionId)
            .HasColumnName("version_id")
            .HasConversion(
                v => v.ToUniversalTime(),
                v => v.ToUniversalTime())
            .IsRequired();

        builder.Property(x => x.VersionIdAsString)
            .HasColumnName("version_id_as_string")
            .IsRequired();

        builder.Property(x => x.IsRemoved)
            .HasColumnName("is_removed")
            .IsRequired();

        builder.HasIndex(x => x.PostalCode)
            .IsUnique();

        builder.HasIndex(x => x.NisCode);
    }
}

public sealed class PostalInformationNameConfiguration : IEntityTypeConfiguration<PostalInformationName>
{
    public void Configure(EntityTypeBuilder<PostalInformationName> builder)
    {
        builder.ToTable("postal_information_name", FeedContext.Schema)
            .HasKey(p => p.Id);

        builder.Property(p => p.Id)
            .ValueGeneratedOnAdd()
            .HasColumnName("id");

        builder.Property(p => p.Name)
            .IsRequired()
            .HasColumnName("name");
        builder.Property(p => p.PostalCode)
            .IsRequired()
            .HasColumnName("postal_code");
        builder.Property(p => p.Language)
            .IsRequired()
            .HasColumnName("language");

        builder.HasIndex(x => x.PostalCode);
    }
}
