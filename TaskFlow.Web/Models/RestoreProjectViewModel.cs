using System.ComponentModel.DataAnnotations;

namespace TaskFlow.Web.Models;

public sealed class RestoreProjectViewModel
{
    [Required]
    public Guid ProjectId { get; set; }
}
