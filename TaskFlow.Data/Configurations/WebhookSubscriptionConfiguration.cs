using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using TaskFlow.Data.Entities;

namespace TaskFlow.Data.Configurations;

public class WebhookSubscriptionConfiguration : IEntityTypeConfiguration<WebhookSubscription>
{
    public void Configure(EntityTypeBuilder<WebhookSubscription> builder)
    {
        builder.ToTable("WebhookSubscriptions");
        builder.HasKey(s => s.Id);
        builder.Property(s => s.EndpointUrl).IsRequired().HasMaxLength(2048);
        builder.Property(s => s.ProtectedSecret).IsRequired().HasColumnType("nvarchar(max)");
        builder.Property(s => s.EventTypesJson).IsRequired().HasColumnType("nvarchar(max)");
        builder.Property(s => s.Status).IsRequired();
        builder.Property(s => s.CreatedAt).IsRequired();
        builder.HasOne(s => s.Workspace).WithMany().HasForeignKey(s => s.WorkspaceId).OnDelete(DeleteBehavior.Cascade);
        builder.HasOne(s => s.CreatedByUser).WithMany().HasForeignKey(s => s.CreatedByUserId).OnDelete(DeleteBehavior.Restrict);
        builder.HasIndex(s => new { s.WorkspaceId, s.Status });
        builder.HasIndex(s => s.CreatedByUserId);
    }
}
