using System.ComponentModel.DataAnnotations;
using TaskFlow.Data.Enums;

namespace TaskFlow.Web.Models;

public sealed class AddTeamMemberViewModel
{
    [Required]
    public Guid TeamId { get; set; }

    [Required]
    public string UserId { get; set; } = string.Empty;

    [Required]
    public TeamRole Role { get; set; } = TeamRole.Member;
}
