using System.Data.Common;
using Microsoft.Data.SqlClient;
using Microsoft.EntityFrameworkCore;
using TaskFlow.Data.Context;
using TaskFlow.Data.Entities;
using TaskFlow.Data.Enums;
using TaskFlow.Services.Boards;
using TaskFlow.Services.Projects;
using TaskFlow.Services.Tasks;
using AsyncTask = System.Threading.Tasks.Task;
using TaskEntity = TaskFlow.Data.Entities.Task;

namespace TaskFlow.Services.Tests;

/// <summary>
/// Editing, changing the status of, archiving and restoring a project.
/// Every test seeds the same world inside a transaction that is rolled back
/// afterwards:
///
///   One project with a board, a column and a task.
///   Manager, Member and Viewer belong to the project.
///   WsOwner and WsAdmin administer the workspace but are not members.
///   Outsider has an account but belongs to nothing.
/// </summary>
[Collection(SqlServerIntegrationCollection.Name)]
public sealed class ProjectManagementIntegrationTests
{
    private readonly SqlServerIntegrationFixture fixture;

    public ProjectManagementIntegrationTests(SqlServerIntegrationFixture fixture)
    {
        this.fixture = fixture;
    }

    // ------------------------------------------------------------------
    // Editing
    // ------------------------------------------------------------------

    [Fact]
    [Trait("Category", "Integration")]
    public AsyncTask ManagersAndWorkspaceAdminsCanEditAProject() => RunAsync(async s =>
    {
        foreach (var (userId, name, description) in new[]
        {
            (s.ManagerId, "  Renamed by manager  ", (string?)"  New description  "),
            (s.WsOwnerId, "Renamed by owner", (string?)null),
            (s.WsAdminId, "Renamed by admin", (string?)"   ")
        })
        {
            var result = await s.NewService().UpdateProjectAsync(
                s.ProjectId, userId, name, description);

            Assert.True(result.Succeeded, result.Error);

            var project = await s.LoadProjectAsync();

            Assert.Equal(name.Trim(), project.Name);
            Assert.Equal(
                string.IsNullOrWhiteSpace(description) ? null : description.Trim(),
                project.Description);
            Assert.NotNull(project.UpdatedAt);
        }
    });

    [Fact]
    [Trait("Category", "Integration")]
    public AsyncTask MembersViewersAndOutsidersCannotEditAProject() => RunAsync(async s =>
    {
        foreach (var userId in new[] { s.MemberId, s.ViewerId })
        {
            var result = await s.NewService().UpdateProjectAsync(
                s.ProjectId, userId, "Hijacked", "Hijacked");

            Assert.False(result.Succeeded);
            Assert.Contains("permission", result.Error);
        }

        var outsider = await s.NewService().UpdateProjectAsync(
            s.ProjectId, s.OutsiderId, "Hijacked", "Hijacked");

        Assert.False(outsider.Succeeded);
        Assert.Contains("not found", outsider.Error);

        var project = await s.LoadProjectAsync();

        Assert.Equal("Original name", project.Name);
        Assert.Equal("Original description", project.Description);
    });

    [Fact]
    [Trait("Category", "Integration")]
    public AsyncTask EditRejectsInvalidNamesAndDescriptions() => RunAsync(async s =>
    {
        foreach (var (name, description) in new[]
        {
            ("   ", (string?)null),
            (new string('x', 201), (string?)null),
            ("Valid name", (string?)new string('y', 2001))
        })
        {
            var result = await s.NewService().UpdateProjectAsync(
                s.ProjectId, s.ManagerId, name, description);

            Assert.False(result.Succeeded);
        }

        var project = await s.LoadProjectAsync();

        Assert.Equal("Original name", project.Name);
        Assert.Equal("Original description", project.Description);
    });

    [Fact]
    [Trait("Category", "Integration")]
    public AsyncTask CreatingAndEditingShareTheSameValidation() => RunAsync(async s =>
    {
        var tooLong = new string('x', 201);

        var createBad = await s.NewService().CreateProjectAsync(
            s.TeamId, s.WorkspaceId, s.WsAdminId, tooLong, null);

        var editBad = await s.NewService().UpdateProjectAsync(
            s.ProjectId, s.ManagerId, tooLong, null);

        Assert.False(createBad.Succeeded);
        Assert.False(editBad.Succeeded);
        Assert.Equal(createBad.Error, editBad.Error);

        var createOk = await s.NewService().CreateProjectAsync(
            s.TeamId, s.WorkspaceId, s.WsAdminId, "  Brand new project  ", "  A description  ");

        Assert.True(createOk.Succeeded, createOk.Error);

        var created = await s.LoadProjectAsync(createOk.ProjectId!.Value);

        Assert.Equal("Brand new project", created.Name);
        Assert.Equal("A description", created.Description);
        Assert.Equal(ProjectStatus.Planned, created.Status);
    });

    // ------------------------------------------------------------------
    // Status
    // ------------------------------------------------------------------

    [Fact]
    [Trait("Category", "Integration")]
    public AsyncTask ManagersCanSetAnyDefinedStatusAndNobodyElseCan() => RunAsync(async s =>
    {
        foreach (var status in Enum.GetValues<ProjectStatus>())
        {
            var result = await s.NewService().UpdateProjectStatusAsync(
                s.ProjectId, s.ManagerId, status);

            Assert.True(result.Succeeded, result.Error);
            Assert.Equal(status, (await s.LoadProjectAsync()).Status);
        }

        var byAdmin = await s.NewService().UpdateProjectStatusAsync(
            s.ProjectId, s.WsAdminId, ProjectStatus.Active);

        Assert.True(byAdmin.Succeeded, byAdmin.Error);

        Assert.False((await s.NewService().UpdateProjectStatusAsync(
            s.ProjectId, s.ManagerId, (ProjectStatus)99)).Succeeded);

        foreach (var userId in new[] { s.MemberId, s.ViewerId, s.OutsiderId })
        {
            var result = await s.NewService().UpdateProjectStatusAsync(
                s.ProjectId, userId, ProjectStatus.Completed);

            Assert.False(result.Succeeded);
        }

        Assert.Equal(ProjectStatus.Active, (await s.LoadProjectAsync()).Status);
    });

    // ------------------------------------------------------------------
    // Archiving and restoring
    // ------------------------------------------------------------------

    [Fact]
    [Trait("Category", "Integration")]
    public AsyncTask ArchivingHidesTheProjectItsBoardsAndItsTasksFromEveryone() => RunAsync(async s =>
    {
        Assert.NotNull(await s.NewService().GetProjectAsync(s.ProjectId, null, s.ManagerId));
        Assert.Single(await s.NewBoardService().GetBoardsAsync(s.ProjectId, null, s.ManagerId));
        Assert.Single(await s.NewTaskService().GetTasksAsync(s.ColumnId, s.ManagerId));

        var archived = await s.NewService().ArchiveProjectAsync(s.ProjectId, s.ManagerId);

        Assert.True(archived.Succeeded, archived.Error);
        Assert.True((await s.LoadProjectAsync()).IsArchived);

        foreach (var userId in new[] { s.ManagerId, s.MemberId, s.ViewerId, s.WsAdminId, s.WsOwnerId })
        {
            Assert.Null(await s.NewService().GetProjectAsync(s.ProjectId, null, userId));
            Assert.DoesNotContain(
                await s.NewService().GetMyProjectsAsync(userId),
                project => project.Id == s.ProjectId);
        }

        Assert.Empty(await s.NewService().GetProjectsAsync(s.TeamId, null, s.WsAdminId));
        Assert.Empty(await s.NewBoardService().GetBoardsAsync(s.ProjectId, null, s.ManagerId));
        Assert.Null(await s.NewBoardService().GetBoardAsync(s.BoardId, null, s.ManagerId));
        Assert.Empty(await s.NewTaskService().GetTasksAsync(s.ColumnId, s.ManagerId));
    });

    [Fact]
    [Trait("Category", "Integration")]
    public AsyncTask OnlyManagersAndWorkspaceAdminsCanArchive() => RunAsync(async s =>
    {
        foreach (var userId in new[] { s.MemberId, s.ViewerId, s.OutsiderId })
        {
            var result = await s.NewService().ArchiveProjectAsync(s.ProjectId, userId);

            Assert.False(result.Succeeded);
        }

        Assert.False((await s.LoadProjectAsync()).IsArchived);

        var byAdmin = await s.NewService().ArchiveProjectAsync(s.ProjectId, s.WsAdminId);

        Assert.True(byAdmin.Succeeded, byAdmin.Error);
    });

    [Fact]
    [Trait("Category", "Integration")]
    public AsyncTask ArchivedProjectsCannotBeEditedOrChangedUntilRestored() => RunAsync(async s =>
    {
        Assert.True((await s.NewService().ArchiveProjectAsync(s.ProjectId, s.ManagerId)).Succeeded);

        var edit = await s.NewService().UpdateProjectAsync(
            s.ProjectId, s.ManagerId, "Edited while archived", null);

        var status = await s.NewService().UpdateProjectStatusAsync(
            s.ProjectId, s.ManagerId, ProjectStatus.Completed);

        Assert.False(edit.Succeeded);
        Assert.False(status.Succeeded);

        var project = await s.LoadProjectAsync();

        Assert.Equal("Original name", project.Name);
        Assert.Equal(ProjectStatus.Planned, project.Status);
    });

    [Fact]
    [Trait("Category", "Integration")]
    public AsyncTask OnlyManagersAndWorkspaceAdminsSeeAndCanRestoreArchivedProjects() => RunAsync(async s =>
    {
        Assert.True((await s.NewService().ArchiveProjectAsync(s.ProjectId, s.ManagerId)).Succeeded);

        foreach (var userId in new[] { s.ManagerId, s.WsAdminId, s.WsOwnerId })
        {
            var archived = await s.NewService().GetMyArchivedProjectsAsync(userId);

            Assert.Equal(s.ProjectId, Assert.Single(archived).Id);
        }

        foreach (var userId in new[] { s.MemberId, s.ViewerId, s.OutsiderId })
        {
            Assert.Empty(await s.NewService().GetMyArchivedProjectsAsync(userId));

            var result = await s.NewService().RestoreProjectAsync(s.ProjectId, userId);

            Assert.False(result.Succeeded);
        }

        Assert.True((await s.LoadProjectAsync()).IsArchived);

        var restored = await s.NewService().RestoreProjectAsync(s.ProjectId, s.ManagerId);

        Assert.True(restored.Succeeded, restored.Error);
        Assert.False((await s.LoadProjectAsync()).IsArchived);

        Assert.NotNull(await s.NewService().GetProjectAsync(s.ProjectId, null, s.MemberId));
        Assert.Contains(
            await s.NewService().GetMyProjectsAsync(s.ManagerId),
            project => project.Id == s.ProjectId);
        Assert.Empty(await s.NewService().GetMyArchivedProjectsAsync(s.ManagerId));
        Assert.Single(await s.NewTaskService().GetTasksAsync(s.ColumnId, s.ManagerId));
    });

    [Fact]
    [Trait("Category", "Integration")]
    public AsyncTask AProjectInAnArchivedTeamCannotBeRestored() => RunAsync(async s =>
    {
        Assert.True((await s.NewService().ArchiveProjectAsync(s.ProjectId, s.ManagerId)).Succeeded);

        var context = s.NewContext();
        var team = await context.Teams.SingleAsync(candidate => candidate.Id == s.TeamId);

        team.IsArchived = true;

        await context.SaveChangesAsync();

        var result = await s.NewService().RestoreProjectAsync(s.ProjectId, s.WsAdminId);

        Assert.False(result.Succeeded);
        Assert.Empty(await s.NewService().GetMyArchivedProjectsAsync(s.WsAdminId));
        Assert.True((await s.LoadProjectAsync()).IsArchived);
    });

    // ------------------------------------------------------------------
    // The flag the page uses
    // ------------------------------------------------------------------

    [Fact]
    [Trait("Category", "Integration")]
    public AsyncTask OnlyManagersAndWorkspaceAdminsCanManageAProject() => RunAsync(async s =>
    {
        foreach (var userId in new[] { s.ManagerId, s.WsOwnerId, s.WsAdminId })
        {
            Assert.True(await s.NewService().CanManageProjectAsync(s.ProjectId, userId));
        }

        foreach (var userId in new[] { s.MemberId, s.ViewerId, s.OutsiderId })
        {
            Assert.False(await s.NewService().CanManageProjectAsync(s.ProjectId, userId));
        }

        Assert.False(await s.NewService().CanManageProjectAsync(Guid.NewGuid(), s.ManagerId));
    });

    // ------------------------------------------------------------------
    // Test plumbing
    // ------------------------------------------------------------------

    private async AsyncTask RunAsync(Func<ProjectScenario, AsyncTask> test)
    {
        await using var connection = fixture.CreateConnection();
        await connection.OpenAsync();

        var transaction = await connection.BeginTransactionAsync();
        Exception? originalException = null;

        try
        {
            await using var scenario = await ProjectScenario.CreateAsync(
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

    private sealed class ProjectScenario : IAsyncDisposable
    {
        private readonly SqlServerIntegrationFixture fixture;
        private readonly SqlConnection connection;
        private readonly DbTransaction transaction;
        private readonly List<ApplicationDbContext> contexts = new();

        private ProjectScenario(
            SqlServerIntegrationFixture fixture,
            SqlConnection connection,
            DbTransaction transaction)
        {
            this.fixture = fixture;
            this.connection = connection;
            this.transaction = transaction;
        }

        public Guid WorkspaceId { get; } = Guid.NewGuid();
        public Guid TeamId { get; } = Guid.NewGuid();
        public Guid ProjectId { get; } = Guid.NewGuid();
        public Guid BoardId { get; } = Guid.NewGuid();
        public Guid ColumnId { get; } = Guid.NewGuid();

        public string WsOwnerId { get; } = NewUserId("ws-owner");
        public string WsAdminId { get; } = NewUserId("ws-admin");
        public string ManagerId { get; } = NewUserId("manager");
        public string MemberId { get; } = NewUserId("member");
        public string ViewerId { get; } = NewUserId("viewer");
        public string OutsiderId { get; } = NewUserId("outsider");

        public static async System.Threading.Tasks.Task<ProjectScenario> CreateAsync(
            SqlServerIntegrationFixture fixture,
            SqlConnection connection,
            DbTransaction transaction)
        {
            var scenario = new ProjectScenario(fixture, connection, transaction);

            await scenario.SeedAsync();

            return scenario;
        }

        // A fresh context per call, so tracked entities from one call can
        // never hide a problem in the next.
        public ApplicationDbContext NewContext()
        {
            var context = fixture.CreateContext(connection);

            context.Database.UseTransaction(transaction);
            contexts.Add(context);

            return context;
        }

        public ProjectService NewService() => new(NewContext());

        public BoardService NewBoardService() => new(NewContext());

        public TaskService NewTaskService() => new(NewContext());

        public System.Threading.Tasks.Task<Project> LoadProjectAsync() => LoadProjectAsync(ProjectId);

        public async System.Threading.Tasks.Task<Project> LoadProjectAsync(Guid projectId)
        {
            return await NewContext().Projects
                .AsNoTracking()
                .SingleAsync(project => project.Id == projectId);
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

            context.AddRange(
                User(WsOwnerId),
                User(WsAdminId),
                User(ManagerId),
                User(MemberId),
                User(ViewerId),
                User(OutsiderId),

                new Workspace { Id = WorkspaceId, Name = "Project management workspace" },
                new Team { Id = TeamId, WorkspaceId = WorkspaceId, Name = "Project management team" },

                new WorkspaceMember { WorkspaceId = WorkspaceId, UserId = WsOwnerId, Role = WorkspaceRole.Owner, IsActive = true },
                new WorkspaceMember { WorkspaceId = WorkspaceId, UserId = WsAdminId, Role = WorkspaceRole.Admin, IsActive = true },

                new Project
                {
                    Id = ProjectId,
                    TeamId = TeamId,
                    Name = "Original name",
                    Description = "Original description",
                    Status = ProjectStatus.Planned
                },

                Membership(ManagerId, ProjectRole.Manager),
                Membership(MemberId, ProjectRole.Member),
                Membership(ViewerId, ProjectRole.Viewer),

                new Board { Id = BoardId, ProjectId = ProjectId, Name = "Board", IsDefault = true },
                new BoardColumn { Id = ColumnId, BoardId = BoardId, Name = "To Do", Position = 1, IsDefault = true },

                new TaskEntity
                {
                    Id = Guid.NewGuid(),
                    BoardColumnId = ColumnId,
                    CreatedByUserId = ManagerId,
                    Title = "Seeded task",
                    Priority = TaskPriority.Medium,
                    Position = 1
                });

            await context.SaveChangesAsync();
        }

        private ProjectMember Membership(string userId, ProjectRole role) => new()
        {
            ProjectId = ProjectId,
            UserId = userId,
            Role = role,
            IsActive = true
        };

        private static string NewUserId(string label) => $"pm-{label}-{Guid.NewGuid():N}";

        private static ApplicationUser User(string id) => new()
        {
            Id = id,
            UserName = $"{id}@taskflow.test",
            NormalizedUserName = $"{id}@taskflow.test".ToUpperInvariant(),
            Email = $"{id}@taskflow.test",
            NormalizedEmail = $"{id}@taskflow.test".ToUpperInvariant(),
            FirstName = "Project",
            LastName = "Tester"
        };
    }
}
