using System.ComponentModel.DataAnnotations;
using TaskFlow.Data.Enums;

namespace TaskFlow.Web.Models;

public sealed class UpdateWorkspaceMemberRoleViewModel
{
    [Required]
    public Guid WorkspaceId { get; set; }

    [Required]
    public Guid MembershipId { get; set; }

    [Required]
    public WorkspaceRole Role { get; set; }
}
