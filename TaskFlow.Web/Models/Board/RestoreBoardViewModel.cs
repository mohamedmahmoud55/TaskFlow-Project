using System.ComponentModel.DataAnnotations;

namespace TaskFlow.Web.Models.Boards;

public sealed class RestoreBoardViewModel
{
    [Required]
    public Guid BoardId { get; set; }

    [Required]
    public Guid ProjectId { get; set; }
}
