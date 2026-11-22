using System.ComponentModel.DataAnnotations;
using TaskFlow.Data.Enums;

namespace TaskFlow.Web.Models;

public sealed class UpdateProjectStatusViewModel
{
    [Required]
    public Guid ProjectId { get; set; }

    [Required]
    public ProjectStatus Status { get; set; }
}
