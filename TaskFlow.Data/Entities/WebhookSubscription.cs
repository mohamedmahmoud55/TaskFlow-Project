using TaskFlow.Data.Enums;

namespace TaskFlow.Data.Entities;

public class WebhookSubscription
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public Guid WorkspaceId { get; set; }
    public string CreatedByUserId { get; set; } = string.Empty;
    public string EndpointUrl { get; set; } = string.Empty;
    public string ProtectedSecret { get; set; } = string.Empty;
    public string EventTypesJson { get; set; } = "[]";
    public WebhookSubscriptionStatus Status { get; set; } = WebhookSubscriptionStatus.Active;
    public DateTime? LastDeliveryAt { get; set; }
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public DateTime? UpdatedAt { get; set; }

    public Workspace Workspace { get; set; } = null!;
    public ApplicationUser CreatedByUser { get; set; } = null!;
    public ICollection<WebhookDelivery> Deliveries { get; set; } = new List<WebhookDelivery>();
}
