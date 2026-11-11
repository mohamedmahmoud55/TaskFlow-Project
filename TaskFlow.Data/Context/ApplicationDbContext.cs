
using Microsoft.AspNetCore.Identity.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore;
using TaskFlow.Data.Configurations;
using TaskFlow.Data.Entities;
using TaskEntity = TaskFlow.Data.Entities.Task;

namespace TaskFlow.Data.Context;

public class ApplicationDbContext : IdentityDbContext<ApplicationUser>
{
    public ApplicationDbContext(
        DbContextOptions<ApplicationDbContext> options)
        : base(options)
    {
    }

    // DbSets
    public DbSet<Workspace> Workspaces => Set<Workspace>();
    public DbSet<WorkspaceMember> WorkspaceMembers => Set<WorkspaceMember>();

    public DbSet<Team> Teams => Set<Team>();
    public DbSet<TeamMember> TeamMembers => Set<TeamMember>();

    public DbSet<Project> Projects => Set<Project>();
    public DbSet<ProjectMember> ProjectMembers => Set<ProjectMember>();

    public DbSet<Board> Boards => Set<Board>();
    public DbSet<BoardColumn> BoardColumns => Set<BoardColumn>();

    public DbSet<TaskEntity> Tasks => Set<TaskEntity>();
   
    public DbSet<TaskAssignee> TaskAssignees => Set<TaskAssignee>();
    
    public DbSet<Subtask> Subtasks => Set<Subtask>();
    public DbSet<Label> Labels => Set<Label>();
    public DbSet<TaskLabel> TaskLabels => Set<TaskLabel>();
    public DbSet<TaskDependency> TaskDependencies
     => Set<TaskDependency>();
    public DbSet<TaskComment> TaskComments => Set<TaskComment>();

    public DbSet<Attachment> Attachments => Set<Attachment>();
    public DbSet<ChecklistItem> ChecklistItems => Set<ChecklistItem>();
    public DbSet<TaskHistory> TaskHistories => Set<TaskHistory>();

    public DbSet<TimeEntry> TimeEntries => Set<TimeEntry>();
    public DbSet<TaskTimer> TaskTimers => Set<TaskTimer>();
    public DbSet<Notification> Notifications => Set<Notification>();
    public DbSet<ActivityLog> ActivityLogs => Set<ActivityLog>();
    public DbSet<TaskCommentMention> TaskCommentMentions => Set<TaskCommentMention>();
    public DbSet<TaskAIRecommendation> TaskAIRecommendations => Set<TaskAIRecommendation>();
    public DbSet<AIAnalysisSession> AIAnalysisSessions => Set<AIAnalysisSession>();
    public DbSet<AIAnalysisItem> AIAnalysisItems => Set<AIAnalysisItem>();
    public DbSet<AutomationRule> AutomationRules => Set<AutomationRule>();
    public DbSet<SavedFilter> SavedFilters => Set<SavedFilter>();
    public DbSet<CustomFieldDefinition> CustomFieldDefinitions => Set<CustomFieldDefinition>();
    public DbSet<CustomFieldValue> CustomFieldValues => Set<CustomFieldValue>();
    public DbSet<ProjectMilestone> ProjectMilestones => Set<ProjectMilestone>();
    public DbSet<WorkspaceInvitation> WorkspaceInvitations => Set<WorkspaceInvitation>();
    public DbSet<SavedView> SavedViews => Set<SavedView>();
    public DbSet<WebhookSubscription> WebhookSubscriptions => Set<WebhookSubscription>();
    public DbSet<WebhookDelivery> WebhookDeliveries => Set<WebhookDelivery>();
    // Fluent API Configurations
    protected override void OnModelCreating(ModelBuilder builder)
    {
        base.OnModelCreating(builder);

        // Automatically discover all configurations
        builder.ApplyConfigurationsFromAssembly(
            typeof(ApplicationDbContext).Assembly
        );

        // Explicitly apply configurations
        builder.ApplyConfiguration(new TeamMemberConfiguration());
        builder.ApplyConfiguration(new ProjectMemberConfiguration());
        builder.ApplyConfiguration(new BoardConfiguration());
        builder.ApplyConfiguration(new BoardColumnConfiguration());
        builder.ApplyConfiguration(new TaskConfiguration());
       
        builder.ApplyConfiguration(new TaskAssigneeConfiguration());
      
        builder.ApplyConfiguration(new TaskCommentConfiguration());
   
        builder.ApplyConfiguration(new AttachmentConfiguration());
        builder.ApplyConfiguration(new ChecklistItemConfiguration());
        builder.ApplyConfiguration(new TaskHistoryConfiguration());
        builder.ApplyConfiguration(new TimeEntryConfiguration());
    }
}
