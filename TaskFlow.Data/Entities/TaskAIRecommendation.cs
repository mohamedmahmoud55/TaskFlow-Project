using TaskFlow.Data.Enums;

namespace TaskFlow.Data.Entities;

public class TaskAIRecommendation
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public Guid TaskId { get; set; }
    public string RequestedByUserId { get; set; } = string.Empty;
    public TaskPriority SuggestedPriority { get; set; }
    public int PriorityScore { get; set; }
    public string Explanation { get; set; } = string.Empty;
    public int? RecommendedOrder { get; set; }
    public string ModelProvider { get; set; } = string.Empty;
    public string? ModelName { get; set; }
    public bool? IsApproved { get; set; }
    public string? ApprovedByUserId { get; set; }
    public DateTime? ApprovedAt { get; set; }
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

    public Task Task { get; set; } = null!;
    public ApplicationUser RequestedByUser { get; set; } = null!;
    public ApplicationUser? ApprovedByUser { get; set; }
}
