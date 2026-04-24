using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace IoTSharp.Data.Configurations;

public class CollectionLogConfiguration : IEntityTypeConfiguration<CollectionLog>
{
    public void Configure(EntityTypeBuilder<CollectionLog> builder)
    {
        builder.ToTable("CollectionLogs");
        builder.HasKey(l => l.Id);

        builder.Property(l => l.RequestId)
            .IsRequired()
            .HasMaxLength(100);

        builder.Property(l => l.RequestAt)
            .IsRequired();

        builder.Property(l => l.RequestFrame)
            .HasColumnType("text");

        builder.Property(l => l.ResponseFrame)
            .HasColumnType("text");

        builder.Property(l => l.ParsedValue)
            .HasMaxLength(500);

        builder.Property(l => l.ConvertedValue)
            .HasMaxLength(500);

        builder.Property(l => l.Status)
            .IsRequired()
            .HasMaxLength(50);

        builder.Property(l => l.ErrorMessage)
            .HasColumnType("text");

        builder.Property(l => l.CreatedAt)
            .IsRequired();

        builder.HasIndex(l => l.GatewayDeviceId);
        builder.HasIndex(l => l.RequestId);
        builder.HasIndex(l => l.Status);
        builder.HasIndex(l => l.CreatedAt);
        builder.HasIndex(l => new { l.GatewayDeviceId, l.CreatedAt });
    }
}