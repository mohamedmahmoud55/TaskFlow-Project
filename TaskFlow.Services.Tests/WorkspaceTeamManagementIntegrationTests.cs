using System.Data.Common;
using Microsoft.Data.SqlClient;
using Microsoft.EntityFrameworkCore;
using TaskFlow.Data.Context;
using TaskFlow.Data.Entities;
using TaskFlow.Data.Enums;
using TaskFlow.Services.Boards;
using TaskFlow.Services.Projects;
using TaskFlow.Services.Tasks;
using TaskFlow.Services.Teams;
using TaskFlow.Services.Teams.Models;
using TaskFlow.Services.Workspaces;
using AsyncTask = System.Threading.Tasks.Task;
using TaskEntity = TaskFlow.Data.Entities.Task;

namespace TaskFlow.Services.Tests;

/// <summary>
/// Creating, listing, editing, archiving and restoring workspaces and teams.
/// Every test seeds the same world inside a transaction that is rolled back
/// afterwards:
///
///   Owner and Admin administer the workspace.
///   Lead leads the team; TeamMember is a Member of the team and of one
///   project inside it.
///   PlainMember and Viewer belong to the workspace only.
///   Outsider has an account but belongs to nothing.
/// </summary>
[Collection(SqlServerIntegrationCollection.Name)]
public sealed class WorkspaceTeamManagementIntegrationTests
{
    private readonly SqlServerIntegrationFixture fixture;

    public WorkspaceTeamManagementIntegrationTests(SqlServerIntegrationFixture fixture)
    {
        this.fixture = fixture;
    }

    // ------------------------------------------------------------------
    // The rules themselves
    // ------------------------------------------------------------------

    [Fact]
    public void OnlyOwnersAndAdminsEditAndOnlyOwnersArchiveWorkspaces()
    {
        Assert.True(WorkspaceRules.CanEdit(WorkspaceRole.Owner));
        Assert.True(WorkspaceRules.CanEdit(WorkspaceRole.Admin));
        Assert.False(WorkspaceRules.CanEdit(WorkspaceRole.Member));
        Assert.False(WorkspaceRules.CanEdit(WorkspaceRole.Viewer));

        Assert.True(WorkspaceRules.CanArchive(WorkspaceRole.Owner));
        Assert.False(WorkspaceRules.CanArchive(WorkspaceRole.Admin));
        Assert.False(WorkspaceRules.CanArchive(WorkspaceRole.Member));
        Assert.False(WorkspaceRules.CanArchive(WorkspaceRole.Viewer));
    }

    [Fact]
    public void AdminsAndLeadsEditTeamsButOnlyAdminsArchiveThem()
    {
        Assert.True(TeamRules.CanEdit(true, null));
        Assert.True(TeamRules.CanEdit(true, TeamRole.Member));
        Assert.True(TeamRules.CanEdit(false, TeamRole.Lead));
        Assert.False(TeamRules.CanEdit(false, TeamRole.Member));
        Assert.False(TeamRules.CanEdit(false, TeamRole.Viewer));
        Assert.False(TeamRules.CanEdit(false, null));

        Assert.True(TeamRules.CanArchive(true));
        Assert.False(TeamRules.CanArchive(false));
    }

    // ------------------------------------------------------------------
    // Creating and listing (closes the service test gap)
    // ------------------------------------------------------------------

    [Fact]
    [Trait("Category", "Integration")]
    public AsyncTask CreatingAWorkspaceMakesTheCreatorTheOwner() => RunAsync(async s =>
    {
        var created = await s.NewWorkspaceService().CreateWorkspaceAsync(
            s.OutsiderId, "  New workspace  ", "  A description  ");

        Assert.True(created.Succeeded, created.Error);

        var mine = await s.NewWorkspaceService().GetUserWorkspacesAsync(s.OutsiderId);
        var workspace = Assert.Single(mine);

        Assert.Equal(created.WorkspaceId, workspace.Id);
        Assert.Equal("New workspace", workspace.Name);
        Assert.Equal("A description", workspace.Description);
        Assert.Equal(WorkspaceRole.Owner, workspace.Role);

        Assert.False((await s.NewWorkspaceService().CreateWorkspaceAsync(
            s.OutsiderId, "   ", null)).Succeeded);

        Assert.False((await s.NewWorkspaceService().CreateWorkspaceAsync(
            "no-such-user", "Valid name", null)).Succeeded);
    });

    [Fact]
    [Trait("Category", "Integration")]
    public AsyncTask OnlyWorkspaceOwnersAndAdminsCanCreateTeamsAndTheCreatorBecomesLead() => RunAsync(async s =>
    {
        var byOwner = await s.NewTeamService().CreateTeamAsync(
            s.WorkspaceId, s.OwnerId, "  Owner team  ", null);

        var byAdmin = await s.NewTeamService().CreateTeamAsync(
            s.WorkspaceId, s.AdminId, "Admin team", "  About  ");

        Assert.True(byOwner.Succeeded, byOwner.Error);
        Assert.True(byAdmin.Succeeded, byAdmin.Error);

        var ownerTeam = await s.NewTeamService().GetTeamAsync(byOwner.TeamId!.Value, null, s.OwnerId);

        Assert.NotNull(ownerTeam);
        Assert.Equal("Owner team", ownerTeam.Name);
        Assert.Equal(TeamRole.Lead, ownerTeam.CurrentUserRole);

        foreach (var userId in new[] { s.LeadId, s.PlainMemberId, s.ViewerId, s.OutsiderId })
        {
            var result = await s.NewTeamService().CreateTeamAsync(
                s.WorkspaceId, userId, "Not allowed", null);

            Assert.False(result.Succeeded);
            Assert.Contains("permission", result.Error);
        }
    });

    [Fact]
    [Trait("Category", "Integration")]
    public AsyncTask TeamListsShowEachPersonOnlyWhatTheyMaySee() => RunAsync(async s =>
    {
        foreach (var userId in new[] { s.OwnerId, s.AdminId })
        {
            var teams = await s.NewTeamService().GetTeamsAsync(s.WorkspaceId, userId);

            Assert.Equal(s.TeamId, Assert.Single(teams).Id);
        }

        var leadTeams = await s.NewTeamService().GetTeamsAsync(s.WorkspaceId, s.LeadId);

        Assert.Equal(TeamRole.Lead, Assert.Single(leadTeams).CurrentUserRole);

        var memberTeams = await s.NewTeamService().GetTeamsAsync(s.WorkspaceId, s.TeamMemberId);

        Assert.Equal(TeamRole.Member, Assert.Single(memberTeams).CurrentUserRole);

        foreach (var userId in new[] { s.PlainMemberId, s.ViewerId, s.OutsiderId })
        {
            Assert.Empty(await s.NewTeamService().GetTeamsAsync(s.WorkspaceId, userId));
        }
    });

    // ------------------------------------------------------------------
    // Editing a workspace
    // ------------------------------------------------------------------

    [Fact]
    [Trait("Category", "Integration")]
    public AsyncTask OwnersAndAdminsCanEditAWorkspace() => RunAsync(async s =>
    {
        var byOwner = await s.NewWorkspaceService().UpdateWorkspaceAsync(
            s.WorkspaceId, s.OwnerId, "  Renamed by owner  ", "  New description  ");

        Assert.True(byOwner.Succeeded, byOwner.Error);

        var afterOwner = await s.LoadWorkspaceAsync();

        Assert.Equal("Renamed by owner", afterOwner.Name);
        Assert.Equal("New description", afterOwner.Description);
        Assert.NotNull(afterOwner.UpdatedAt);

        var byAdmin = await s.NewWorkspaceService().UpdateWorkspaceAsync(
            s.WorkspaceId, s.AdminId, "Renamed by admin", "   ");

        Assert.True(byAdmin.Succeeded, byAdmin.Error);

        var afterAdmin = await s.LoadWorkspaceAsync();

        Assert.Equal("Renamed by admin", afterAdmin.Name);
        Assert.Null(afterAdmin.Description);
    });

    [Fact]
    [Trait("Category", "Integration")]
    public AsyncTask OtherRolesCannotEditAWorkspace() => RunAsync(async s =>
    {
        foreach (var userId in new[] { s.LeadId, s.PlainMemberId, s.ViewerId })
        {
            var result = await s.NewWorkspaceService().UpdateWorkspaceAsync(
                s.WorkspaceId, userId, "Hijacked", "Hijacked");

            Assert.False(result.Succeeded);
            Assert.Contains("permission", result.Error);
        }

        var outsider = await s.NewWorkspaceService().UpdateWorkspaceAsync(
            s.WorkspaceId, s.OutsiderId, "Hijacked", "Hijacked");

        Assert.False(outsider.Succeeded);
        Assert.Contains("not found", outsider.Error);

        var workspace = await s.LoadWorkspaceAsync();

        Assert.Equal("Original workspace", workspace.Name);
        Assert.Equal("Original workspace description", workspace.Description);
    });

    [Fact]
    [Trait("Category", "Integration")]
    public AsyncTask CreatingAndEditingAWorkspaceShareTheSameValidation() => RunAsync(async s =>
    {
        var tooLong = new string('x', 151);

        var createBad = await s.NewWorkspaceService().CreateWorkspaceAsync(
            s.OutsiderId, tooLong, null);

        var editBad = await s.NewWorkspaceService().UpdateWorkspaceAsync(
            s.WorkspaceId, s.OwnerId, tooLong, null);

        Assert.False(createBad.Succeeded);
        Assert.False(editBad.Succeeded);
        Assert.Equal(createBad.Error, editBad.Error);

        var longDescription = new string('y', 1001);

        Assert.False((await s.NewWorkspaceService().UpdateWorkspaceAsync(
            s.WorkspaceId, s.OwnerId, "Valid", longDescription)).Succeeded);

        Assert.False((await s.NewWorkspaceService().UpdateWorkspaceAsync(
            s.WorkspaceId, s.OwnerId, "   ", null)).Succeeded);

        Assert.Equal("Original workspace", (await s.LoadWorkspaceAsync()).Name);
    });

    // ------------------------------------------------------------------
    // Archiving and restoring a workspace
    // ------------------------------------------------------------------

    [Fact]
    [Trait("Category", "Integration")]
    public AsyncTask OnlyTheOwnerCanArchiveAWorkspace() => RunAsync(async s =>
    {
        foreach (var userId in new[] { s.AdminId, s.LeadId, s.PlainMemberId, s.ViewerId, s.OutsiderId })
        {
            var result = await s.NewWorkspaceService().ArchiveWorkspaceAsync(s.WorkspaceId, userId);

            Assert.False(result.Succeeded);
        }

        Assert.False((await s.LoadWorkspaceAsync()).IsArchived);

        var byOwner = await s.NewWorkspaceService().ArchiveWorkspaceAsync(s.WorkspaceId, s.OwnerId);

        Assert.True(byOwner.Succeeded, byOwner.Error);
        Assert.True((await s.LoadWorkspaceAsync()).IsArchived);
    });

    [Fact]
    [Trait("Category", "Integration")]
    public AsyncTask ArchivingAWorkspaceHidesEverythingInsideItFromEveryone() => RunAsync(async s =>
    {
        Assert.NotNull(await s.NewTeamService().GetTeamAsync(s.TeamId, null, s.LeadId));
        Assert.NotNull(await s.NewProjectService().GetProjectAsync(s.ProjectId, null, s.TeamMemberId));
        Assert.Single(await s.NewTaskService().GetTasksAsync(s.ColumnId, s.TeamMemberId));

        Assert.True((await s.NewWorkspaceService().ArchiveWorkspaceAsync(s.WorkspaceId, s.OwnerId)).Succeeded);

        foreach (var userId in new[] { s.OwnerId, s.AdminId, s.LeadId, s.TeamMemberId, s.PlainMemberId })
        {
            Assert.DoesNotContain(
                await s.NewWorkspaceService().GetUserWorkspacesAsync(userId),
                workspace => workspace.Id == s.WorkspaceId);

            Assert.Null(await s.NewWorkspaceService().GetWorkspaceAsync(s.WorkspaceId, userId));
            Assert.Empty(await s.NewTeamService().GetTeamsAsync(s.WorkspaceId, userId));
            Assert.Null(await s.NewTeamService().GetTeamAsync(s.TeamId, null, userId));
            Assert.Null(await s.NewProjectService().GetProjectAsync(s.ProjectId, null, userId));
            Assert.DoesNotContain(
                await s.NewProjectService().GetMyProjectsAsync(userId),
                project => project.Id == s.ProjectId);
        }

        Assert.Empty(await s.NewBoardService().GetBoardsAsync(s.ProjectId, null, s.TeamMemberId));
        Assert.Empty(await s.NewTaskService().GetTasksAsync(s.ColumnId, s.TeamMemberId));
    });

    [Fact]
    [Trait("Category", "Integration")]
    public AsyncTask OnlyTheOwnerSeesAndCanRestoreAnArchivedWorkspace() => RunAsync(async s =>
    {
        Assert.True((await s.NewWorkspaceService().ArchiveWorkspaceAsync(s.WorkspaceId, s.OwnerId)).Succeeded);

        var archived = await s.NewWorkspaceService().GetArchivedWorkspacesAsync(s.OwnerId);

        Assert.Equal(s.WorkspaceId, Assert.Single(archived).Id);

        foreach (var userId in new[] { s.AdminId, s.LeadId, s.PlainMemberId, s.ViewerId, s.OutsiderId })
        {
            Assert.Empty(await s.NewWorkspaceService().GetArchivedWorkspacesAsync(userId));

            var result = await s.NewWorkspaceService().RestoreWorkspaceAsync(s.WorkspaceId, userId);

            Assert.False(result.Succeeded);
        }

        Assert.True((await s.LoadWorkspaceAsync()).IsArchived);

        var restored = await s.NewWorkspaceService().RestoreWorkspaceAsync(s.WorkspaceId, s.OwnerId);

        Assert.True(restored.Succeeded, restored.Error);
        Assert.False((await s.LoadWorkspaceAsync()).IsArchived);

        Assert.Contains(
            await s.NewWorkspaceService().GetUserWorkspacesAsync(s.OwnerId),
            workspace => workspace.Id == s.WorkspaceId);
        Assert.NotNull(await s.NewTeamService().GetTeamAsync(s.TeamId, null, s.LeadId));
        Assert.NotNull(await s.NewProjectService().GetProjectAsync(s.ProjectId, null, s.TeamMemberId));
        Assert.Empty(await s.NewWorkspaceService().GetArchivedWorkspacesAsync(s.OwnerId));
    });

    [Fact]
    [Trait("Category", "Integration")]
    public AsyncTask AnArchivedWorkspaceCannotBeEditedOrArchivedAgain() => RunAsync(async s =>
    {
        Assert.True((await s.NewWorkspaceService().ArchiveWorkspaceAsync(s.WorkspaceId, s.OwnerId)).Succeeded);

        var edit = await s.NewWorkspaceService().UpdateWorkspaceAsync(
            s.WorkspaceId, s.OwnerId, "Edited while archived", null);

        var archiveAgain = await s.NewWorkspaceService().ArchiveWorkspaceAsync(s.WorkspaceId, s.OwnerId);

        Assert.False(edit.Succeeded);
        Assert.False(archiveAgain.Succeeded);
        Assert.Equal("Original workspace", (await s.LoadWorkspaceAsync()).Name);
    });

    // ------------------------------------------------------------------
    // Editing a team
    // ------------------------------------------------------------------

    [Fact]
    [Trait("Category", "Integration")]
    public AsyncTask WorkspaceAdminsAndTeamLeadsCanEditATeam() => RunAsync(async s =>
    {
        foreach (var (userId, name) in new[]
        {
            (s.OwnerId, "  Renamed by owner  "),
            (s.AdminId, "Renamed by admin"),
            (s.LeadId, "Renamed by lead")
        })
        {
            var result = await s.NewTeamService().UpdateTeamAsync(
                s.TeamId, userId, name, "  New description  ");

            Assert.True(result.Succeeded, result.Error);

            var team = await s.LoadTeamAsync();

            Assert.Equal(name.Trim(), team.Name);
            Assert.Equal("New description", team.Description);
            Assert.NotNull(team.UpdatedAt);
        }
    });

    [Fact]
    [Trait("Category", "Integration")]
    public AsyncTask TeamMembersAndPeopleOutsideTheTeamCannotEditIt() => RunAsync(async s =>
    {
        var teamMember = await s.NewTeamService().UpdateTeamAsync(
            s.TeamId, s.TeamMemberId, "Hijacked", "Hijacked");

        Assert.False(teamMember.Succeeded);
        Assert.Contains("permission", teamMember.Error);

        foreach (var userId in new[] { s.PlainMemberId, s.ViewerId, s.OutsiderId })
        {
            var result = await s.NewTeamService().UpdateTeamAsync(
                s.TeamId, userId, "Hijacked", "Hijacked");

            Assert.False(result.Succeeded);
            Assert.Contains("not found", result.Error);
        }

        var team = await s.LoadTeamAsync();

        Assert.Equal("Original team", team.Name);
        Assert.Equal("Original team description", team.Description);
    });

    [Fact]
    [Trait("Category", "Integration")]
    public AsyncTask CreatingAndEditingATeamShareTheSameValidation() => RunAsync(async s =>
    {
        var tooLong = new string('x', 151);

        var createBad = await s.NewTeamService().CreateTeamAsync(
            s.WorkspaceId, s.AdminId, tooLong, null);

        var editBad = await s.NewTeamService().UpdateTeamAsync(
            s.TeamId, s.LeadId, tooLong, null);

        Assert.False(createBad.Succeeded);
        Assert.False(editBad.Succeeded);
        Assert.Equal(createBad.Error, editBad.Error);

        Assert.False((await s.NewTeamService().UpdateTeamAsync(
            s.TeamId, s.LeadId, "Valid", new string('y', 1001))).Succeeded);

        Assert.False((await s.NewTeamService().UpdateTeamAsync(
            s.TeamId, s.LeadId, "   ", null)).Succeeded);

        Assert.Equal("Original team", (await s.LoadTeamAsync()).Name);
    });

    // ------------------------------------------------------------------
    // Archiving and restoring a team
    // ------------------------------------------------------------------

    [Fact]
    [Trait("Category", "Integration")]
    public AsyncTask OnlyWorkspaceOwnersAndAdminsCanArchiveATeam() => RunAsync(async s =>
    {
        var byLead = await s.NewTeamService().ArchiveTeamAsync(s.TeamId, s.LeadId);

        Assert.False(byLead.Succeeded);
        Assert.Contains("workspace Owner or Admin", byLead.Error);

        foreach (var userId in new[] { s.TeamMemberId, s.PlainMemberId, s.ViewerId, s.OutsiderId })
        {
            Assert.False((await s.NewTeamService().ArchiveTeamAsync(s.TeamId, userId)).Succeeded);
        }

        Assert.False((await s.LoadTeamAsync()).IsArchived);

        var byAdmin = await s.NewTeamService().ArchiveTeamAsync(s.TeamId, s.AdminId);

        Assert.True(byAdmin.Succeeded, byAdmin.Error);
        Assert.True((await s.LoadTeamAsync()).IsArchived);
    });

    [Fact]
    [Trait("Category", "Integration")]
    public AsyncTask ArchivingATeamHidesItAndItsProjectsFromEveryone() => RunAsync(async s =>
    {
        Assert.True((await s.NewTeamService().ArchiveTeamAsync(s.TeamId, s.AdminId)).Succeeded);

        foreach (var userId in new[] { s.OwnerId, s.AdminId, s.LeadId, s.TeamMemberId })
        {
            Assert.DoesNotContain(
                await s.NewTeamService().GetTeamsAsync(s.WorkspaceId, userId),
                team => team.Id == s.TeamId);

            Assert.Null(await s.NewTeamService().GetTeamAsync(s.TeamId, null, userId));
            Assert.Null(await s.NewProjectService().GetProjectAsync(s.ProjectId, null, userId));
            Assert.DoesNotContain(
                await s.NewProjectService().GetMyProjectsAsync(userId),
                project => project.Id == s.ProjectId);
        }

        Assert.Empty(await s.NewBoardService().GetBoardsAsync(s.ProjectId, null, s.TeamMemberId));
        Assert.Empty(await s.NewTaskService().GetTasksAsync(s.ColumnId, s.TeamMemberId));

        // The workspace itself is still there.
        Assert.NotNull(await s.NewWorkspaceService().GetWorkspaceAsync(s.WorkspaceId, s.LeadId));
    });

    [Fact]
    [Trait("Category", "Integration")]
    public AsyncTask OnlyWorkspaceOwnersAndAdminsSeeAndCanRestoreArchivedTeams() => RunAsync(async s =>
    {
        Assert.True((await s.NewTeamService().ArchiveTeamAsync(s.TeamId, s.AdminId)).Succeeded);

        foreach (var userId in new[] { s.OwnerId, s.AdminId })
        {
            var archived = await s.NewTeamService().GetArchivedTeamsAsync(s.WorkspaceId, userId);

            Assert.Equal(s.TeamId, Assert.Single(archived).Id);
        }

        foreach (var userId in new[] { s.LeadId, s.TeamMemberId, s.PlainMemberId, s.ViewerId, s.OutsiderId })
        {
            Assert.Empty(await s.NewTeamService().GetArchivedTeamsAsync(s.WorkspaceId, userId));

            var result = await s.NewTeamService().RestoreTeamAsync(s.TeamId, userId);

            Assert.False(result.Succeeded);
        }

        Assert.True((await s.LoadTeamAsync()).IsArchived);

        var restored = await s.NewTeamService().RestoreTeamAsync(s.TeamId, s.AdminId);

        Assert.True(restored.Succeeded, restored.Error);
        Assert.False((await s.LoadTeamAsync()).IsArchived);

        Assert.NotNull(await s.NewTeamService().GetTeamAsync(s.TeamId, null, s.LeadId));
        Assert.NotNull(await s.NewProjectService().GetProjectAsync(s.ProjectId, null, s.TeamMemberId));
        Assert.Empty(await s.NewTeamService().GetArchivedTeamsAsync(s.WorkspaceId, s.AdminId));
    });

    [Fact]
    [Trait("Category", "Integration")]
    public AsyncTask ATeamCannotBeRestoredWhileItsWorkspaceIsArchived() => RunAsync(async s =>
    {
        Assert.True((await s.NewTeamService().ArchiveTeamAsync(s.TeamId, s.AdminId)).Succeeded);
        Assert.True((await s.NewWorkspaceService().ArchiveWorkspaceAsync(s.WorkspaceId, s.OwnerId)).Succeeded);

        var blocked = await s.NewTeamService().RestoreTeamAsync(s.TeamId, s.OwnerId);

        Assert.False(blocked.Succeeded);
        Assert.Empty(await s.NewTeamService().GetArchivedTeamsAsync(s.WorkspaceId, s.OwnerId));

        Assert.True((await s.NewWorkspaceService().RestoreWorkspaceAsync(s.WorkspaceId, s.OwnerId)).Succeeded);

        // Restoring the workspace does not bring back a team archived on its own.
        Assert.Null(await s.NewTeamService().GetTeamAsync(s.TeamId, null, s.LeadId));

        var restored = await s.NewTeamService().RestoreTeamAsync(s.TeamId, s.OwnerId);

        Assert.True(restored.Succeeded, restored.Error);
        Assert.NotNull(await s.NewTeamService().GetTeamAsync(s.TeamId, null, s.LeadId));
    });

    [Fact]
    [Trait("Category", "Integration")]
    public AsyncTask LifecycleFlagsFollowTheRules() => RunAsync(async s =>
    {
        foreach (var userId in new[] { s.OwnerId, s.AdminId })
        {
            var access = await s.NewTeamService().GetTeamLifecycleAccessAsync(s.TeamId, userId);

            Assert.True(access.CanEdit);
            Assert.True(access.CanArchive);
        }

        var lead = await s.NewTeamService().GetTeamLifecycleAccessAsync(s.TeamId, s.LeadId);

        Assert.True(lead.CanEdit);
        Assert.False(lead.CanArchive);

        foreach (var userId in new[] { s.TeamMemberId, s.PlainMemberId, s.ViewerId, s.OutsiderId })
        {
            var access = await s.NewTeamService().GetTeamLifecycleAccessAsync(s.TeamId, userId);

            Assert.False(access.CanEdit);
            Assert.False(access.CanArchive);
        }

        Assert.Equal(
            TeamLifecycleAccess.None,
            await s.NewTeamService().GetTeamLifecycleAccessAsync(Guid.NewGuid(), s.OwnerId));
    });

    // ------------------------------------------------------------------
    // Test plumbing
    // ------------------------------------------------------------------

    private async AsyncTask RunAsync(Func<LifecycleScenario, AsyncTask> test)
    {
        await using var connection = fixture.CreateConnection();
        await connection.OpenAsync();

        var transaction = await connection.BeginTransactionAsync();
        Exception? originalException = null;

        try
        {
            await using var scenario = await LifecycleScenario.CreateAsync(
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

    private sealed class LifecycleScenario : IAsyncDisposable
    {
        private readonly SqlServerIntegrationFixture fixture;
        private readonly SqlConnection connection;
        private readonly DbTransaction transaction;
        private readonly List<ApplicationDbContext> contexts = new();

        private LifecycleScenario(
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

        public string OwnerId { get; } = NewUserId("owner");
        public string AdminId { get; } = NewUserId("admin");
        public string LeadId { get; } = NewUserId("lead");
        public string TeamMemberId { get; } = NewUserId("team-member");
        public string PlainMemberId { get; } = NewUserId("plain-member");
        public string ViewerId { get; } = NewUserId("viewer");
        public string OutsiderId { get; } = NewUserId("outsider");

        public static async System.Threading.Tasks.Task<LifecycleScenario> CreateAsync(
            SqlServerIntegrationFixture fixture,
            SqlConnection connection,
            DbTransaction transaction)
        {
            var scenario = new LifecycleScenario(fixture, connection, transaction);

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

        public WorkspaceService NewWorkspaceService() => new(NewContext());

        public TeamService NewTeamService() => new(NewContext());

        public ProjectService NewProjectService() => new(NewContext());

        public BoardService NewBoardService() => new(NewContext());

        public TaskService NewTaskService() => new(NewContext());

        public async System.Threading.Tasks.Task<Workspace> LoadWorkspaceAsync()
        {
            return await NewContext().Workspaces
                .AsNoTracking()
                .SingleAsync(workspace => workspace.Id == WorkspaceId);
        }

        public async System.Threading.Tasks.Task<Team> LoadTeamAsync()
        {
            return await NewContext().Teams
                .AsNoTracking()
                .SingleAsync(team => team.Id == TeamId);
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
                User(OwnerId),
                User(AdminId),
                User(LeadId),
                User(TeamMemberId),
                User(PlainMemberId),
                User(ViewerId),
                User(OutsiderId),

                new Workspace
                {
                    Id = WorkspaceId,
                    Name = "Original workspace",
                    Description = "Original workspace description"
                },
                new Team
                {
                    Id = TeamId,
                    WorkspaceId = WorkspaceId,
                    Name = "Original team",
                    Description = "Original team description"
                },

                WorkspaceMembership(OwnerId, WorkspaceRole.Owner),
                WorkspaceMembership(AdminId, WorkspaceRole.Admin),
                WorkspaceMembership(LeadId, WorkspaceRole.Member),
                WorkspaceMembership(TeamMemberId, WorkspaceRole.Member),
                WorkspaceMembership(PlainMemberId, WorkspaceRole.Member),
                WorkspaceMembership(ViewerId, WorkspaceRole.Viewer),

                new TeamMember { TeamId = TeamId, UserId = LeadId, Role = TeamRole.Lead, IsActive = true },
                new TeamMember { TeamId = TeamId, UserId = TeamMemberId, Role = TeamRole.Member, IsActive = true },

                new Project
                {
                    Id = ProjectId,
                    TeamId = TeamId,
                    Name = "Project in the team",
                    Status = ProjectStatus.Planned
                },
                new ProjectMember { ProjectId = ProjectId, UserId = TeamMemberId, Role = ProjectRole.Member, IsActive = true },

                new Board { Id = BoardId, ProjectId = ProjectId, Name = "Board", IsDefault = true },
                new BoardColumn { Id = ColumnId, BoardId = BoardId, Name = "To Do", Position = 1, IsDefault = true },

                new TaskEntity
                {
                    Id = Guid.NewGuid(),
                    BoardColumnId = ColumnId,
                    CreatedByUserId = TeamMemberId,
                    Title = "Seeded task",
                    Priority = TaskPriority.Medium,
                    Position = 1
                });

            await context.SaveChangesAsync();
        }

        private WorkspaceMember WorkspaceMembership(string userId, WorkspaceRole role) => new()
        {
            WorkspaceId = WorkspaceId,
            UserId = userId,
            Role = role,
            IsActive = true
        };

        private static string NewUserId(string label) => $"wt-{label}-{Guid.NewGuid():N}";

        private static ApplicationUser User(string id) => new()
        {
            Id = id,
            UserName = $"{id}@taskflow.test",
            NormalizedUserName = $"{id}@taskflow.test".ToUpperInvariant(),
            Email = $"{id}@taskflow.test",
            NormalizedEmail = $"{id}@taskflow.test".ToUpperInvariant(),
            FirstName = "Lifecycle",
            LastName = "Tester"
        };
    }
}
