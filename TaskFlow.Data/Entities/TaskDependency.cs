
using TaskFlow.Data.Enums;

namespace TaskFlow.Data.Entities;

public class TaskDependency
{
    public Guid Id { get; set; }

    // The prerequisite task
    public Guid PredecessorTaskId { get; set; }

    // The dependent task
    public Guid SuccessorTaskId { get; set; }

    public DependencyType DependencyType { get; set; }

    // Positive = delay, negative = lead
    public int LagMinutes { get; set; }

    public DateTime CreatedAt { get; set; }

    // Navigation Properties
    public Task PredecessorTask { get; set; } = null!;

    public Task SuccessorTask { get; set; } = null!;
}
