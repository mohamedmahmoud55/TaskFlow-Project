using TaskFlow.Data.Enums;

namespace TaskFlow.Data.Entities;

public class AIAnalysisSession
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public Guid WorkspaceId { get; set; }
    public string RequestedByUserId { get; set; } = string.Empty;
    public AIAnalysisStatus Status { get; set; } = AIAnalysisStatus.Pending;
    public string ModelProvider { get; set; } = string.Empty;
    public string? ModelName { get; set; }
    public string? InputSummary { get; set; }
    public string? ResultSummary { get; set; }
    public string? ErrorMessage { get; set; }
    public DateTime StartedAt { get; set; } = DateTime.UtcNow;
    public DateTime? CompletedAt { get; set; }
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

    public Workspace Workspace { get; set; } = null!;
    public ApplicationUser RequestedByUser { get; set; } = null!;
    public ICollection<AIAnalysisItem> Items { get; set; } = new List<AIAnalysisItem>();
}
