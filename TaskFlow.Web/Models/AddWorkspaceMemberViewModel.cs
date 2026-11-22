using System.ComponentModel.DataAnnotations;
using TaskFlow.Data.Enums;

namespace TaskFlow.Web.Models;

public sealed class AddWorkspaceMemberViewModel
{
    [Required]
    public Guid WorkspaceId { get; set; }

    [Required]
    [EmailAddress]
    [Display(Name = "Member Email")]
    public string Email { get; set; } = string.Empty;

    [Required]
    public WorkspaceRole Role { get; set; } = WorkspaceRole.Member;
}
