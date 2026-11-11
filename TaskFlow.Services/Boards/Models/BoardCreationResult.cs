
namespace TaskFlow.Services.Boards.Models;

public class BoardCreationResult
{
    public bool Succeeded { get; set; }
    public Guid? BoardId { get; set; }
    public string? ErrorMessage { get; set; }

    public static BoardCreationResult Success(Guid boardId)
    {
        return new BoardCreationResult
        {
            Succeeded = true,
            BoardId = boardId
        };
    }

    public static BoardCreationResult Failure(string errorMessage)
    {
        return new BoardCreationResult
        {
            Succeeded = false,
            ErrorMessage = errorMessage
        };
    }
}
