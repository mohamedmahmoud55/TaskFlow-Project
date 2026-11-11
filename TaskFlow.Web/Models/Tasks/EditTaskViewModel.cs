
using System.ComponentModel.DataAnnotations;
using TaskFlow.Data.Enums;

namespace TaskFlow.Web.Models.Tasks;

public class EditTaskViewModel
{
    public Guid Id { get; set; }

    public Guid BoardColumnId { get; set; }

    [Required]
    [StringLength(200, MinimumLength = 1)]
    public string Title { get; set; } = string.Empty;

    [StringLength(5000)]
    public string? Description { get; set; }

    public TaskPriority Priority { get; set; }

    public DateTime? StartDate { get; set; }

    public DateTime? DueDate { get; set; }

    [Range(1, int.MaxValue)]
    public int? EstimatedMinutes { get; set; }
}
