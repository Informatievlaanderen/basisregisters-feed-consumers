namespace Basisregisters.FeedConsumers.Model;

using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

public sealed class FeedState
{
    public string Name { get; set; } = null!;
    public long EventPosition { get; set; }
    public int Page { get; set; }

    private FeedState() { }

    public FeedState(string name, long eventPosition, int page)
    {
        Name = name;
        EventPosition = eventPosition;
        Page = page;
    }
}

public sealed class FeedStateConfiguration : IEntityTypeConfiguration<FeedState>
{
    public void Configure(EntityTypeBuilder<FeedState> builder)
    {
        builder
            .ToTable("feed_states", FeedContext.Schema)
            .HasKey(x => x.Name);

        builder
            .Property(x => x.Name)
            .HasColumnName("name")
            .HasMaxLength(255);

        builder
            .Property(x => x.EventPosition)
            .HasColumnName("event_position")
            .ValueGeneratedNever();

        builder
            .Property(x => x.Page)
            .HasColumnName("page")
            .ValueGeneratedNever();
    }
}
