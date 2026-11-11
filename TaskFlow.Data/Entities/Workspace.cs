
namespace TaskFlow.Data.Entities;

public class Workspace
{
    public Guid Id { get; set; } = Guid.NewGuid();

    public string Name { get; set; } = string.Empty;

    public string? Description { get; set; }

    public string? LogoUrl { get; set; }

    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

    public DateTime? UpdatedAt { get; set; }

    public bool IsArchived { get; set; } = false;
    public ICollection<WorkspaceMember> Members { get; set; }
    = new List<WorkspaceMember>();
    public ICollection<Team> Teams { get; set; }
    = new List<Team>();
    public ICollection<Label> Labels { get; set; }
    = new List<Label>();

    public ICollection<AIAnalysisSession> AIAnalysisSessions { get; set; }
        = new List<AIAnalysisSession>();
}
