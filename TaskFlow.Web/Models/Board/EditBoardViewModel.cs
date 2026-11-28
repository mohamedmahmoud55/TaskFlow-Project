using System.ComponentModel.DataAnnotations;
using TaskFlow.Services.Boards.Models;

namespace TaskFlow.Web.Models.Boards;

public sealed class EditBoardViewModel
{
    [Required]
    public Guid Id { get; set; }

    [Required]
    public Guid ProjectId { get; set; }

    public Guid? TeamId { get; set; }

    [Required]
    [StringLength(150, MinimumLength = 1)]
    [Display(Name = "Board name")]
    public string Name { get; set; } = string.Empty;

    [StringLength(1000)]
    [Display(Name = "Description")]
    public string? Description { get; set; }

    [Display(Name = "Default board")]
    public bool IsDefault { get; set; }

    public bool CurrentlyDefault { get; set; }

    public bool CanArchive { get; set; }

    public IReadOnlyList<BoardListItem> OtherActiveBoards { get; set; }
        = Array.Empty<BoardListItem>();
}
