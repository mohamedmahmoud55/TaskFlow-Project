namespace TaskFlow.Services.Tasks.Models;

public enum TaskFailureReason
{
    None = 0,

    // The task does not exist, or the user cannot see it.
    NotFound = 1,

    // The user can see the task but may not change it.
    Forbidden = 2,

    // The submitted values were rejected.
    Invalid = 3,

    // The change could not be saved.
    Failed = 4
}

public class TaskUpdateResult
{
    public bool Succeeded { get; set; }

    public TaskFailureReason Reason { get; set; }

    public string? ErrorMessage { get; set; }

    public static TaskUpdateResult Success()
    {
        return new TaskUpdateResult
        {
            Succeeded = true
        };
    }

    public static TaskUpdateResult NotFound()
    {
        return new TaskUpdateResult
        {
            Reason = TaskFailureReason.NotFound,
            ErrorMessage = "The task could not be found."
        };
    }

    public static TaskUpdateResult Forbidden(string errorMessage)
    {
        return new TaskUpdateResult
        {
            Reason = TaskFailureReason.Forbidden,
            ErrorMessage = errorMessage
        };
    }

    public static TaskUpdateResult Invalid(string errorMessage)
    {
        return new TaskUpdateResult
        {
            Reason = TaskFailureReason.Invalid,
            ErrorMessage = errorMessage
        };
    }

    public static TaskUpdateResult Failed(string errorMessage)
    {
        return new TaskUpdateResult
        {
            Reason = TaskFailureReason.Failed,
            ErrorMessage = errorMessage
        };
    }
}
