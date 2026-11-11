
using System;
using System.ComponentModel.DataAnnotations;

namespace TaskFlow.Web.Models.Boards;

public class CreateBoardViewModel
{
    [Required]
    [StringLength(150, MinimumLength = 1)]
    [Display(Name = "Board name")]
    public string Name { get; set; } = string.Empty;

    [StringLength(1000)]
    [Display(Name = "Description")]
    public string? Description { get; set; }

    [Display(Name = "Set as default board")]
    public bool IsDefault { get; set; }

    public Guid ProjectId { get; set; }

    public Guid? TeamId { get; set; }
}
