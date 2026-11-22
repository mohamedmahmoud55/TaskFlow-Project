using System.ComponentModel.DataAnnotations;

namespace TaskFlow.Web.Models;

public sealed class RestoreWorkspaceViewModel
{
    [Required]
    public Guid WorkspaceId { get; set; }
}
