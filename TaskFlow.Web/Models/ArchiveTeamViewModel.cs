using System.ComponentModel.DataAnnotations;

namespace TaskFlow.Web.Models;

public sealed class ArchiveTeamViewModel
{
    [Required]
    public Guid TeamId { get; set; }

    public Guid WorkspaceId { get; set; }
}
