using System.Data.Common;
using Microsoft.Data.SqlClient;
using Microsoft.EntityFrameworkCore;
using TaskFlow.Data.Context;
using TaskFlow.Data.Entities;
using TaskFlow.Data.Enums;
using TaskFlow.Services.Projects;
using TaskFlow.Services.Tasks;
using TaskFlow.Services.Tasks.Assignments;
using TaskFlow.Services.Tasks.Models;
using AsyncTask = System.Threading.Tasks.Task;
using TaskEntity = TaskFlow.Data.Entities.Task;

namespace TaskFlow.Services.Tests;

/// <summary>
/// Multi-user permission tests for task creation, editing, archiving,
/// deleting and assignment.
///
/// Every test seeds the same small world inside a transaction that is rolled
/// back afterwards:
///
///   Project A (team 1): Manager, Member, Viewer and an inactive Member;
///                       three tasks, one created by each of Manager,
///                       Member and Viewer.
///   Workspace Admin:    not a project member, but administers the workspace.
///   Outsider:           a member of Project B (team 2) only.
///   Stranger:           belongs to nothing.
/// </summary>
[Collection(SqlServerIntegrationCollection.Name)]
public sealed class TaskPermissionIntegrationTests
{
    private readonly SqlServerIntegrationFixture fixture;

    public TaskPermissionIntegrationTests(SqlServerIntegrationFixture fixture)
    {
        this.fixture = fixture;
    }

    // ------------------------------------------------------------------
    // Creating tasks
    // ------------------------------------------------------------------

    [Fact]
    [Trait("Category", "Integration")]
    public AsyncTask ViewerCannotCreateTasks() => RunAsync(async s =>
    {
        var result = await s.NewTaskService().CreateTaskAsync(
            s.ColumnId, s.ViewerId, "Created by viewer",
            null, TaskPriority.Medium, null, null, null);

        Assert.False(result.Succeeded);
        Assert.False(await s.TaskExistsAsync("Created by viewer"));
    });

    [Fact]
    [Trait("Category", "Integration")]
    public AsyncTask MembersManagersAndWorkspaceAdminsCanCreateTasks() => RunAsync(async s =>
    {
        foreach (var (userId, title) in new[]
        {
            (s.MemberId, "Created by member"),
            (s.ManagerId, "Created by manager"),
            (s.AdminId, "Created by workspace admin")
        })
        {
            var result = await s.NewTaskService().CreateTaskAsync(
                s.ColumnId, userId, title,
                null, TaskPriority.Medium, null, null, null);

            Assert.True(result.Succeeded, $"{title}: {result.ErrorMessage}");
            Assert.True(await s.TaskExistsAsync(title));
        }
    });

    [Fact]
    [Trait("Category", "Integration")]
    public AsyncTask OutsidersAndInactiveMembersCannotCreateTasks() => RunAsync(async s =>
    {
        foreach (var userId in new[] { s.OutsiderId, s.InactiveId, s.StrangerId })
        {
            var result = await s.NewTaskService().CreateTaskAsync(
                s.ColumnId, userId, "Should not exist",
                null, TaskPriority.Medium, null, null, null);

            Assert.False(result.Succeeded);
        }

        Assert.False(await s.TaskExistsAsync("Should not exist"));
    });

    [Fact]
    [Trait("Category", "Integration")]
    public AsyncTask CreateRejectsInvalidInputWithAMessage() => RunAsync(async s =>
    {
        var emptyTitle = await s.NewTaskService().CreateTaskAsync(
            s.ColumnId, s.ManagerId, "   ",
            null, TaskPriority.Medium, null, null, null);

        Assert.False(emptyTitle.Succeeded);
        Assert.False(string.IsNullOrWhiteSpace(emptyTitle.ErrorMessage));

        var dueBeforeStart = await s.NewTaskService().CreateTaskAsync(
            s.ColumnId, s.ManagerId, "Bad dates",
            null, TaskPriority.Medium,
            new DateTime(2026, 12, 10), new DateTime(2026, 12, 1), null);

        Assert.False(dueBeforeStart.Succeeded);
        Assert.Contains("Due date", dueBeforeStart.ErrorMessage);
        Assert.False(await s.TaskExistsAsync("Bad dates"));
    });

    // ------------------------------------------------------------------
    // Reading tasks
    // ------------------------------------------------------------------

    [Fact]
    [Trait("Category", "Integration")]
    public AsyncTask OnlyProjectMembersAndWorkspaceAdminsCanReadTasks() => RunAsync(async s =>
    {
        foreach (var userId in new[] { s.ViewerId, s.MemberId, s.ManagerId, s.AdminId })
        {
            var tasks = await s.NewTaskService().GetTasksAsync(s.ColumnId, userId);

            Assert.Equal(3, tasks.Count);
            Assert.NotNull(await s.NewTaskService().GetTaskAsync(s.ManagerTaskId, null, userId));
        }

        foreach (var userId in new[] { s.OutsiderId, s.InactiveId, s.StrangerId })
        {
            Assert.Empty(await s.NewTaskService().GetTasksAsync(s.ColumnId, userId));
            Assert.Null(await s.NewTaskService().GetTaskAsync(s.ManagerTaskId, null, userId));
        }
    });

    // ------------------------------------------------------------------
    // Editing tasks
    // ------------------------------------------------------------------

    [Fact]
    [Trait("Category", "Integration")]
    public AsyncTask MembersCanEditOnlyTheirOwnTasks() => RunAsync(async s =>
    {
        var own = await s.NewTaskService().UpdateTaskAsync(
            s.MemberTaskId, s.MemberId, "Member edit",
            null, TaskPriority.High, null, null, null);

        Assert.True(own.Succeeded, own.ErrorMessage);
        Assert.Equal("Member edit", (await s.LoadTaskAsync(s.MemberTaskId)).Title);

        var others = await s.NewTaskService().UpdateTaskAsync(
            s.ManagerTaskId, s.MemberId, "Hijacked",
            null, TaskPriority.High, null, null, null);

        Assert.False(others.Succeeded);
        Assert.Equal(TaskFailureReason.Forbidden, others.Reason);
        Assert.Equal("Manager task", (await s.LoadTaskAsync(s.ManagerTaskId)).Title);
    });

    [Fact]
    [Trait("Category", "Integration")]
    public AsyncTask ManagersAndWorkspaceAdminsCanEditAnyTask() => RunAsync(async s =>
    {
        foreach (var (userId, title) in new[]
        {
            (s.ManagerId, "Edited by manager"),
            (s.AdminId, "Edited by workspace admin")
        })
        {
            var result = await s.NewTaskService().UpdateTaskAsync(
                s.MemberTaskId, userId, title,
                null, TaskPriority.Low, null, null, null);

            Assert.True(result.Succeeded, result.ErrorMessage);
            Assert.Equal(title, (await s.LoadTaskAsync(s.MemberTaskId)).Title);
        }
    });

    [Fact]
    [Trait("Category", "Integration")]
    public AsyncTask ViewerCannotEditEvenTheirOwnTask() => RunAsync(async s =>
    {
        var result = await s.NewTaskService().UpdateTaskAsync(
            s.ViewerTaskId, s.ViewerId, "Viewer edit",
            null, TaskPriority.High, null, null, null);

        Assert.False(result.Succeeded);
        Assert.Equal(TaskFailureReason.Forbidden, result.Reason);
        Assert.Equal("Viewer task", (await s.LoadTaskAsync(s.ViewerTaskId)).Title);
    });

    [Fact]
    [Trait("Category", "Integration")]
    public AsyncTask UpdateReportsNotFoundForUsersWhoCannotSeeTheTask() => RunAsync(async s =>
    {
        foreach (var userId in new[] { s.OutsiderId, s.InactiveId, s.StrangerId })
        {
            var result = await s.NewTaskService().UpdateTaskAsync(
                s.ManagerTaskId, userId, "Should not change",
                null, TaskPriority.High, null, null, null);

            Assert.False(result.Succeeded);
            Assert.Equal(TaskFailureReason.NotFound, result.Reason);
        }

        var missing = await s.NewTaskService().UpdateTaskAsync(
            Guid.NewGuid(), s.ManagerId, "Missing task",
            null, TaskPriority.High, null, null, null);

        Assert.Equal(TaskFailureReason.NotFound, missing.Reason);
        Assert.Equal("Manager task", (await s.LoadTaskAsync(s.ManagerTaskId)).Title);
    });

    [Fact]
    [Trait("Category", "Integration")]
    public AsyncTask UpdateReportsInvalidInputInsteadOfNotFound() => RunAsync(async s =>
    {
        var dueBeforeStart = await s.NewTaskService().UpdateTaskAsync(
            s.ManagerTaskId, s.ManagerId, "Valid title",
            null, TaskPriority.Medium,
            new DateTime(2026, 12, 10), new DateTime(2026, 12, 1), null);

        Assert.False(dueBeforeStart.Succeeded);
        Assert.Equal(TaskFailureReason.Invalid, dueBeforeStart.Reason);
        Assert.Contains("Due date", dueBeforeStart.ErrorMessage);

        var emptyTitle = await s.NewTaskService().UpdateTaskAsync(
            s.ManagerTaskId, s.ManagerId, "   ",
            null, TaskPriority.Medium, null, null, null);

        Assert.Equal(TaskFailureReason.Invalid, emptyTitle.Reason);
        Assert.Equal("Manager task", (await s.LoadTaskAsync(s.ManagerTaskId)).Title);
    });

    // ------------------------------------------------------------------
    // Archiving and deleting tasks
    // ------------------------------------------------------------------

    [Fact]
    [Trait("Category", "Integration")]
    public AsyncTask ViewerCannotArchiveOrDeleteTasks() => RunAsync(async s =>
    {
        Assert.False(await s.NewTaskService().ArchiveTaskAsync(s.ViewerTaskId, s.ViewerId));
        Assert.False(await s.NewTaskService().DeleteTaskAsync(s.ViewerTaskId, s.ViewerId));

        var task = await s.LoadTaskAsync(s.ViewerTaskId);

        Assert.False(task.IsArchived);
    });

    [Fact]
    [Trait("Category", "Integration")]
    public AsyncTask MembersCanArchiveOnlyTheirOwnTasks() => RunAsync(async s =>
    {
        Assert.True(await s.NewTaskService().ArchiveTaskAsync(s.MemberTaskId, s.MemberId));
        Assert.True((await s.LoadTaskAsync(s.MemberTaskId)).IsArchived);

        Assert.False(await s.NewTaskService().ArchiveTaskAsync(s.ManagerTaskId, s.MemberId));
        Assert.False((await s.LoadTaskAsync(s.ManagerTaskId)).IsArchived);
    });

    [Fact]
    [Trait("Category", "Integration")]
    public AsyncTask OnlyManagersAndWorkspaceAdminsCanPhysicallyDeleteTasks() => RunAsync(async s =>
    {
        // Members cannot physically delete even their own tasks.
        Assert.False(await s.NewTaskService().DeleteTaskAsync(s.MemberTaskId, s.MemberId));
        Assert.True(await s.TaskExistsAsync("Member task"));

        // Inactive, outsider, and stranger cannot delete.
        foreach (var userId in new[] { s.InactiveId, s.OutsiderId, s.StrangerId })
        {
            Assert.False(await s.NewTaskService().DeleteTaskAsync(s.ViewerTaskId, userId));
        }

        // Manager and Workspace Admin can delete.
        Assert.True(await s.NewTaskService().DeleteTaskAsync(s.ViewerTaskId, s.ManagerId));
        Assert.False(await s.TaskExistsAsync("Viewer task"));

        Assert.True(await s.NewTaskService().DeleteTaskAsync(s.MemberTaskId, s.AdminId));
        Assert.False(await s.TaskExistsAsync("Member task"));
    });

    // ------------------------------------------------------------------
    // Assigning tasks
    // ------------------------------------------------------------------

    [Fact]
    [Trait("Category", "Integration")]
    public AsyncTask OnlyManagersAndWorkspaceAdminsCanAssignMembers() => RunAsync(async s =>
    {
        Assert.False(await s.NewAssignmentService().UpdateAssigneesAsync(
            s.ManagerTaskId, s.MemberId, new[] { s.MemberId }));
        Assert.Empty(await s.AssigneeIdsAsync(s.ManagerTaskId));

        Assert.True(await s.NewAssignmentService().UpdateAssigneesAsync(
            s.ManagerTaskId, s.ManagerId, new[] { s.MemberId }));
        Assert.Equal(s.MemberId, Assert.Single(await s.AssigneeIdsAsync(s.ManagerTaskId)));

        Assert.True(await s.NewAssignmentService().UpdateAssigneesAsync(
            s.ManagerTaskId, s.AdminId, new[] { s.MemberId, s.ManagerId }));
        Assert.Equal(2, (await s.AssigneeIdsAsync(s.ManagerTaskId)).Count);
    });

    [Fact]
    [Trait("Category", "Integration")]
    public AsyncTask ViewersOutsidersAndInactiveMembersCannotBeAssigned() => RunAsync(async s =>
    {
        foreach (var candidate in new[] { s.ViewerId, s.OutsiderId, s.InactiveId })
        {
            Assert.False(await s.NewAssignmentService().UpdateAssigneesAsync(
                s.ManagerTaskId, s.ManagerId, new[] { candidate }));
        }

        Assert.Empty(await s.AssigneeIdsAsync(s.ManagerTaskId));
    });

    [Fact]
    [Trait("Category", "Integration")]
    public AsyncTask AssignmentDetailsFollowTheSameAccessRules() => RunAsync(async s =>
    {
        Assert.Null(await s.NewAssignmentService().GetAssignmentDetailsAsync(
            s.ManagerTaskId, s.OutsiderId));
        Assert.Null(await s.NewAssignmentService().GetAssignmentDetailsAsync(
            s.ManagerTaskId, s.StrangerId));
        Assert.Null(await s.NewAssignmentService().GetAssignmentDetailsAsync(
            s.ManagerTaskId, s.InactiveId));

        var viewerView = await s.NewAssignmentService().GetAssignmentDetailsAsync(
            s.ManagerTaskId, s.ViewerId);

        Assert.NotNull(viewerView);
        Assert.False(viewerView.CanManageAssignments);

        var managerView = await s.NewAssignmentService().GetAssignmentDetailsAsync(
            s.ManagerTaskId, s.ManagerId);

        Assert.NotNull(managerView);
        Assert.True(managerView.CanManageAssignments);
        Assert.Contains(managerView.Members, member => member.UserId == s.MemberId);
        Assert.DoesNotContain(managerView.Members, member => member.UserId == s.ViewerId);

        var adminView = await s.NewAssignmentService().GetAssignmentDetailsAsync(
            s.ManagerTaskId, s.AdminId);

        Assert.NotNull(adminView);
        Assert.True(adminView.CanManageAssignments);
    });

    // ------------------------------------------------------------------
    // What the UI is told (CanCreateTasks and CanEdit)
    // ------------------------------------------------------------------

    [Fact]
    [Trait("Category", "Integration")]
    public AsyncTask CanCreateTasksAgreesWithWhatCreateActuallyAllows() => RunAsync(async s =>
    {
        var everyone = new[]
        {
            s.ViewerId, s.MemberId, s.ManagerId, s.AdminId,
            s.OutsiderId, s.InactiveId, s.StrangerId
        };

        foreach (var userId in everyone)
        {
            var flag = await s.NewTaskService().CanCreateTasksAsync(s.ColumnId, userId);

            var result = await s.NewTaskService().CreateTaskAsync(
                s.ColumnId, userId, $"Parity {userId}",
                null, TaskPriority.Medium, null, null, null);

            Assert.Equal(flag, result.Succeeded);
        }

        Assert.False(await s.NewTaskService().CanCreateTasksAsync(Guid.NewGuid(), s.ManagerId));
    });

    [Fact]
    [Trait("Category", "Integration")]
    public AsyncTask TaskListFlagsShowWhichTasksTheUserCanEdit() => RunAsync(async s =>
    {
        foreach (var userId in new[] { s.ManagerId, s.AdminId })
        {
            var tasks = await s.NewTaskService().GetTasksAsync(s.ColumnId, userId);

            Assert.Equal(3, tasks.Count);
            Assert.All(tasks, task => Assert.True(task.CanEdit));
        }

        var memberTasks = await s.NewTaskService().GetTasksAsync(s.ColumnId, s.MemberId);

        Assert.Equal(3, memberTasks.Count);
        Assert.Equal(s.MemberTaskId, Assert.Single(memberTasks, task => task.CanEdit).Id);

        var viewerTasks = await s.NewTaskService().GetTasksAsync(s.ColumnId, s.ViewerId);

        Assert.Equal(3, viewerTasks.Count);
        Assert.All(viewerTasks, task => Assert.False(task.CanEdit));
    });

    [Fact]
    [Trait("Category", "Integration")]
    public AsyncTask TaskDetailsFlagShowsWhetherTheUserCanEdit() => RunAsync(async s =>
    {
        var managerView = await s.NewTaskService().GetTaskAsync(s.MemberTaskId, null, s.ManagerId);

        Assert.NotNull(managerView);
        Assert.True(managerView.CanEdit);
        Assert.NotEqual(Guid.Empty, managerView.ProjectId);

        var ownTask = await s.NewTaskService().GetTaskAsync(s.MemberTaskId, null, s.MemberId);
        var othersTask = await s.NewTaskService().GetTaskAsync(s.ManagerTaskId, null, s.MemberId);

        Assert.NotNull(ownTask);
        Assert.NotNull(othersTask);
        Assert.True(ownTask.CanEdit);
        Assert.False(othersTask.CanEdit);

        var viewersOwnTask = await s.NewTaskService().GetTaskAsync(s.ViewerTaskId, null, s.ViewerId);

        Assert.NotNull(viewersOwnTask);
        Assert.False(viewersOwnTask.CanEdit);
    });

    [Fact]
    [Trait("Category", "Integration")]
    public AsyncTask EditFlagsAgreeWithWhatUpdateActuallyAllows() => RunAsync(async s =>
    {
        var users = new[] { s.ViewerId, s.MemberId, s.ManagerId, s.AdminId };
        var tasks = new[] { s.ManagerTaskId, s.MemberTaskId, s.ViewerTaskId };

        foreach (var userId in users)
        {
            foreach (var taskId in tasks)
            {
                var details = await s.NewTaskService().GetTaskAsync(taskId, null, userId);

                Assert.NotNull(details);

                var result = await s.NewTaskService().UpdateTaskAsync(
                    taskId, userId, "Parity check",
                    null, TaskPriority.Medium, null, null, null);

                Assert.Equal(details.CanEdit, result.Succeeded);
            }
        }
    });

    // ------------------------------------------------------------------
    // Navigation context
    // ------------------------------------------------------------------

    [Fact]
    [Trait("Category", "Integration")]
    public AsyncTask ColumnContextIsOnlyReturnedToUsersWhoCanRead() => RunAsync(async s =>
    {
        var task = await s.NewTaskService().GetTaskAsync(s.ManagerTaskId, null, s.ManagerId);

        Assert.NotNull(task);

        foreach (var userId in new[] { s.ViewerId, s.MemberId, s.ManagerId, s.AdminId })
        {
            var context = await s.NewTaskService().GetColumnContextAsync(s.ColumnId, userId);

            Assert.NotNull(context);
            Assert.Equal(task.ProjectId, context.ProjectId);
            Assert.Equal(task.BoardId, context.BoardId);
            Assert.Equal("Board A", context.BoardName);
            Assert.Equal("To Do", context.ColumnName);
        }

        foreach (var userId in new[] { s.OutsiderId, s.InactiveId, s.StrangerId })
        {
            Assert.Null(await s.NewTaskService().GetColumnContextAsync(s.ColumnId, userId));
        }

        Assert.Null(await s.NewTaskService().GetColumnContextAsync(Guid.NewGuid(), s.ManagerId));
    });

    // ------------------------------------------------------------------
    // My Projects
    // ------------------------------------------------------------------

    [Fact]
    [Trait("Category", "Integration")]
    public AsyncTask MyProjectsListsOnlyProjectsTheUserCanOpen() => RunAsync(async s =>
    {
        foreach (var (userId, role) in new[]
        {
            (s.ManagerId, ProjectRole.Manager),
            (s.MemberId, ProjectRole.Member),
            (s.ViewerId, ProjectRole.Viewer)
        })
        {
            var projects = await s.NewProjectService().GetMyProjectsAsync(userId);

            var project = Assert.Single(projects);

            Assert.Equal("Project A", project.Name);
            Assert.Equal("Team A", project.TeamName);
            Assert.Equal(role, project.CurrentUserRole);
        }

        var outsiderProjects = await s.NewProjectService().GetMyProjectsAsync(s.OutsiderId);

        Assert.Equal("Project B", Assert.Single(outsiderProjects).Name);

        foreach (var userId in new[] { s.InactiveId, s.StrangerId })
        {
            Assert.Empty(await s.NewProjectService().GetMyProjectsAsync(userId));
        }
    });

    [Fact]
    [Trait("Category", "Integration")]
    public AsyncTask MyProjectsShowsEveryProjectToAWorkspaceAdmin() => RunAsync(async s =>
    {
        var projects = await s.NewProjectService().GetMyProjectsAsync(s.AdminId);

        Assert.Equal(new[] { "Project A", "Project B" }, projects.Select(project => project.Name).ToArray());
        Assert.All(projects, project => Assert.Null(project.CurrentUserRole));
    });

    // ------------------------------------------------------------------
    // Test plumbing
    // ------------------------------------------------------------------

    private async AsyncTask RunAsync(Func<PermissionScenario, AsyncTask> test)
    {
        await using var connection = fixture.CreateConnection();
        await connection.OpenAsync();

        var transaction = await connection.BeginTransactionAsync();
        Exception? originalException = null;

        try
        {
            await using var scenario = await PermissionScenario.CreateAsync(
                fixture, connection, transaction);

            await test(scenario);
        }
        catch (Exception exception)
        {
            originalException = exception;
            throw;
        }
        finally
        {
            await Stage3TestData.CleanupAsync(transaction, originalException);
        }
    }

    private sealed class PermissionScenario : IAsyncDisposable
    {
        private readonly SqlServerIntegrationFixture fixture;
        private readonly SqlConnection connection;
        private readonly DbTransaction transaction;
        private readonly List<ApplicationDbContext> contexts = new();

        private PermissionScenario(
            SqlServerIntegrationFixture fixture,
            SqlConnection connection,
            DbTransaction transaction)
        {
            this.fixture = fixture;
            this.connection = connection;
            this.transaction = transaction;
        }

        public string ManagerId { get; } = NewUserId("manager");
        public string MemberId { get; } = NewUserId("member");
        public string ViewerId { get; } = NewUserId("viewer");
        public string InactiveId { get; } = NewUserId("inactive");
        public string AdminId { get; } = NewUserId("admin");
        public string OutsiderId { get; } = NewUserId("outsider");
        public string StrangerId { get; } = NewUserId("stranger");

        public Guid ColumnId { get; } = Guid.NewGuid();
        public Guid ManagerTaskId { get; } = Guid.NewGuid();
        public Guid MemberTaskId { get; } = Guid.NewGuid();
        public Guid ViewerTaskId { get; } = Guid.NewGuid();

        public static async System.Threading.Tasks.Task<PermissionScenario> CreateAsync(
            SqlServerIntegrationFixture fixture,
            SqlConnection connection,
            DbTransaction transaction)
        {
            var scenario = new PermissionScenario(fixture, connection, transaction);

            await scenario.SeedAsync();

            return scenario;
        }

        // A fresh context per service call, so one call's tracked entities
        // can never hide a problem in the next call.
        public ApplicationDbContext NewContext()
        {
            var context = fixture.CreateContext(connection);

            context.Database.UseTransaction(transaction);
            contexts.Add(context);

            return context;
        }

        public TaskService NewTaskService() => new(NewContext());

        public TaskAssignmentService NewAssignmentService() => new(NewContext());

        public ProjectService NewProjectService() => new(NewContext());

        public async System.Threading.Tasks.Task<TaskEntity> LoadTaskAsync(Guid taskId)
        {
            return await NewContext().Tasks
                .AsNoTracking()
                .SingleAsync(task => task.Id == taskId);
        }

        public async System.Threading.Tasks.Task<bool> TaskExistsAsync(string title)
        {
            return await NewContext().Tasks
                .AsNoTracking()
                .AnyAsync(task => task.Title == title);
        }

        public async System.Threading.Tasks.Task<List<string>> AssigneeIdsAsync(Guid taskId)
        {
            return await NewContext().TaskAssignees
                .AsNoTracking()
                .Where(assignee => assignee.TaskId == taskId)
                .Select(assignee => assignee.UserId)
                .ToListAsync();
        }

        public async ValueTask DisposeAsync()
        {
            foreach (var context in contexts)
            {
                await context.DisposeAsync();
            }
        }

        private async AsyncTask SeedAsync()
        {
            await using var context = fixture.CreateContext(connection);

            context.Database.UseTransaction(transaction);

            var workspaceId = Guid.NewGuid();
            var teamId = Guid.NewGuid();
            var otherTeamId = Guid.NewGuid();
            var projectId = Guid.NewGuid();
            var otherProjectId = Guid.NewGuid();
            var boardId = Guid.NewGuid();
            var otherBoardId = Guid.NewGuid();
            var otherColumnId = Guid.NewGuid();

            context.AddRange(
                User(ManagerId),
                User(MemberId),
                User(ViewerId),
                User(InactiveId),
                User(AdminId),
                User(OutsiderId),
                User(StrangerId),

                new Workspace { Id = workspaceId, Name = "Permission workspace" },
                new Team { Id = teamId, WorkspaceId = workspaceId, Name = "Team A" },
                new Team { Id = otherTeamId, WorkspaceId = workspaceId, Name = "Team B" },

                new Project { Id = projectId, TeamId = teamId, Name = "Project A", Status = ProjectStatus.Active },
                new Project { Id = otherProjectId, TeamId = otherTeamId, Name = "Project B", Status = ProjectStatus.Active },

                new Board { Id = boardId, ProjectId = projectId, Name = "Board A", IsDefault = true },
                new Board { Id = otherBoardId, ProjectId = otherProjectId, Name = "Board B", IsDefault = true },

                new BoardColumn { Id = ColumnId, BoardId = boardId, Name = "To Do", Position = 1, IsDefault = true },
                new BoardColumn { Id = otherColumnId, BoardId = otherBoardId, Name = "To Do", Position = 1, IsDefault = true },

                new WorkspaceMember { WorkspaceId = workspaceId, UserId = AdminId, Role = WorkspaceRole.Admin },

                Membership(projectId, ManagerId, ProjectRole.Manager, isActive: true),
                Membership(projectId, MemberId, ProjectRole.Member, isActive: true),
                Membership(projectId, ViewerId, ProjectRole.Viewer, isActive: true),
                Membership(projectId, InactiveId, ProjectRole.Member, isActive: false),
                Membership(otherProjectId, OutsiderId, ProjectRole.Manager, isActive: true),

                SeedTask(ManagerTaskId, ColumnId, ManagerId, "Manager task", position: 1),
                SeedTask(MemberTaskId, ColumnId, MemberId, "Member task", position: 2),
                SeedTask(ViewerTaskId, ColumnId, ViewerId, "Viewer task", position: 3));

            await context.SaveChangesAsync();
        }

        private static string NewUserId(string label) => $"perm-{label}-{Guid.NewGuid():N}";

        private static ApplicationUser User(string id) => new()
        {
            Id = id,
            UserName = $"{id}@taskflow.test",
            NormalizedUserName = $"{id}@taskflow.test".ToUpperInvariant(),
            Email = $"{id}@taskflow.test",
            NormalizedEmail = $"{id}@taskflow.test".ToUpperInvariant(),
            FirstName = "Perm",
            LastName = "Tester"
        };

        private static ProjectMember Membership(
            Guid projectId,
            string userId,
            ProjectRole role,
            bool isActive) => new()
        {
            ProjectId = projectId,
            UserId = userId,
            Role = role,
            IsActive = isActive
        };

        private static TaskEntity SeedTask(
            Guid id,
            Guid columnId,
            string createdByUserId,
            string title,
            int position) => new()
        {
            Id = id,
            BoardColumnId = columnId,
            CreatedByUserId = createdByUserId,
            Title = title,
            Priority = TaskPriority.Medium,
            Position = position
        };
    }
}
