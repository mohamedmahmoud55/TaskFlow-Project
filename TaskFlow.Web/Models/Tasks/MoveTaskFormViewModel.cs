using System.ComponentModel.DataAnnotations;

namespace TaskFlow.Web.Models.Tasks;

public sealed class MoveTaskFormViewModel
{
    [Required]
    public Guid BoardId { get; set; }

    [Required]
    public Guid TaskId { get; set; }

    [Required]
    public Guid TargetColumnId { get; set; }

    public int TargetIndex { get; set; } = int.MaxValue;
}
