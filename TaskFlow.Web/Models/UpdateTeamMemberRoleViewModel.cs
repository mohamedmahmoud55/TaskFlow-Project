using System.ComponentModel.DataAnnotations;
using TaskFlow.Data.Enums;

namespace TaskFlow.Web.Models;

public sealed class UpdateTeamMemberRoleViewModel
{
    [Required]
    public Guid TeamId { get; set; }

    [Required]
    public Guid MembershipId { get; set; }

    [Required]
    public TeamRole Role { get; set; }
}
