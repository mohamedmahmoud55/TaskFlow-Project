
using System.ComponentModel.DataAnnotations;

namespace TaskFlow.Web.Models.Boards;

public class CreateBoardColumnViewModel
{
    public Guid BoardId { get; set; }

    [Required]
    [StringLength(100, MinimumLength = 1)]
    [Display(Name = "Column name")]
    public string Name { get; set; } = string.Empty;

    [StringLength(500)]
    [Display(Name = "Description")]
    public string? Description { get; set; }

    [Range(1, int.MaxValue)]
    [Display(Name = "Work in progress limit")]
    public int? WipLimit { get; set; }

    [Display(Name = "Completed column")]
    public bool IsCompleted { get; set; }
}
