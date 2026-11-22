using System.ComponentModel.DataAnnotations;

namespace TaskFlow.Web.Models;

public sealed class RestoreTeamViewModel
{
    [Required]
    public Guid TeamId { get; set; }

    public Guid WorkspaceId { get; set; }
}
