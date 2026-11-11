using System.Data.Common;
using Microsoft.Data.SqlClient;
using Microsoft.EntityFrameworkCore;
using TaskFlow.Data.Entities;
using TaskFlow.Data.Enums;
using TaskEntity = TaskFlow.Data.Entities.Task;
using AsyncTask = System.Threading.Tasks.Task;

namespace TaskFlow.Services.Tests;

internal sealed record Stage3Prerequisites(string SecondaryUserId, Guid CommentId);
internal sealed record Stage3TaskGraph(string UserId, Guid WorkspaceId, Guid ProjectId, Guid TaskId);

internal static class Stage3TestData
{
    public static async System.Threading.Tasks.Task<Stage3Prerequisites> CreateAsync(
        SqlServerIntegrationFixture fixture,
        SqlConnection connection,
        DbTransaction transaction,
        Stage2Graph graph)
    {
        var secondaryUserId = $"stage3-{Guid.NewGuid():N}";
        var commentId = Guid.NewGuid();

        await using var context = fixture.CreateContext(connection);
        context.Database.UseTransaction(transaction);
        context.AddRange(
            new ApplicationUser
            {
                Id = secondaryUserId,
                UserName = $"{secondaryUserId}@example.test",
                NormalizedUserName = secondaryUserId.ToUpperInvariant(),
                Email = $"{secondaryUserId}@example.test",
                NormalizedEmail = secondaryUserId.ToUpperInvariant(),
                FirstName = "Stage",
                LastName = "Three"
            },
            new TaskComment
            {
                Id = commentId,
                TaskId = graph.TaskId,
                UserId = graph.UserId,
                Content = "Stage 3 mention comment",
                CreatedAt = DateTime.UtcNow
            });
        await context.SaveChangesAsync();

        return new Stage3Prerequisites(secondaryUserId, commentId);
    }

    public static async System.Threading.Tasks.Task<string> CreateUserAsync(
        SqlServerIntegrationFixture fixture,
        SqlConnection connection,
        DbTransaction transaction)
    {
        var userId = $"stage3-{Guid.NewGuid():N}";
        await using var context = fixture.CreateContext(connection);
        context.Database.UseTransaction(transaction);
        context.Users.Add(new ApplicationUser
        {
            Id = userId,
            UserName = $"{userId}@example.test",
            NormalizedUserName = userId.ToUpperInvariant(),
            Email = $"{userId}@example.test",
            NormalizedEmail = userId.ToUpperInvariant(),
            FirstName = "Stage",
            LastName = "Three"
        });
        await context.SaveChangesAsync();
        return userId;
    }

    public static async System.Threading.Tasks.Task<Guid> CreateWorkspaceAsync(
        SqlServerIntegrationFixture fixture,
        SqlConnection connection,
        DbTransaction transaction)
    {
        var workspaceId = Guid.NewGuid();
        await using var context = fixture.CreateContext(connection);
        context.Database.UseTransaction(transaction);
        context.Workspaces.Add(new Workspace { Id = workspaceId, Name = $"Stage 3 Workspace {workspaceId:N}" });
        await context.SaveChangesAsync();
        return workspaceId;
    }

    public static async System.Threading.Tasks.Task<(Guid WorkspaceId, Guid TeamId, Guid ProjectId)> CreateProjectAsync(
        SqlServerIntegrationFixture fixture,
        SqlConnection connection,
        DbTransaction transaction)
    {
        var workspaceId = Guid.NewGuid();
        var teamId = Guid.NewGuid();
        var projectId = Guid.NewGuid();
        await using var context = fixture.CreateContext(connection);
        context.Database.UseTransaction(transaction);
        context.AddRange(
            new Workspace { Id = workspaceId, Name = $"Stage 3 Workspace {workspaceId:N}" },
            new Team { Id = teamId, WorkspaceId = workspaceId, Name = $"Stage 3 Team {teamId:N}" },
            new Project { Id = projectId, TeamId = teamId, Name = $"Stage 3 Project {projectId:N}", Status = ProjectStatus.Active });
        await context.SaveChangesAsync();
        return (workspaceId, teamId, projectId);
    }

    public static async System.Threading.Tasks.Task<Stage3TaskGraph> CreateTaskAsync(
        SqlServerIntegrationFixture fixture,
        SqlConnection connection,
        DbTransaction transaction)
    {
        var userId = await CreateUserAsync(fixture, connection, transaction);
        var workspaceId = Guid.NewGuid();
        var teamId = Guid.NewGuid();
        var projectId = Guid.NewGuid();
        var boardId = Guid.NewGuid();
        var columnId = Guid.NewGuid();
        var taskId = Guid.NewGuid();
        await using var context = fixture.CreateContext(connection);
        context.Database.UseTransaction(transaction);
        context.AddRange(
            new Workspace { Id = workspaceId, Name = $"Stage 3 Workspace {workspaceId:N}" },
            new Team { Id = teamId, WorkspaceId = workspaceId, Name = $"Stage 3 Team {teamId:N}" },
            new Project { Id = projectId, TeamId = teamId, Name = $"Stage 3 Project {projectId:N}", Status = ProjectStatus.Active },
            new Board { Id = boardId, ProjectId = projectId, Name = "Stage 3 Board", IsDefault = true },
            new BoardColumn { Id = columnId, BoardId = boardId, Name = "Stage 3 Column", Position = 1, IsDefault = true },
            new TaskEntity { Id = taskId, BoardColumnId = columnId, CreatedByUserId = userId, Title = "Stage 3 Task", Priority = TaskPriority.Medium, Position = 1 });
        await context.SaveChangesAsync();
        return new Stage3TaskGraph(userId, workspaceId, projectId, taskId);
    }

    public static async AsyncTask CleanupAsync(DbTransaction transaction, Exception? originalException)
    {
        try
        {
            await transaction.RollbackAsync();
        }
        catch when (originalException is not null)
        {
        }
        finally
        {
            try
            {
                await transaction.DisposeAsync();
            }
            catch when (originalException is not null)
            {
            }
        }
    }

    public static void AssertUniqueViolation(DbUpdateException exception)
    {
        Stage2Assertions.AssertUniqueViolation(exception);
    }

    public static void AssertCheckConstraintViolation(DbUpdateException exception)
    {
        var sqlException = exception.InnerException as SqlException
            ?? exception.InnerException?.InnerException as SqlException;

        Assert.NotNull(sqlException);
        Assert.Equal(547, sqlException.Number);
    }
}
