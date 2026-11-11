
namespace TaskFlow.Services.Boards.Models;

public class BoardListItem
{
    public Guid Id { get; set; }
    public string Name { get; set; } = string.Empty;
    public string? Description { get; set; }
    public bool IsDefault { get; set; }
    public DateTime CreatedAt { get; set; }
    public int ColumnCount { get; set; }
}
