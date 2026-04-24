using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace IoTSharp.Data.Configurations;

public class CollectionPointConfiguration : IEntityTypeConfiguration<CollectionPoint>
{
    public void Configure(EntityTypeBuilder<CollectionPoint> builder)
    {
        builder.ToTable("CollectionPoints");
        builder.HasKey(p => p.Id);

        builder.Property(p => p.PointKey)
            .IsRequired()
            .HasMaxLength(100);

        builder.Property(p => p.PointName)
            .HasMaxLength(200);

        builder.Property(p => p.FunctionCode)
            .IsRequired();

        builder.Property(p => p.Address)
            .IsRequired();

        builder.Property(p => p.RegisterCount)
            .HasDefaultValue(1);

        builder.Property(p => p.RawDataType)
            .IsRequired()
            .HasMaxLength(50)
            .HasDefaultValue("uint16");

        builder.Property(p => p.ByteOrder)
            .HasMaxLength(10)
            .HasDefaultValue("AB");

        builder.Property(p => p.WordOrder)
            .HasMaxLength(10)
            .HasDefaultValue("AB");

        builder.Property(p => p.ReadPeriodMs)
            .HasDefaultValue(30000);

        builder.Property(p => p.PollingGroup)
            .HasMaxLength(50);

        builder.Property(p => p.TransformsJson)
            .HasColumnType("text");

        builder.Property(p => p.TargetName)
            .HasMaxLength(100);

        builder.Property(p => p.TargetType)
            .HasMaxLength(50)
            .HasDefaultValue("Telemetry");

        builder.Property(p => p.TargetValueType)
            .HasMaxLength(50)
            .HasDefaultValue("Double");

        builder.Property(p => p.DisplayName)
            .HasMaxLength(200);

        builder.Property(p => p.Unit)
            .HasMaxLength(50);

        builder.Property(p => p.GroupName)
            .HasMaxLength(100);

        builder.Property(p => p.Enabled)
            .HasDefaultValue(true);

        builder.Property(p => p.SortOrder)
            .HasDefaultValue(0);

        builder.Property(p => p.CreatedAt)
            .IsRequired();

        builder.Property(p => p.UpdatedAt)
            .IsRequired();

        builder.HasOne(p => p.Device)
            .WithMany(d => d.Points)
            .HasForeignKey(p => p.DeviceId)
            .OnDelete(DeleteBehavior.Cascade);

        builder.HasOne(p => p.TargetDevice)
            .WithMany()
            .HasForeignKey(p => p.TargetDeviceId)
            .OnDelete(DeleteBehavior.SetNull);

        builder.HasIndex(p => p.DeviceId);
        builder.HasIndex(p => p.TargetDeviceId);
        builder.HasIndex(p => new { p.DeviceId, p.Address, p.FunctionCode }).IsUnique();
    }
}