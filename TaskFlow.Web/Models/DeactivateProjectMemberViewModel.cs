
using System.ComponentModel.DataAnnotations;

namespace TaskFlow.Web.Models;

public sealed class DeactivateProjectMemberViewModel
{
    [Required]
    public Guid ProjectId { get; set; }

    [Required]
    public Guid MembershipId { get; set; }
}
