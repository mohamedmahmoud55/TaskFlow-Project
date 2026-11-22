using System.ComponentModel.DataAnnotations;

namespace TaskFlow.Web.Models;

public sealed class EditWorkspaceViewModel
{
    [Required]
    public Guid Id { get; set; }

    [Required]
    [StringLength(150)]
    [Display(Name = "Workspace name")]
    public string Name { get; set; } = string.Empty;

    [StringLength(1000)]
    public string? Description { get; set; }
}
