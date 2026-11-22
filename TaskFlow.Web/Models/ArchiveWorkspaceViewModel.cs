using System.ComponentModel.DataAnnotations;

namespace TaskFlow.Web.Models;

public sealed class ArchiveWorkspaceViewModel
{
    [Required]
    public Guid WorkspaceId { get; set; }
}
