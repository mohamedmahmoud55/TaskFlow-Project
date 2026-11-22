using System.ComponentModel.DataAnnotations;

namespace TaskFlow.Web.Models;

public sealed class EditTeamViewModel
{
    [Required]
    public Guid Id { get; set; }

    public Guid WorkspaceId { get; set; }

    [Required]
    [StringLength(150)]
    [Display(Name = "Team name")]
    public string Name { get; set; } = string.Empty;

    [StringLength(1000)]
    public string? Description { get; set; }
}
