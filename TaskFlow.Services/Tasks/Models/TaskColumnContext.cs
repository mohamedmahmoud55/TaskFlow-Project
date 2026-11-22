namespace TaskFlow.Services.Tasks.Models;

// Where a board column sits, so pages that only know a column can show
// the project and board they belong to.
public class TaskColumnContext
{
    public Guid ProjectId { get; set; }

    public Guid BoardId { get; set; }

    public string BoardName { get; set; } = string.Empty;

    public string ColumnName { get; set; } = string.Empty;
}
