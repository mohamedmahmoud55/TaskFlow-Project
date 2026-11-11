using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using TaskFlow.Data.Entities;

namespace TaskFlow.Data.Configurations;

public class NotificationConfiguration : IEntityTypeConfiguration<Notification>
{
    public void Configure(EntityTypeBuilder<Notification> builder)
    {
        builder.ToTable("Notifications");
        builder.HasKey(n => n.Id);
        builder.Property(n => n.UserId).IsRequired();
        builder.Property(n => n.Type).IsRequired();
        builder.Property(n => n.Title).IsRequired().HasMaxLength(200);
        builder.Property(n => n.Message).IsRequired().HasMaxLength(2000);
        builder.Property(n => n.RelatedEntityType).HasMaxLength(100);
        builder.Property(n => n.ActionUrl).HasMaxLength(2048);
        builder.Property(n => n.IsRead).IsRequired();
        builder.Property(n => n.CreatedAt).IsRequired();

        builder.HasOne(n => n.User)
            .WithMany(user => user.Notifications)
            .HasForeignKey(n => n.UserId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasIndex(n => new { n.UserId, n.IsRead, n.CreatedAt });
        builder.HasIndex(n => new { n.UserId, n.CreatedAt });
        builder.HasIndex(n => n.ExpiresAt);
    }
}
