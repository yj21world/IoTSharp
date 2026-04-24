using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace IoTSharp.Data.Configurations;

public class CollectionTaskConfiguration : IEntityTypeConfiguration<CollectionTask>
{
    public void Configure(EntityTypeBuilder<CollectionTask> builder)
    {
        builder.ToTable("CollectionTasks");
        builder.HasKey(t => t.Id);

        builder.Property(t => t.TaskKey)
            .IsRequired()
            .HasMaxLength(100);

        builder.Property(t => t.Protocol)
            .IsRequired()
            .HasMaxLength(50)
            .HasDefaultValue("Modbus");

        builder.Property(t => t.Version)
            .HasDefaultValue(1);

        builder.Property(t => t.Enabled)
            .HasDefaultValue(true);

        builder.Property(t => t.ConnectionJson)
            .HasColumnType("text");

        builder.Property(t => t.ReportPolicyJson)
            .HasColumnType("text");

        builder.Property(t => t.CreatedAt)
            .IsRequired();

        builder.Property(t => t.UpdatedAt)
            .IsRequired();

        builder.HasOne(t => t.GatewayDevice)
            .WithMany()
            .HasForeignKey(t => t.GatewayDeviceId)
            .OnDelete(DeleteBehavior.Cascade);

        builder.HasIndex(t => t.TaskKey).IsUnique();
        builder.HasIndex(t => t.GatewayDeviceId);
        builder.HasIndex(t => t.Enabled);
    }
}