using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace IoTSharp.Data.Configurations;

public class CollectionRuleTemplateConfiguration : IEntityTypeConfiguration<CollectionRuleTemplate>
{
    public void Configure(EntityTypeBuilder<CollectionRuleTemplate> builder)
    {
        builder.ToTable("CollectionRuleTemplates");
        builder.HasKey(r => r.Id);

        builder.Property(r => r.PointKey)
            .IsRequired()
            .HasMaxLength(100);

        builder.Property(r => r.PointName)
            .HasMaxLength(200);

        builder.Property(r => r.Description)
            .HasColumnType("text");

        builder.Property(r => r.FunctionCode)
            .IsRequired();

        builder.Property(r => r.Address)
            .IsRequired();

        builder.Property(r => r.RegisterCount)
            .HasDefaultValue((ushort)1);

        builder.Property(r => r.RawDataType)
            .IsRequired()
            .HasMaxLength(50)
            .HasDefaultValue("uint16");

        builder.Property(r => r.ByteOrder)
            .HasMaxLength(10)
            .HasDefaultValue("AB");

        builder.Property(r => r.WordOrder)
            .HasMaxLength(10)
            .HasDefaultValue("AB");

        builder.Property(r => r.ReadPeriodMs)
            .HasDefaultValue(30000);

        builder.Property(r => r.PollingGroup)
            .HasMaxLength(50);

        builder.Property(r => r.TransformsJson)
            .HasColumnType("text");

        builder.Property(r => r.TargetName)
            .HasMaxLength(100);

        builder.Property(r => r.TargetType)
            .HasMaxLength(50)
            .HasDefaultValue("Telemetry");

        builder.Property(r => r.TargetValueType)
            .HasMaxLength(50)
            .HasDefaultValue("Double");

        builder.Property(r => r.Unit)
            .HasMaxLength(50);

        builder.Property(r => r.GroupName)
            .HasMaxLength(100);

        builder.Property(r => r.SortOrder)
            .HasDefaultValue(0);

        builder.Property(r => r.CreatedAt)
            .IsRequired();

        builder.Property(r => r.UpdatedAt)
            .IsRequired();

        builder.HasOne(r => r.Profile)
            .WithMany(p => p.CollectionRules)
            .HasForeignKey(r => r.ProfileId)
            .OnDelete(DeleteBehavior.Cascade);

        builder.HasIndex(r => r.ProfileId);
    }
}