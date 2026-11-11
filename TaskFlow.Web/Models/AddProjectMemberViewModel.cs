
using System.ComponentModel.DataAnnotations;
using TaskFlow.Data.Enums;

namespace TaskFlow.Web.Models;

public sealed class AddProjectMemberViewModel
{
    [Required]
    public Guid ProjectId { get; set; }

    [Required]
    [EmailAddress]
    [Display(Name = "Member Email")]
    public string Email { get; set; } = string.Empty;

    [Required]
    public ProjectRole Role { get; set; } = ProjectRole.Member;
}
