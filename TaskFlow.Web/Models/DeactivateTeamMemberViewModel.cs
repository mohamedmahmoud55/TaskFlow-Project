using System.ComponentModel.DataAnnotations;

namespace TaskFlow.Web.Models;

public sealed class DeactivateTeamMemberViewModel
{
    [Required]
    public Guid TeamId { get; set; }

    [Required]
    public Guid MembershipId { get; set; }
}
