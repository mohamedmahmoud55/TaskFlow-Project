
namespace TaskFlow.Data.Enums;

public enum TaskHistoryActionType
{
    Created = 1,

    Updated = 2,

    TitleChanged = 3,

    DescriptionChanged = 4,

    PriorityChanged = 5,

    DueDateChanged = 6,

    StartDateChanged = 7,

    ColumnChanged = 8,

    AssigneeAdded = 9,

    AssigneeRemoved = 10,

    Archived = 11,

    Restored = 12,

    Completed = 13,

    Reopened = 14
}
