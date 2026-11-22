using System.Data.Common;
using Microsoft.Data.SqlClient;
using Microsoft.EntityFrameworkCore;
using TaskFlow.Data.Context;
using TaskFlow.Data.Entities;
using TaskFlow.Data.Enums;
using TaskFlow.Services.Workspaces;
using AsyncTask = System.Threading.Tasks.Task;

namespace TaskFlow.Services.Tests;

/// <summary>
/// Who may add, change and remove workspace members, and what each person
/// is allowed to see. Every test seeds the same workspace inside a
/// transaction that is rolled back afterwards:
///
///   Owner, two Admins, a Member and a Viewer belong to it.
///   Outsider has an account but no membership.
///   CandidateA, B and C have accounts and are available to be added.
/// </summary>
[Collection(SqlServerIntegrationCollection.Name)]
public sealed class WorkspaceMemberIntegrationTests
{
    private readonly SqlServerIntegrationFixture fixture;

    public WorkspaceMemberIntegrationTests(SqlServerIntegrationFixture fixture)
    {
        this.fixture = fixture;
    }

    // ------------------------------------------------------------------
    // The role hierarchy itself
    // ------------------------------------------------------------------

    [Fact]
    public void OnlyOwnersAndAdminsCanHandOutRolesAndNobodyCanHandOutOwner()
    {
        Assert.Equal(
            new[] { WorkspaceRole.Admin, WorkspaceRole.Member, WorkspaceRole.Viewer },
            WorkspaceMemberRules.AssignableRoles(WorkspaceRole.Owner).ToArray());

        Assert.Equal(
            new[] { WorkspaceRole.Member, WorkspaceRole.Viewer },
            WorkspaceMemberRules.AssignableRoles(WorkspaceRole.Admin).ToArray());

        Assert.Empty(WorkspaceMemberRules.AssignableRoles(WorkspaceRole.Member));
        Assert.Empty(WorkspaceMemberRules.AssignableRoles(WorkspaceRole.Viewer));

        foreach (var actorRole in Enum.GetValues<WorkspaceRole>())
        {
            Assert.False(WorkspaceMemberRules.CanAssign(actorRole, WorkspaceRole.Owner));
        }
    }

    // ------------------------------------------------------------------
    // Adding members
    // ------------------------------------------------------------------

    [Fact]
    [Trait("Category", "Integration")]
    public AsyncTask OwnerCanAddMembersWithAnyAssignableRole() => RunAsync(async s =>
    {
        foreach (var (userId, role) in new[]
        {
            (s.CandidateAId, WorkspaceRole.Admin),
            (s.CandidateBId, WorkspaceRole.Member),
            (s.CandidateCId, WorkspaceRole.Viewer)
        })
        {
            var result = await s.NewService().AddWorkspaceMemberAsync(
                s.WorkspaceId, s.OwnerId, s.EmailOf(userId), role);

            Assert.True(result.Succeeded, result.Error);

            var membership = await s.MembershipOfAsync(userId);

            Assert.NotNull(membership);
            Assert.Equal(role, membership.Role);
            Assert.True(membership.IsActive);
        }
    });

    [Fact]
    [Trait("Category", "Integration")]
    public AsyncTask AdminsCanAddMembersAndViewersButNotAdminsOrOwners() => RunAsync(async s =>
    {
        var asMember = await s.NewService().AddWorkspaceMemberAsync(
            s.WorkspaceId, s.AdminId, s.EmailOf(s.CandidateAId), WorkspaceRole.Member);

        var asViewer = await s.NewService().AddWorkspaceMemberAsync(
            s.WorkspaceId, s.AdminId, s.EmailOf(s.CandidateBId), WorkspaceRole.Viewer);

        Assert.True(asMember.Succeeded, asMember.Error);
        Assert.True(asViewer.Succeeded, asViewer.Error);

        foreach (var role in new[] { WorkspaceRole.Admin, WorkspaceRole.Owner })
        {
            var result = await s.NewService().AddWorkspaceMemberAsync(
                s.WorkspaceId, s.AdminId, s.EmailOf(s.CandidateCId), role);

            Assert.False(result.Succeeded);
        }

        Assert.Null(await s.MembershipOfAsync(s.CandidateCId));
    });

    [Fact]
    [Trait("Category", "Integration")]
    public AsyncTask MembersViewersAndOutsidersCannotAddMembers() => RunAsync(async s =>
    {
        foreach (var actorId in new[] { s.MemberId, s.ViewerId, s.OutsiderId })
        {
            var result = await s.NewService().AddWorkspaceMemberAsync(
                s.WorkspaceId, actorId, s.EmailOf(s.CandidateAId), WorkspaceRole.Member);

            Assert.False(result.Succeeded);
        }

        Assert.Null(await s.MembershipOfAsync(s.CandidateAId));
    });

    [Fact]
    [Trait("Category", "Integration")]
    public AsyncTask AddingRejectsUnknownEmailsAndInvalidInput() => RunAsync(async s =>
    {
        var unknown = await s.NewService().AddWorkspaceMemberAsync(
            s.WorkspaceId, s.OwnerId, "nobody@nowhere.test", WorkspaceRole.Member);

        Assert.False(unknown.Succeeded);
        Assert.Contains("No registered account", unknown.Error);

        var blank = await s.NewService().AddWorkspaceMemberAsync(
            s.WorkspaceId, s.OwnerId, "   ", WorkspaceRole.Member);

        Assert.False(blank.Succeeded);

        var invalidRole = await s.NewService().AddWorkspaceMemberAsync(
            s.WorkspaceId, s.OwnerId, s.EmailOf(s.CandidateAId), (WorkspaceRole)99);

        Assert.False(invalidRole.Succeeded);

        var ownerRole = await s.NewService().AddWorkspaceMemberAsync(
            s.WorkspaceId, s.OwnerId, s.EmailOf(s.CandidateAId), WorkspaceRole.Owner);

        Assert.False(ownerRole.Succeeded);
        Assert.Null(await s.MembershipOfAsync(s.CandidateAId));
    });

    [Fact]
    [Trait("Category", "Integration")]
    public AsyncTask AnExistingMemberCannotBeAddedAgainButADeactivatedOneIsReactivated() => RunAsync(async s =>
    {
        var again = await s.NewService().AddWorkspaceMemberAsync(
            s.WorkspaceId, s.OwnerId, s.EmailOf(s.MemberId), WorkspaceRole.Viewer);

        Assert.False(again.Succeeded);

        var addOwnerAgain = await s.NewService().AddWorkspaceMemberAsync(
            s.WorkspaceId, s.AdminId, s.EmailOf(s.OwnerId), WorkspaceRole.Member);

        Assert.False(addOwnerAgain.Succeeded);

        var unchanged = await s.MembershipOfAsync(s.MemberId);

        Assert.NotNull(unchanged);
        Assert.Equal(WorkspaceRole.Member, unchanged.Role);

        var deactivated = await s.NewService().DeactivateWorkspaceMemberAsync(
            s.WorkspaceId, await s.MembershipIdOfAsync(s.MemberId), s.OwnerId);

        Assert.True(deactivated.Succeeded, deactivated.Error);

        var readded = await s.NewService().AddWorkspaceMemberAsync(
            s.WorkspaceId, s.OwnerId, s.EmailOf(s.MemberId), WorkspaceRole.Viewer);

        Assert.True(readded.Succeeded, readded.Error);

        var reactivated = await s.MembershipOfAsync(s.MemberId);

        Assert.NotNull(reactivated);
        Assert.True(reactivated.IsActive);
        Assert.Equal(WorkspaceRole.Viewer, reactivated.Role);
    });

    // ------------------------------------------------------------------
    // Changing roles
    // ------------------------------------------------------------------

    [Fact]
    [Trait("Category", "Integration")]
    public AsyncTask OwnerCanChangeRolesBelowOwnerButNotTheirOwn() => RunAsync(async s =>
    {
        var promote = await s.NewService().UpdateWorkspaceMemberRoleAsync(
            s.WorkspaceId, await s.MembershipIdOfAsync(s.MemberId), s.OwnerId, WorkspaceRole.Admin);

        Assert.True(promote.Succeeded, promote.Error);
        Assert.Equal(WorkspaceRole.Admin, (await s.MembershipOfAsync(s.MemberId))!.Role);

        var demote = await s.NewService().UpdateWorkspaceMemberRoleAsync(
            s.WorkspaceId, await s.MembershipIdOfAsync(s.Admin2Id), s.OwnerId, WorkspaceRole.Viewer);

        Assert.True(demote.Succeeded, demote.Error);
        Assert.Equal(WorkspaceRole.Viewer, (await s.MembershipOfAsync(s.Admin2Id))!.Role);

        var makeOwner = await s.NewService().UpdateWorkspaceMemberRoleAsync(
            s.WorkspaceId, await s.MembershipIdOfAsync(s.ViewerId), s.OwnerId, WorkspaceRole.Owner);

        Assert.False(makeOwner.Succeeded);

        var ownOwnRole = await s.NewService().UpdateWorkspaceMemberRoleAsync(
            s.WorkspaceId, await s.MembershipIdOfAsync(s.OwnerId), s.OwnerId, WorkspaceRole.Admin);

        Assert.False(ownOwnRole.Succeeded);
        Assert.Equal(WorkspaceRole.Owner, (await s.MembershipOfAsync(s.OwnerId))!.Role);
        Assert.Equal(WorkspaceRole.Viewer, (await s.MembershipOfAsync(s.ViewerId))!.Role);
    });

    [Fact]
    [Trait("Category", "Integration")]
    public AsyncTask AdminsCanOnlyMoveMembersAndViewersBetweenMemberAndViewer() => RunAsync(async s =>
    {
        var memberMembership = await s.MembershipIdOfAsync(s.MemberId);
        var viewerMembership = await s.MembershipIdOfAsync(s.ViewerId);

        var allowed = await s.NewService().UpdateWorkspaceMemberRoleAsync(
            s.WorkspaceId, memberMembership, s.AdminId, WorkspaceRole.Viewer);

        Assert.True(allowed.Succeeded, allowed.Error);

        var promoteToAdmin = await s.NewService().UpdateWorkspaceMemberRoleAsync(
            s.WorkspaceId, viewerMembership, s.AdminId, WorkspaceRole.Admin);

        Assert.False(promoteToAdmin.Succeeded);

        var otherAdmin = await s.NewService().UpdateWorkspaceMemberRoleAsync(
            s.WorkspaceId, await s.MembershipIdOfAsync(s.Admin2Id), s.AdminId, WorkspaceRole.Member);

        Assert.False(otherAdmin.Succeeded);

        var theOwner = await s.NewService().UpdateWorkspaceMemberRoleAsync(
            s.WorkspaceId, await s.MembershipIdOfAsync(s.OwnerId), s.AdminId, WorkspaceRole.Member);

        Assert.False(theOwner.Succeeded);

        var themselves = await s.NewService().UpdateWorkspaceMemberRoleAsync(
            s.WorkspaceId, await s.MembershipIdOfAsync(s.AdminId), s.AdminId, WorkspaceRole.Member);

        Assert.False(themselves.Succeeded);

        Assert.Equal(WorkspaceRole.Viewer, (await s.MembershipOfAsync(s.MemberId))!.Role);
        Assert.Equal(WorkspaceRole.Viewer, (await s.MembershipOfAsync(s.ViewerId))!.Role);
        Assert.Equal(WorkspaceRole.Admin, (await s.MembershipOfAsync(s.Admin2Id))!.Role);
        Assert.Equal(WorkspaceRole.Owner, (await s.MembershipOfAsync(s.OwnerId))!.Role);
        Assert.Equal(WorkspaceRole.Admin, (await s.MembershipOfAsync(s.AdminId))!.Role);
    });

    [Fact]
    [Trait("Category", "Integration")]
    public AsyncTask MembersViewersAndOutsidersCannotChangeRoles() => RunAsync(async s =>
    {
        var viewerMembership = await s.MembershipIdOfAsync(s.ViewerId);

        foreach (var actorId in new[] { s.MemberId, s.ViewerId, s.OutsiderId })
        {
            var result = await s.NewService().UpdateWorkspaceMemberRoleAsync(
                s.WorkspaceId, viewerMembership, actorId, WorkspaceRole.Member);

            Assert.False(result.Succeeded);
        }

        Assert.Equal(WorkspaceRole.Viewer, (await s.MembershipOfAsync(s.ViewerId))!.Role);
    });

    // ------------------------------------------------------------------
    // Deactivating members
    // ------------------------------------------------------------------

    [Fact]
    [Trait("Category", "Integration")]
    public AsyncTask DeactivatingFollowsTheSameHierarchy() => RunAsync(async s =>
    {
        var ownerMembership = await s.MembershipIdOfAsync(s.OwnerId);
        var adminMembership = await s.MembershipIdOfAsync(s.AdminId);
        var admin2Membership = await s.MembershipIdOfAsync(s.Admin2Id);
        var memberMembership = await s.MembershipIdOfAsync(s.MemberId);
        var viewerMembership = await s.MembershipIdOfAsync(s.ViewerId);

        // Not allowed
        Assert.False((await s.NewService().DeactivateWorkspaceMemberAsync(s.WorkspaceId, admin2Membership, s.AdminId)).Succeeded);
        Assert.False((await s.NewService().DeactivateWorkspaceMemberAsync(s.WorkspaceId, ownerMembership, s.AdminId)).Succeeded);
        Assert.False((await s.NewService().DeactivateWorkspaceMemberAsync(s.WorkspaceId, adminMembership, s.AdminId)).Succeeded);
        Assert.False((await s.NewService().DeactivateWorkspaceMemberAsync(s.WorkspaceId, ownerMembership, s.OwnerId)).Succeeded);
        Assert.False((await s.NewService().DeactivateWorkspaceMemberAsync(s.WorkspaceId, viewerMembership, s.MemberId)).Succeeded);
        Assert.False((await s.NewService().DeactivateWorkspaceMemberAsync(s.WorkspaceId, memberMembership, s.ViewerId)).Succeeded);
        Assert.False((await s.NewService().DeactivateWorkspaceMemberAsync(s.WorkspaceId, memberMembership, s.OutsiderId)).Succeeded);

        foreach (var userId in new[] { s.OwnerId, s.AdminId, s.Admin2Id, s.MemberId, s.ViewerId })
        {
            Assert.True((await s.MembershipOfAsync(userId))!.IsActive);
        }

        // Allowed
        var byAdmin = await s.NewService().DeactivateWorkspaceMemberAsync(
            s.WorkspaceId, memberMembership, s.AdminId);

        var byOwner = await s.NewService().DeactivateWorkspaceMemberAsync(
            s.WorkspaceId, admin2Membership, s.OwnerId);

        Assert.True(byAdmin.Succeeded, byAdmin.Error);
        Assert.True(byOwner.Succeeded, byOwner.Error);
        Assert.False((await s.MembershipOfAsync(s.MemberId))!.IsActive);
        Assert.False((await s.MembershipOfAsync(s.Admin2Id))!.IsActive);
    });

    [Fact]
    [Trait("Category", "Integration")]
    public AsyncTask ADeactivatedMemberLosesTheWorkspace() => RunAsync(async s =>
    {
        var result = await s.NewService().DeactivateWorkspaceMemberAsync(
            s.WorkspaceId, await s.MembershipIdOfAsync(s.MemberId), s.OwnerId);

        Assert.True(result.Succeeded, result.Error);

        Assert.Null(await s.NewService().GetWorkspaceAsync(s.WorkspaceId, s.MemberId));
        Assert.Empty(await s.NewService().GetWorkspaceMembersAsync(s.WorkspaceId, s.MemberId));
        Assert.NotNull(await s.NewService().GetWorkspaceAsync(s.WorkspaceId, s.OwnerId));
    });

    // ------------------------------------------------------------------
    // What each person sees
    // ------------------------------------------------------------------

    [Fact]
    [Trait("Category", "Integration")]
    public AsyncTask MemberListIsLimitedToMembersAndShowsDeactivatedPeopleOnlyToManagers() => RunAsync(async s =>
    {
        var deactivated = await s.NewService().DeactivateWorkspaceMemberAsync(
            s.WorkspaceId, await s.MembershipIdOfAsync(s.ViewerId), s.OwnerId);

        Assert.True(deactivated.Succeeded, deactivated.Error);

        foreach (var managerId in new[] { s.OwnerId, s.AdminId })
        {
            Assert.Equal(5, (await s.NewService().GetWorkspaceMembersAsync(s.WorkspaceId, managerId)).Count);
        }

        var memberView = await s.NewService().GetWorkspaceMembersAsync(s.WorkspaceId, s.MemberId);

        Assert.Equal(4, memberView.Count);
        Assert.DoesNotContain(memberView, member => member.UserId == s.ViewerId);
        Assert.Contains(memberView, member => member.UserId == s.OwnerId && member.Email == s.EmailOf(s.OwnerId));

        Assert.Empty(await s.NewService().GetWorkspaceMembersAsync(s.WorkspaceId, s.OutsiderId));
        Assert.Empty(await s.NewService().GetWorkspaceMembersAsync(s.WorkspaceId, s.ViewerId));
    });

    [Fact]
    [Trait("Category", "Integration")]
    public AsyncTask ManageFlagsFollowTheRoleHierarchy() => RunAsync(async s =>
    {
        var ownerView = await s.NewService().GetWorkspaceMembersAsync(s.WorkspaceId, s.OwnerId);

        Assert.False(ownerView.Single(member => member.UserId == s.OwnerId).CanManage);
        Assert.True(ownerView.Single(member => member.UserId == s.OwnerId).IsCurrentUser);

        foreach (var userId in new[] { s.AdminId, s.Admin2Id, s.MemberId, s.ViewerId })
        {
            Assert.True(ownerView.Single(member => member.UserId == userId).CanManage);
        }

        var adminView = await s.NewService().GetWorkspaceMembersAsync(s.WorkspaceId, s.AdminId);

        foreach (var userId in new[] { s.MemberId, s.ViewerId })
        {
            Assert.True(adminView.Single(member => member.UserId == userId).CanManage);
        }

        foreach (var userId in new[] { s.OwnerId, s.AdminId, s.Admin2Id })
        {
            Assert.False(adminView.Single(member => member.UserId == userId).CanManage);
        }

        var memberView = await s.NewService().GetWorkspaceMembersAsync(s.WorkspaceId, s.MemberId);

        Assert.All(memberView, member => Assert.False(member.CanManage));
    });

    [Fact]
    [Trait("Category", "Integration")]
    public AsyncTask OnlyOwnersAndAdminsCanManageMembers() => RunAsync(async s =>
    {
        foreach (var userId in new[] { s.OwnerId, s.AdminId, s.Admin2Id })
        {
            Assert.True(await s.NewService().CanManageWorkspaceMembersAsync(s.WorkspaceId, userId));
        }

        foreach (var userId in new[] { s.MemberId, s.ViewerId, s.OutsiderId })
        {
            Assert.False(await s.NewService().CanManageWorkspaceMembersAsync(s.WorkspaceId, userId));
        }

        Assert.False(await s.NewService().CanManageWorkspaceMembersAsync(Guid.NewGuid(), s.OwnerId));
    });

    // ------------------------------------------------------------------
    // Test plumbing
    // ------------------------------------------------------------------

    private async AsyncTask RunAsync(Func<WorkspaceScenario, AsyncTask> test)
    {
        await using var connection = fixture.CreateConnection();
        await connection.OpenAsync();

        var transaction = await connection.BeginTransactionAsync();
        Exception? originalException = null;

        try
        {
            await using var scenario = await WorkspaceScenario.CreateAsync(
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

    private sealed class WorkspaceScenario : IAsyncDisposable
    {
        private readonly SqlServerIntegrationFixture fixture;
        private readonly SqlConnection connection;
        private readonly DbTransaction transaction;
        private readonly List<ApplicationDbContext> contexts = new();

        private WorkspaceScenario(
            SqlServerIntegrationFixture fixture,
            SqlConnection connection,
            DbTransaction transaction)
        {
            this.fixture = fixture;
            this.connection = connection;
            this.transaction = transaction;
        }

        public Guid WorkspaceId { get; } = Guid.NewGuid();

        public string OwnerId { get; } = NewUserId("owner");
        public string AdminId { get; } = NewUserId("admin");
        public string Admin2Id { get; } = NewUserId("admin2");
        public string MemberId { get; } = NewUserId("member");
        public string ViewerId { get; } = NewUserId("viewer");
        public string OutsiderId { get; } = NewUserId("outsider");
        public string CandidateAId { get; } = NewUserId("candidate-a");
        public string CandidateBId { get; } = NewUserId("candidate-b");
        public string CandidateCId { get; } = NewUserId("candidate-c");

        public static async System.Threading.Tasks.Task<WorkspaceScenario> CreateAsync(
            SqlServerIntegrationFixture fixture,
            SqlConnection connection,
            DbTransaction transaction)
        {
            var scenario = new WorkspaceScenario(fixture, connection, transaction);

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

        public WorkspaceService NewService() => new(NewContext());

        public async System.Threading.Tasks.Task<WorkspaceMember?> MembershipOfAsync(string userId)
        {
            return await NewContext().WorkspaceMembers
                .AsNoTracking()
                .SingleOrDefaultAsync(member =>
                    member.WorkspaceId == WorkspaceId &&
                    member.UserId == userId);
        }

        public async System.Threading.Tasks.Task<Guid> MembershipIdOfAsync(string userId)
        {
            var membership = await NewContext().WorkspaceMembers
                .AsNoTracking()
                .SingleAsync(member =>
                    member.WorkspaceId == WorkspaceId &&
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
                User(OwnerId),
                User(AdminId),
                User(Admin2Id),
                User(MemberId),
                User(ViewerId),
                User(OutsiderId),
                User(CandidateAId),
                User(CandidateBId),
                User(CandidateCId),

                new Workspace { Id = WorkspaceId, Name = "Members workspace" },

                Membership(OwnerId, WorkspaceRole.Owner),
                Membership(AdminId, WorkspaceRole.Admin),
                Membership(Admin2Id, WorkspaceRole.Admin),
                Membership(MemberId, WorkspaceRole.Member),
                Membership(ViewerId, WorkspaceRole.Viewer));

            await context.SaveChangesAsync();
        }

        private WorkspaceMember Membership(string userId, WorkspaceRole role) => new()
        {
            WorkspaceId = WorkspaceId,
            UserId = userId,
            Role = role,
            IsActive = true
        };

        private static string NewUserId(string label) => $"ws-{label}-{Guid.NewGuid():N}";

        private static ApplicationUser User(string id) => new()
        {
            Id = id,
            UserName = $"{id}@taskflow.test",
            NormalizedUserName = $"{id}@taskflow.test".ToUpperInvariant(),
            Email = $"{id}@taskflow.test",
            NormalizedEmail = $"{id}@taskflow.test".ToUpperInvariant(),
            FirstName = "Workspace",
            LastName = "Tester"
        };
    }
}
