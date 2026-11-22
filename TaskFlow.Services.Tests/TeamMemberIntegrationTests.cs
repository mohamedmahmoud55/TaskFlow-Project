using System.Data.Common;
using Microsoft.Data.SqlClient;
using Microsoft.EntityFrameworkCore;
using TaskFlow.Data.Context;
using TaskFlow.Data.Entities;
using TaskFlow.Data.Enums;
using TaskFlow.Services.Teams;
using AsyncTask = System.Threading.Tasks.Task;

namespace TaskFlow.Services.Tests;

/// <summary>
/// Who may add, change and remove team members, and what each person can
/// see. Every test seeds the same workspace inside a transaction that is
/// rolled back afterwards:
///
///   WsOwner and WsAdmin administer the workspace but are not on the team.
///   Lead and Lead2 lead the team; Member and Viewer are on it.
///   Free and Free2 belong to the workspace but not to the team.
///   Outsider has an account but belongs to nothing.
/// </summary>
[Collection(SqlServerIntegrationCollection.Name)]
public sealed class TeamMemberIntegrationTests
{
    private readonly SqlServerIntegrationFixture fixture;

    public TeamMemberIntegrationTests(SqlServerIntegrationFixture fixture)
    {
        this.fixture = fixture;
    }

    // ------------------------------------------------------------------
    // The role rules themselves
    // ------------------------------------------------------------------

    [Fact]
    public void WorkspaceAdminsCanHandOutEveryRoleAndLeadsOnlyMemberAndViewer()
    {
        Assert.Equal(
            new[] { TeamRole.Lead, TeamRole.Member, TeamRole.Viewer },
            TeamMemberRules.AssignableRoles(true, null).ToArray());

        Assert.Equal(
            new[] { TeamRole.Lead, TeamRole.Member, TeamRole.Viewer },
            TeamMemberRules.AssignableRoles(true, TeamRole.Member).ToArray());

        Assert.Equal(
            new[] { TeamRole.Member, TeamRole.Viewer },
            TeamMemberRules.AssignableRoles(false, TeamRole.Lead).ToArray());

        Assert.Empty(TeamMemberRules.AssignableRoles(false, TeamRole.Member));
        Assert.Empty(TeamMemberRules.AssignableRoles(false, TeamRole.Viewer));
        Assert.Empty(TeamMemberRules.AssignableRoles(false, null));
    }

    // ------------------------------------------------------------------
    // Adding members
    // ------------------------------------------------------------------

    [Fact]
    [Trait("Category", "Integration")]
    public AsyncTask WorkspaceOwnersAndAdminsCanAddWorkspaceMembersWithAnyRole() => RunAsync(async s =>
    {
        var byOwner = await s.NewService().AddTeamMemberAsync(
            s.TeamId, s.WsOwnerId, s.FreeId, TeamRole.Lead);

        var byAdmin = await s.NewService().AddTeamMemberAsync(
            s.TeamId, s.WsAdminId, s.Free2Id, TeamRole.Viewer);

        Assert.True(byOwner.Succeeded, byOwner.Error);
        Assert.True(byAdmin.Succeeded, byAdmin.Error);

        Assert.Equal(TeamRole.Lead, (await s.MembershipOfAsync(s.FreeId))!.Role);
        Assert.Equal(TeamRole.Viewer, (await s.MembershipOfAsync(s.Free2Id))!.Role);
    });

    [Fact]
    [Trait("Category", "Integration")]
    public AsyncTask LeadsCanAddMembersAndViewersButNotLeads() => RunAsync(async s =>
    {
        var asLead = await s.NewService().AddTeamMemberAsync(
            s.TeamId, s.LeadId, s.Free2Id, TeamRole.Lead);

        Assert.False(asLead.Succeeded);
        Assert.Null(await s.MembershipOfAsync(s.Free2Id));

        var asMember = await s.NewService().AddTeamMemberAsync(
            s.TeamId, s.LeadId, s.FreeId, TeamRole.Member);

        var asViewer = await s.NewService().AddTeamMemberAsync(
            s.TeamId, s.LeadId, s.Free2Id, TeamRole.Viewer);

        Assert.True(asMember.Succeeded, asMember.Error);
        Assert.True(asViewer.Succeeded, asViewer.Error);
    });

    [Fact]
    [Trait("Category", "Integration")]
    public AsyncTask MembersViewersAndPeopleOutsideTheTeamCannotAdd() => RunAsync(async s =>
    {
        foreach (var actorId in new[] { s.MemberId, s.ViewerId, s.FreeId, s.OutsiderId })
        {
            var result = await s.NewService().AddTeamMemberAsync(
                s.TeamId, actorId, s.Free2Id, TeamRole.Member);

            Assert.False(result.Succeeded);
        }

        Assert.Null(await s.MembershipOfAsync(s.Free2Id));
    });

    [Fact]
    [Trait("Category", "Integration")]
    public AsyncTask OnlyWorkspaceMembersCanBeAddedAndInputIsChecked() => RunAsync(async s =>
    {
        var outsider = await s.NewService().AddTeamMemberAsync(
            s.TeamId, s.WsAdminId, s.OutsiderId, TeamRole.Member);

        Assert.False(outsider.Succeeded);
        Assert.Contains("workspace", outsider.Error);
        Assert.Null(await s.MembershipOfAsync(s.OutsiderId));

        Assert.False((await s.NewService().AddTeamMemberAsync(
            s.TeamId, s.WsAdminId, "no-such-user", TeamRole.Member)).Succeeded);

        Assert.False((await s.NewService().AddTeamMemberAsync(
            s.TeamId, s.WsAdminId, "  ", TeamRole.Member)).Succeeded);

        Assert.False((await s.NewService().AddTeamMemberAsync(
            s.TeamId, s.WsAdminId, s.FreeId, (TeamRole)99)).Succeeded);

        Assert.False((await s.NewService().AddTeamMemberAsync(
            s.TeamId, s.WsAdminId, s.MemberId, TeamRole.Viewer)).Succeeded);

        Assert.Null(await s.MembershipOfAsync(s.FreeId));
        Assert.Equal(TeamRole.Member, (await s.MembershipOfAsync(s.MemberId))!.Role);
    });

    [Fact]
    [Trait("Category", "Integration")]
    public AsyncTask ADeactivatedTeamMemberCanBeAddedBack() => RunAsync(async s =>
    {
        var deactivated = await s.NewService().DeactivateTeamMemberAsync(
            s.TeamId, await s.MembershipIdOfAsync(s.MemberId), s.WsAdminId);

        Assert.True(deactivated.Succeeded, deactivated.Error);

        var readded = await s.NewService().AddTeamMemberAsync(
            s.TeamId, s.LeadId, s.MemberId, TeamRole.Viewer);

        Assert.True(readded.Succeeded, readded.Error);

        var membership = await s.MembershipOfAsync(s.MemberId);

        Assert.NotNull(membership);
        Assert.True(membership.IsActive);
        Assert.Equal(TeamRole.Viewer, membership.Role);
    });

    // ------------------------------------------------------------------
    // Changing roles
    // ------------------------------------------------------------------

    [Fact]
    [Trait("Category", "Integration")]
    public AsyncTask WorkspaceAdminsCanChangeAnyTeamRole() => RunAsync(async s =>
    {
        var promote = await s.NewService().UpdateTeamMemberRoleAsync(
            s.TeamId, await s.MembershipIdOfAsync(s.MemberId), s.WsAdminId, TeamRole.Lead);

        var demote = await s.NewService().UpdateTeamMemberRoleAsync(
            s.TeamId, await s.MembershipIdOfAsync(s.Lead2Id), s.WsOwnerId, TeamRole.Member);

        Assert.True(promote.Succeeded, promote.Error);
        Assert.True(demote.Succeeded, demote.Error);
        Assert.Equal(TeamRole.Lead, (await s.MembershipOfAsync(s.MemberId))!.Role);
        Assert.Equal(TeamRole.Member, (await s.MembershipOfAsync(s.Lead2Id))!.Role);
    });

    [Fact]
    [Trait("Category", "Integration")]
    public AsyncTask LeadsCanOnlyMoveMembersAndViewersBetweenMemberAndViewer() => RunAsync(async s =>
    {
        var allowed = await s.NewService().UpdateTeamMemberRoleAsync(
            s.TeamId, await s.MembershipIdOfAsync(s.MemberId), s.LeadId, TeamRole.Viewer);

        Assert.True(allowed.Succeeded, allowed.Error);

        var promoteToLead = await s.NewService().UpdateTeamMemberRoleAsync(
            s.TeamId, await s.MembershipIdOfAsync(s.ViewerId), s.LeadId, TeamRole.Lead);

        var otherLead = await s.NewService().UpdateTeamMemberRoleAsync(
            s.TeamId, await s.MembershipIdOfAsync(s.Lead2Id), s.LeadId, TeamRole.Member);

        var themselves = await s.NewService().UpdateTeamMemberRoleAsync(
            s.TeamId, await s.MembershipIdOfAsync(s.LeadId), s.LeadId, TeamRole.Member);

        Assert.False(promoteToLead.Succeeded);
        Assert.False(otherLead.Succeeded);
        Assert.False(themselves.Succeeded);

        Assert.Equal(TeamRole.Viewer, (await s.MembershipOfAsync(s.MemberId))!.Role);
        Assert.Equal(TeamRole.Viewer, (await s.MembershipOfAsync(s.ViewerId))!.Role);
        Assert.Equal(TeamRole.Lead, (await s.MembershipOfAsync(s.Lead2Id))!.Role);
        Assert.Equal(TeamRole.Lead, (await s.MembershipOfAsync(s.LeadId))!.Role);
    });

    [Fact]
    [Trait("Category", "Integration")]
    public AsyncTask MembersViewersAndPeopleOutsideTheTeamCannotChangeRoles() => RunAsync(async s =>
    {
        var viewerMembership = await s.MembershipIdOfAsync(s.ViewerId);

        foreach (var actorId in new[] { s.MemberId, s.ViewerId, s.FreeId, s.OutsiderId })
        {
            var result = await s.NewService().UpdateTeamMemberRoleAsync(
                s.TeamId, viewerMembership, actorId, TeamRole.Member);

            Assert.False(result.Succeeded);
        }

        Assert.Equal(TeamRole.Viewer, (await s.MembershipOfAsync(s.ViewerId))!.Role);
    });

    // ------------------------------------------------------------------
    // Deactivating members
    // ------------------------------------------------------------------

    [Fact]
    [Trait("Category", "Integration")]
    public AsyncTask DeactivatingFollowsTheSameRules() => RunAsync(async s =>
    {
        var leadMembership = await s.MembershipIdOfAsync(s.LeadId);
        var lead2Membership = await s.MembershipIdOfAsync(s.Lead2Id);
        var memberMembership = await s.MembershipIdOfAsync(s.MemberId);
        var viewerMembership = await s.MembershipIdOfAsync(s.ViewerId);

        // Not allowed
        Assert.False((await s.NewService().DeactivateTeamMemberAsync(s.TeamId, lead2Membership, s.LeadId)).Succeeded);
        Assert.False((await s.NewService().DeactivateTeamMemberAsync(s.TeamId, leadMembership, s.LeadId)).Succeeded);
        Assert.False((await s.NewService().DeactivateTeamMemberAsync(s.TeamId, viewerMembership, s.MemberId)).Succeeded);
        Assert.False((await s.NewService().DeactivateTeamMemberAsync(s.TeamId, memberMembership, s.ViewerId)).Succeeded);
        Assert.False((await s.NewService().DeactivateTeamMemberAsync(s.TeamId, memberMembership, s.FreeId)).Succeeded);
        Assert.False((await s.NewService().DeactivateTeamMemberAsync(s.TeamId, memberMembership, s.OutsiderId)).Succeeded);

        foreach (var userId in new[] { s.LeadId, s.Lead2Id, s.MemberId, s.ViewerId })
        {
            Assert.True((await s.MembershipOfAsync(userId))!.IsActive);
        }

        // Allowed
        var byLead = await s.NewService().DeactivateTeamMemberAsync(
            s.TeamId, memberMembership, s.LeadId);

        var byAdmin = await s.NewService().DeactivateTeamMemberAsync(
            s.TeamId, lead2Membership, s.WsAdminId);

        Assert.True(byLead.Succeeded, byLead.Error);
        Assert.True(byAdmin.Succeeded, byAdmin.Error);
        Assert.False((await s.MembershipOfAsync(s.MemberId))!.IsActive);
        Assert.False((await s.MembershipOfAsync(s.Lead2Id))!.IsActive);
    });

    [Fact]
    [Trait("Category", "Integration")]
    public AsyncTask ADeactivatedMemberLosesTheTeam() => RunAsync(async s =>
    {
        var result = await s.NewService().DeactivateTeamMemberAsync(
            s.TeamId, await s.MembershipIdOfAsync(s.MemberId), s.LeadId);

        Assert.True(result.Succeeded, result.Error);

        Assert.Null(await s.NewService().GetTeamAsync(s.TeamId, null, s.MemberId));
        Assert.Empty(await s.NewService().GetTeamMembersAsync(s.TeamId, s.MemberId));
        Assert.NotNull(await s.NewService().GetTeamAsync(s.TeamId, null, s.WsAdminId));
    });

    // ------------------------------------------------------------------
    // What each person sees
    // ------------------------------------------------------------------

    [Fact]
    [Trait("Category", "Integration")]
    public AsyncTask MemberListShowsDeactivatedPeopleOnlyToManagers() => RunAsync(async s =>
    {
        var deactivated = await s.NewService().DeactivateTeamMemberAsync(
            s.TeamId, await s.MembershipIdOfAsync(s.ViewerId), s.LeadId);

        Assert.True(deactivated.Succeeded, deactivated.Error);

        foreach (var managerId in new[] { s.WsOwnerId, s.WsAdminId, s.LeadId })
        {
            Assert.Equal(4, (await s.NewService().GetTeamMembersAsync(s.TeamId, managerId)).Count);
        }

        var memberView = await s.NewService().GetTeamMembersAsync(s.TeamId, s.MemberId);

        Assert.Equal(3, memberView.Count);
        Assert.DoesNotContain(memberView, member => member.UserId == s.ViewerId);
        Assert.Contains(memberView, member => member.UserId == s.LeadId && member.Email == s.EmailOf(s.LeadId));

        foreach (var userId in new[] { s.FreeId, s.OutsiderId, s.ViewerId })
        {
            Assert.Empty(await s.NewService().GetTeamMembersAsync(s.TeamId, userId));
        }
    });

    [Fact]
    [Trait("Category", "Integration")]
    public AsyncTask ManageFlagsFollowTheRoleRules() => RunAsync(async s =>
    {
        var adminView = await s.NewService().GetTeamMembersAsync(s.TeamId, s.WsAdminId);

        Assert.All(adminView, member => Assert.True(member.CanManage));

        var leadView = await s.NewService().GetTeamMembersAsync(s.TeamId, s.LeadId);

        Assert.True(leadView.Single(member => member.UserId == s.MemberId).CanManage);
        Assert.True(leadView.Single(member => member.UserId == s.ViewerId).CanManage);
        Assert.False(leadView.Single(member => member.UserId == s.Lead2Id).CanManage);
        Assert.False(leadView.Single(member => member.UserId == s.LeadId).CanManage);
        Assert.True(leadView.Single(member => member.UserId == s.LeadId).IsCurrentUser);

        var memberView = await s.NewService().GetTeamMembersAsync(s.TeamId, s.MemberId);

        Assert.All(memberView, member => Assert.False(member.CanManage));
    });

    [Fact]
    [Trait("Category", "Integration")]
    public AsyncTask ManagementInfoListsWhoCanBeAddedAndWhichRolesCanBeGiven() => RunAsync(async s =>
    {
        var admin = await s.NewService().GetTeamMemberManagementAsync(s.TeamId, s.WsAdminId);

        Assert.True(admin.CanManage);
        Assert.Equal(
            new[] { TeamRole.Lead, TeamRole.Member, TeamRole.Viewer },
            admin.AssignableRoles.ToArray());
        Assert.Contains(admin.Candidates, candidate => candidate.UserId == s.FreeId);
        Assert.Contains(admin.Candidates, candidate => candidate.UserId == s.Free2Id);
        Assert.DoesNotContain(admin.Candidates, candidate => candidate.UserId == s.OutsiderId);
        Assert.DoesNotContain(admin.Candidates, candidate => candidate.UserId == s.MemberId);

        var lead = await s.NewService().GetTeamMemberManagementAsync(s.TeamId, s.LeadId);

        Assert.True(lead.CanManage);
        Assert.Equal(
            new[] { TeamRole.Member, TeamRole.Viewer },
            lead.AssignableRoles.ToArray());

        foreach (var userId in new[] { s.MemberId, s.ViewerId, s.FreeId, s.OutsiderId })
        {
            var none = await s.NewService().GetTeamMemberManagementAsync(s.TeamId, userId);

            Assert.False(none.CanManage);
            Assert.Empty(none.AssignableRoles);
            Assert.Empty(none.Candidates);
        }
    });

    // ------------------------------------------------------------------
    // Test plumbing
    // ------------------------------------------------------------------

    private async AsyncTask RunAsync(Func<TeamScenario, AsyncTask> test)
    {
        await using var connection = fixture.CreateConnection();
        await connection.OpenAsync();

        var transaction = await connection.BeginTransactionAsync();
        Exception? originalException = null;

        try
        {
            await using var scenario = await TeamScenario.CreateAsync(
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

    private sealed class TeamScenario : IAsyncDisposable
    {
        private readonly SqlServerIntegrationFixture fixture;
        private readonly SqlConnection connection;
        private readonly DbTransaction transaction;
        private readonly List<ApplicationDbContext> contexts = new();

        private TeamScenario(
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

        public string WsOwnerId { get; } = NewUserId("ws-owner");
        public string WsAdminId { get; } = NewUserId("ws-admin");
        public string LeadId { get; } = NewUserId("lead");
        public string Lead2Id { get; } = NewUserId("lead2");
        public string MemberId { get; } = NewUserId("member");
        public string ViewerId { get; } = NewUserId("viewer");
        public string FreeId { get; } = NewUserId("free");
        public string Free2Id { get; } = NewUserId("free2");
        public string OutsiderId { get; } = NewUserId("outsider");

        public static async System.Threading.Tasks.Task<TeamScenario> CreateAsync(
            SqlServerIntegrationFixture fixture,
            SqlConnection connection,
            DbTransaction transaction)
        {
            var scenario = new TeamScenario(fixture, connection, transaction);

            await scenario.SeedAsync();

            return scenario;
        }

        public string EmailOf(string userId) => $"{userId}@taskflow.test";

        // A fresh context per call, so tracked entities from one call can
        // never hide a problem in the next.
        public ApplicationDbContext NewContext()
        {
            var context = fixture.CreateContext(connection);

            context.Database.UseTransaction(transaction);
            contexts.Add(context);

            return context;
        }

        public TeamService NewService() => new(NewContext());

        public async System.Threading.Tasks.Task<TeamMember?> MembershipOfAsync(string userId)
        {
            return await NewContext().TeamMembers
                .AsNoTracking()
                .SingleOrDefaultAsync(member =>
                    member.TeamId == TeamId &&
                    member.UserId == userId);
        }

        public async System.Threading.Tasks.Task<Guid> MembershipIdOfAsync(string userId)
        {
            var membership = await NewContext().TeamMembers
                .AsNoTracking()
                .SingleAsync(member =>
                    member.TeamId == TeamId &&
                    member.UserId == userId);

            return membership.Id;
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
                User(LeadId),
                User(Lead2Id),
                User(MemberId),
                User(ViewerId),
                User(FreeId),
                User(Free2Id),
                User(OutsiderId),

                new Workspace { Id = WorkspaceId, Name = "Team members workspace" },
                new Team { Id = TeamId, WorkspaceId = WorkspaceId, Name = "Team members team" },

                WorkspaceMembership(WsOwnerId, WorkspaceRole.Owner),
                WorkspaceMembership(WsAdminId, WorkspaceRole.Admin),
                WorkspaceMembership(LeadId, WorkspaceRole.Member),
                WorkspaceMembership(Lead2Id, WorkspaceRole.Member),
                WorkspaceMembership(MemberId, WorkspaceRole.Member),
                WorkspaceMembership(ViewerId, WorkspaceRole.Member),
                WorkspaceMembership(FreeId, WorkspaceRole.Member),
                WorkspaceMembership(Free2Id, WorkspaceRole.Member),

                TeamMembership(LeadId, TeamRole.Lead),
                TeamMembership(Lead2Id, TeamRole.Lead),
                TeamMembership(MemberId, TeamRole.Member),
                TeamMembership(ViewerId, TeamRole.Viewer));

            await context.SaveChangesAsync();
        }

        private WorkspaceMember WorkspaceMembership(string userId, WorkspaceRole role) => new()
        {
            WorkspaceId = WorkspaceId,
            UserId = userId,
            Role = role,
            IsActive = true
        };

        private TeamMember TeamMembership(string userId, TeamRole role) => new()
        {
            TeamId = TeamId,
            UserId = userId,
            Role = role,
            IsActive = true
        };

        private static string NewUserId(string label) => $"tm-{label}-{Guid.NewGuid():N}";

        private static ApplicationUser User(string id) => new()
        {
            Id = id,
            UserName = $"{id}@taskflow.test",
            NormalizedUserName = $"{id}@taskflow.test".ToUpperInvariant(),
            Email = $"{id}@taskflow.test",
            NormalizedEmail = $"{id}@taskflow.test".ToUpperInvariant(),
            FirstName = "Team",
            LastName = "Tester"
        };
    }
}
