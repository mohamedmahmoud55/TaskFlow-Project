
namespace TaskFlow.Services.Boards.Models;

public class BoardColumnCreationResult
{
    public bool Succeeded { get; set; }
    public Guid? ColumnId { get; set; }
    public string? ErrorMessage { get; set; }

    public static BoardColumnCreationResult Success(Guid columnId)
    {
        return new BoardColumnCreationResult
        {
            Succeeded = true,
            ColumnId = columnId
        };
    }

    public static BoardColumnCreationResult Failure(string errorMessage)
    {
        return new BoardColumnCreationResult
        {
            Succeeded = false,
            ErrorMessage = errorMessage
        };
    }
}
