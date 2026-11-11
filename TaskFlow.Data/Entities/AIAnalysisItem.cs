using TaskFlow.Data.Enums;

namespace TaskFlow.Data.Entities;

public class AIAnalysisItem
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public Guid AIAnalysisSessionId { get; set; }
    public Guid TaskId { get; set; }
    public TaskPriority SuggestedPriority { get; set; }
    public int PriorityScore { get; set; }
    public string Explanation { get; set; } = string.Empty;
    public int? RecommendedOrder { get; set; }
    public string? RiskLevel { get; set; }
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

    public AIAnalysisSession AIAnalysisSession { get; set; } = null!;
    public Task Task { get; set; } = null!;
}
