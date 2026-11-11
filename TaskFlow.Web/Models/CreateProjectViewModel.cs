using System.ComponentModel.DataAnnotations;

namespace TaskFlow.Web.Models;

public sealed class CreateProjectViewModel
{
    [Required]
    [StringLength(200)]
    [Display(Name = "Project name")]
    public string Name { get; set; } = string.Empty;

    [StringLength(2000)]
    public string? Description { get; set; }
}
