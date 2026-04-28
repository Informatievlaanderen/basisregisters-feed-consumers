namespace Basisregisters.FeedConsumers.Model;

using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

public sealed class ParcelAddress
{
    public string VbrCaPaKey { get; set; }
    public int AddressPersistentLocalId { get; set; }

    private ParcelAddress() { }

    public ParcelAddress(string vbrCaPaKey, int addressPersistentLocalId)
    {
        VbrCaPaKey = vbrCaPaKey;
        AddressPersistentLocalId = addressPersistentLocalId;
    }
}

public sealed class ParcelAddressConfiguration : IEntityTypeConfiguration<ParcelAddress>
{
    public void Configure(EntityTypeBuilder<ParcelAddress> builder)
    {
        builder
            .ToTable("parcel_addresses", FeedContext.Schema)
            .HasKey(b => new { b.VbrCaPaKey, b.AddressPersistentLocalId });

        builder.Property(b => b.VbrCaPaKey)
            .HasMaxLength(24)
            .IsRequired()
            .HasColumnName("vbr_capakey");

        builder.Property(b => b.AddressPersistentLocalId)
            .IsRequired()
            .HasColumnName("address_persistent_local_id");

        builder.HasIndex(b => b.AddressPersistentLocalId);
        builder.HasIndex(b => b.VbrCaPaKey);
    }
}
