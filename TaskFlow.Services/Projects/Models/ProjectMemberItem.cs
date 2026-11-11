

using TaskFlow.Data.Enums;
namespace TaskFlow.Services.Projects.Models;

public class ProjectMemberItem
{
    public Guid Id { get; set; }

    public string UserId { get; set; } = string.Empty;

    public string DisplayName { get; set; } = string.Empty;

    public string Email { get; set; } = string.Empty;

    public ProjectRole Role { get; set; }

    public bool IsActive { get; set; }

    public DateTime JoinedAt { get; set; }
}
