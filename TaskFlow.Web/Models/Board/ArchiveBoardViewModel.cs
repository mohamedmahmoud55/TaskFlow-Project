using System.ComponentModel.DataAnnotations;

namespace TaskFlow.Web.Models.Boards;

public sealed class ArchiveBoardViewModel
{
    [Required]
    public Guid BoardId { get; set; }

    [Required]
    public Guid ProjectId { get; set; }

    public Guid? NewDefaultBoardId { get; set; }
}
