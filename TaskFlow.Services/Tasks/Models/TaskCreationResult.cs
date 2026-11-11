
namespace TaskFlow.Services.Tasks.Models;

public class TaskCreationResult
{
    public bool Succeeded { get; set; }

    public Guid? TaskId { get; set; }

    public string? ErrorMessage { get; set; }

    public static TaskCreationResult Success(Guid taskId)
    {
        return new TaskCreationResult
        {
            Succeeded = true,
            TaskId = taskId
        };
    }

    public static TaskCreationResult Failure(string errorMessage)
    {
        return new TaskCreationResult
        {
            Succeeded = false,
            ErrorMessage = errorMessage
        };
    }
}
