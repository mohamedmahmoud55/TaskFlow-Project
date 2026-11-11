namespace TaskFlow.Data.Entities;

public class WebhookDelivery
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public Guid WebhookSubscriptionId { get; set; }
    public string EventType { get; set; } = string.Empty;
    public DateTime AttemptedAt { get; set; } = DateTime.UtcNow;
    public int? ResponseStatusCode { get; set; }
    public string? ResponseBody { get; set; }
    public bool Succeeded { get; set; }
    public string? ErrorMessage { get; set; }
    public DateTime? CompletedAt { get; set; }

    public WebhookSubscription WebhookSubscription { get; set; } = null!;
}
