
using Microsoft.AspNetCore.Identity;

namespace TaskFlow.Data.Entities;

public class ApplicationUser : IdentityUser
{
    public string FirstName { get; set; } = string.Empty;

    public string LastName { get; set; } = string.Empty;

    public string? ProfilePictureUrl { get; set; }

    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

    public bool IsActive { get; set; } = true;

    // Workspace Memberships
    public ICollection<WorkspaceMember> WorkspaceMemberships { get; set; }
        = new List<WorkspaceMember>();

    // Team Memberships
    public ICollection<TeamMember> TeamMemberships { get; set; }
        = new List<TeamMember>();

    // Project Memberships
    public ICollection<ProjectMember> ProjectMemberships { get; set; }
        = new List<ProjectMember>();

    // Tasks created by this user
    public ICollection<Task> CreatedTasks { get; set; }
        = new List<Task>();

    // Tasks assigned to this user
    public ICollection<TaskAssignee> AssignedTasks { get; set; }
        = new List<TaskAssignee>();

    // Assignments created by this user
    public ICollection<TaskAssignee> AssignedTasksByMe { get; set; }
        = new List<TaskAssignee>();

    // Comments written by this user
    public ICollection<TaskComment> Comments { get; set; }
        = new List<TaskComment>();

    // Comments deleted by this user
    public ICollection<TaskComment> DeletedComments { get; set; }
        = new List<TaskComment>();
    public ICollection<Attachment> UploadedAttachments { get; set; }
        = new List<Attachment>();
    // Task History Actions
    public ICollection<TaskHistory> TaskHistories { get; set; }
        = new List<TaskHistory>();
    // Time Entries
    public ICollection<TimeEntry> TimeEntries { get; set; }
        = new List<TimeEntry>();

    public ICollection<TaskTimer> TaskTimers { get; set; }
        = new List<TaskTimer>();

    public ICollection<Notification> Notifications { get; set; }
        = new List<Notification>();

    public ICollection<ActivityLog> ActivityLogs { get; set; }
        = new List<ActivityLog>();

    public ICollection<TaskCommentMention> Mentions { get; set; }
        = new List<TaskCommentMention>();

    public ICollection<TaskAIRecommendation> AIRecommendationsRequested { get; set; }
        = new List<TaskAIRecommendation>();

    public ICollection<TaskAIRecommendation> AIRecommendationsApproved { get; set; }
        = new List<TaskAIRecommendation>();

    public ICollection<AIAnalysisSession> AIAnalysisSessions { get; set; }
        = new List<AIAnalysisSession>();

    public ICollection<SavedFilter> SavedFilters { get; set; }
        = new List<SavedFilter>();

    public ICollection<SavedView> SavedViews { get; set; }
        = new List<SavedView>();
}
