
namespace TaskFlow.Services.Boards.Models;

public class BoardColumnItem
{
    public Guid Id { get; set; }
    public string Name { get; set; } = string.Empty;
    public string? Description { get; set; }
    public int Position { get; set; }
    public int? WipLimit { get; set; }
    public bool IsDefault { get; set; }
    public bool IsCompleted { get; set; }
}
