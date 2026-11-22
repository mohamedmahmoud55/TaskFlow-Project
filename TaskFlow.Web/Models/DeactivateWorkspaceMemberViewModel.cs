using System.ComponentModel.DataAnnotations;

namespace TaskFlow.Web.Models;

public sealed class DeactivateWorkspaceMemberViewModel
{
    [Required]
    public Guid WorkspaceId { get; set; }

    [Required]
    public Guid MembershipId { get; set; }
}
