namespace Basisregisters.FeedConsumers.Model;

using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

public sealed class BuildingUnitAddress
{
    public int BuildingUnitPersistentLocalId { get; set; }
    public int AddressPersistentLocalId { get; set; }

    private BuildingUnitAddress() { }

    public BuildingUnitAddress(int buildingUnitPersistentLocalId, int addressPersistentLocalId)
    {
        BuildingUnitPersistentLocalId = buildingUnitPersistentLocalId;
        AddressPersistentLocalId = addressPersistentLocalId;
    }
}

public sealed class BuildingUnitAddressesConfiguration : IEntityTypeConfiguration<BuildingUnitAddress>
{
    public void Configure(EntityTypeBuilder<BuildingUnitAddress> builder)
    {
        builder
            .ToTable("buildingunit_addresses", FeedContext.Schema)
            .HasKey(b => new { b.BuildingUnitPersistentLocalId, b.AddressPersistentLocalId });

        builder.Property(b => b.BuildingUnitPersistentLocalId)
            .IsRequired()
            .HasColumnName("buildingunit_persistent_local_id");

        builder.Property(b => b.AddressPersistentLocalId)
            .IsRequired()
            .HasColumnName("address_persistent_local_id");

        builder.HasIndex(b => b.AddressPersistentLocalId);
        builder.HasIndex(b => b.BuildingUnitPersistentLocalId);
    }
}
