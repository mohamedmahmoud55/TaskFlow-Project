using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using TaskFlow.Data.Entities;

namespace TaskFlow.Data.Configurations;

public class WebhookDeliveryConfiguration : IEntityTypeConfiguration<WebhookDelivery>
{
    public void Configure(EntityTypeBuilder<WebhookDelivery> builder)
    {
        builder.ToTable("WebhookDeliveries");
        builder.HasKey(d => d.Id);
        builder.Property(d => d.EventType).IsRequired().HasMaxLength(100);
        builder.Property(d => d.ResponseBody).HasColumnType("nvarchar(max)");
        builder.Property(d => d.ErrorMessage).HasMaxLength(2000);
        builder.Property(d => d.AttemptedAt).IsRequired();
        builder.Property(d => d.Succeeded).IsRequired();
        builder.HasOne(d => d.WebhookSubscription).WithMany(s => s.Deliveries).HasForeignKey(d => d.WebhookSubscriptionId).OnDelete(DeleteBehavior.Cascade);
        builder.HasIndex(d => new { d.WebhookSubscriptionId, d.AttemptedAt });
        builder.HasIndex(d => new { d.Succeeded, d.AttemptedAt });
    }
}
