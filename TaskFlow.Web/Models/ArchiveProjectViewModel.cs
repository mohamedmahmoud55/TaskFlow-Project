using System.ComponentModel.DataAnnotations;

namespace TaskFlow.Web.Models;

public sealed class ArchiveProjectViewModel
{
    [Required]
    public Guid ProjectId { get; set; }
}
