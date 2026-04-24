using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace IoTSharp.Data.Configurations;

public class DeviceTypeProfileConfiguration : IEntityTypeConfiguration<DeviceTypeProfile>
{
    public void Configure(EntityTypeBuilder<DeviceTypeProfile> builder)
    {
        builder.ToTable("DeviceTypeProfiles");
        builder.HasKey(p => p.Id);

        builder.Property(p => p.ProfileKey)
            .IsRequired()
            .HasMaxLength(100);

        builder.Property(p => p.ProfileName)
            .HasMaxLength(200);

        builder.Property(p => p.Description)
            .HasColumnType("text");

        builder.Property(p => p.Icon)
            .HasMaxLength(500);

        builder.Property(p => p.Version)
            .HasDefaultValue(1);

        builder.Property(p => p.Enabled)
            .HasDefaultValue(true);

        builder.Property(p => p.CreatedAt)
            .IsRequired();

        builder.Property(p => p.UpdatedAt)
            .IsRequired();

        builder.HasIndex(p => p.ProfileKey).IsUnique();
        builder.HasIndex(p => p.DeviceType);
        builder.HasIndex(p => p.Enabled);
    }
}