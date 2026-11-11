
using System.ComponentModel.DataAnnotations;
using TaskFlow.Data.Enums;

namespace TaskFlow.Web.Models;

public sealed class UpdateProjectMemberRoleViewModel
{
    [Required]
    public Guid ProjectId { get; set; }

    [Required]
    public Guid MembershipId { get; set; }

    [Required]
    public ProjectRole Role { get; set; }
}
