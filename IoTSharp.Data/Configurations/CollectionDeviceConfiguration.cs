using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace IoTSharp.Data.Configurations;

public class CollectionDeviceConfiguration : IEntityTypeConfiguration<CollectionDevice>
{
    public void Configure(EntityTypeBuilder<CollectionDevice> builder)
    {
        builder.ToTable("CollectionDevices");
        builder.HasKey(d => d.Id);

        builder.Property(d => d.DeviceKey)
            .IsRequired()
            .HasMaxLength(100);

        builder.Property(d => d.DeviceName)
            .HasMaxLength(200);

        builder.Property(d => d.SlaveId)
            .IsRequired();

        builder.Property(d => d.Enabled)
            .HasDefaultValue(true);

        builder.Property(d => d.SortOrder)
            .HasDefaultValue(0);

        builder.Property(d => d.ProtocolOptionsJson)
            .HasColumnType("text");

        builder.Property(d => d.CreatedAt)
            .IsRequired();

        builder.Property(d => d.UpdatedAt)
            .IsRequired();

        builder.HasOne(d => d.Task)
            .WithMany(t => t.Devices)
            .HasForeignKey(d => d.TaskId)
            .OnDelete(DeleteBehavior.Cascade);

        builder.HasIndex(d => d.TaskId);
        builder.HasIndex(d => d.SlaveId);
        builder.HasIndex(d => new { d.TaskId, d.SlaveId }).IsUnique();
    }
}