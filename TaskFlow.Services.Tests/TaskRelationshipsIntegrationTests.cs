using Microsoft.Data.SqlClient;
using Microsoft.EntityFrameworkCore;
using TaskFlow.Data.Context;
using TaskFlow.Data.Entities;
using TaskFlow.Data.Enums;
using AsyncTask = System.Threading.Tasks.Task;
using TaskEntity = TaskFlow.Data.Entities.Task;

namespace TaskFlow.Services.Tests;

[Collection(SqlServerIntegrationCollection.Name)]
public sealed class TaskRelationshipsIntegrationTests
{
    private readonly SqlServerIntegrationFixture fixture;

    public TaskRelationshipsIntegrationTests(SqlServerIntegrationFixture fixture)
    {
        this.fixture = fixture;
    }

    [Fact]
    [Trait("Category", "Integration")]
    public async AsyncTask PersistsTaskAssigneeAndRejectsDuplicateAssignment()
    {
        await using var connection = fixture.CreateConnection();
        await connection.OpenAsync();
        var transaction = await connection.BeginTransactionAsync();
        var assigneeId = Guid.NewGuid();
        Exception? originalException = null;

        try
        {
            var graph = await Stage2TestData.CreateStage1GraphAsync(fixture, connection, transaction);
            await using (var context = fixture.CreateContext(connection))
            {
                context.Database.UseTransaction(transaction);
                context.TaskAssignees.Add(new TaskAssignee
                {
                    Id = assigneeId,
                    TaskId = graph.TaskId,
                    UserId = graph.UserId,
                    AssignedAt = DateTime.UtcNow,
                    AssignedByUserId = graph.UserId
                });
                await context.SaveChangesAsync();
            }

            await using (var readContext = fixture.CreateContext(connection))
            {
                readContext.Database.UseTransaction(transaction);
                var assignee = await readContext.TaskAssignees.SingleAsync(a => a.Id == assigneeId);
                Assert.Equal(graph.TaskId, assignee.TaskId);
                Assert.Equal(graph.UserId, assignee.UserId);
                Assert.Equal(graph.UserId, assignee.AssignedByUserId);
            }

            await using var duplicateContext = fixture.CreateContext(connection);
            duplicateContext.Database.UseTransaction(transaction);
            duplicateContext.TaskAssignees.Add(new TaskAssignee
            {
                Id = Guid.NewGuid(),
                TaskId = graph.TaskId,
                UserId = graph.UserId,
                AssignedAt = DateTime.UtcNow,
                AssignedByUserId = graph.UserId
            });

            var exception = await Assert.ThrowsAsync<DbUpdateException>(() => duplicateContext.SaveChangesAsync());
            Stage2Assertions.AssertUniqueViolation(exception);
        }
        catch (Exception exception)
        {
            originalException = exception;
            throw;
        }
        finally
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
    }

    [Fact]
    [Trait("Category", "Integration")]
    public async AsyncTask PersistsSubtaskAndRejectsDuplicatePosition()
    {
        await using var connection = fixture.CreateConnection();
        await connection.OpenAsync();
        var transaction = await connection.BeginTransactionAsync();
        var subtaskId = Guid.NewGuid();
        Exception? originalException = null;

        try
        {
            var graph = await Stage2TestData.CreateStage1GraphAsync(fixture, connection, transaction);
            await using (var context = fixture.CreateContext(connection))
            {
                context.Database.UseTransaction(transaction);
                context.Subtasks.Add(new Subtask
                {
                    Id = subtaskId,
                    TaskId = graph.TaskId,
                    Title = "Stage 2 subtask",
                    Position = 1,
                    CreatedAt = DateTime.UtcNow
                });
                await context.SaveChangesAsync();
            }

            await using var duplicateContext = fixture.CreateContext(connection);
            duplicateContext.Database.UseTransaction(transaction);
            duplicateContext.Subtasks.Add(new Subtask
            {
                Id = Guid.NewGuid(),
                TaskId = graph.TaskId,
                Title = "Duplicate position",
                Position = 1,
                CreatedAt = DateTime.UtcNow
            });

            var exception = await Assert.ThrowsAsync<DbUpdateException>(() => duplicateContext.SaveChangesAsync());
            Stage2Assertions.AssertUniqueViolation(exception);
        }
        catch (Exception exception)
        {
            originalException = exception;
            throw;
        }
        finally
        {
            try
            {
                await transaction.RollbackAsync();
            }
            catch when (originalException is not null)
            {
            }
        }
    }

    [Fact]
    [Trait("Category", "Integration")]
    public async AsyncTask PersistsLabelsAndTaskLabelAndRejectsConfiguredDuplicates()
    {
        await using var connection = fixture.CreateConnection();
        await connection.OpenAsync();
        var transaction = await connection.BeginTransactionAsync();
        var secondWorkspaceId = Guid.NewGuid();
        var labelId = Guid.NewGuid();
        var taskLabelId = Guid.NewGuid();
        Exception? originalException = null;

        try
        {
            var graph = await Stage2TestData.CreateStage1GraphAsync(fixture, connection, transaction);
            await using (var context = fixture.CreateContext(connection))
            {
                context.Database.UseTransaction(transaction);
                context.Workspaces.Add(new Workspace
                {
                    Id = secondWorkspaceId,
                    Name = $"Stage 2 Other Workspace {secondWorkspaceId:N}"
                });
                context.Labels.Add(new Label
                {
                    Id = labelId,
                    WorkspaceId = graph.WorkspaceId,
                    Name = "Stage 2 Label",
                    Color = "#123456",
                    CreatedAt = DateTime.UtcNow
                });
                context.TaskLabels.Add(new TaskLabel
                {
                    Id = taskLabelId,
                    TaskId = graph.TaskId,
                    LabelId = labelId,
                    AssignedAt = DateTime.UtcNow
                });
                await context.SaveChangesAsync();
            }

            await using (var readContext = fixture.CreateContext(connection))
            {
                readContext.Database.UseTransaction(transaction);
                var taskLabel = await readContext.TaskLabels
                    .Include(tl => tl.Task)
                    .Include(tl => tl.Label)
                    .SingleAsync(tl => tl.Id == taskLabelId);
                Assert.Equal(graph.TaskId, taskLabel.TaskId);
                Assert.Equal(labelId, taskLabel.LabelId);
                Assert.Equal(graph.WorkspaceId, taskLabel.Label.WorkspaceId);
            }

            await using (var duplicateLabelContext = fixture.CreateContext(connection))
            {
                duplicateLabelContext.Database.UseTransaction(transaction);
                duplicateLabelContext.Labels.Add(new Label
                {
                    Id = Guid.NewGuid(),
                    WorkspaceId = graph.WorkspaceId,
                    Name = "Stage 2 Label",
                    Color = "#654321",
                    CreatedAt = DateTime.UtcNow
                });
                var exception = await Assert.ThrowsAsync<DbUpdateException>(() => duplicateLabelContext.SaveChangesAsync());
                Stage2Assertions.AssertUniqueViolation(exception);
            }

            await using (var sameNameOtherWorkspaceContext = fixture.CreateContext(connection))
            {
                sameNameOtherWorkspaceContext.Database.UseTransaction(transaction);
                sameNameOtherWorkspaceContext.Labels.Add(new Label
                {
                    Id = Guid.NewGuid(),
                    WorkspaceId = secondWorkspaceId,
                    Name = "Stage 2 Label",
                    Color = "#abcdef",
                    CreatedAt = DateTime.UtcNow
                });
                await sameNameOtherWorkspaceContext.SaveChangesAsync();
            }

            await using var duplicateTaskLabelContext = fixture.CreateContext(connection);
            duplicateTaskLabelContext.Database.UseTransaction(transaction);
            duplicateTaskLabelContext.TaskLabels.Add(new TaskLabel
            {
                Id = Guid.NewGuid(),
                TaskId = graph.TaskId,
                LabelId = labelId,
                AssignedAt = DateTime.UtcNow
            });
            var taskLabelException = await Assert.ThrowsAsync<DbUpdateException>(() => duplicateTaskLabelContext.SaveChangesAsync());
            Stage2Assertions.AssertUniqueViolation(taskLabelException);
        }
        catch (Exception exception)
        {
            originalException = exception;
            throw;
        }
        finally
        {
            try
            {
                await transaction.RollbackAsync();
            }
            catch when (originalException is not null)
            {
            }
        }
    }

    [Fact]
    [Trait("Category", "Integration")]
    public async AsyncTask PersistsTaskDependenciesAndUsesDependencyTypeInTheUniqueKey()
    {
        await using var connection = fixture.CreateConnection();
        await connection.OpenAsync();
        var transaction = await connection.BeginTransactionAsync();
        var secondTaskId = Guid.NewGuid();
        var dependencyId = Guid.NewGuid();
        Exception? originalException = null;

        try
        {
            var graph = await Stage2TestData.CreateStage1GraphAsync(fixture, connection, transaction);
            await using (var context = fixture.CreateContext(connection))
            {
                context.Database.UseTransaction(transaction);
                context.Tasks.Add(new TaskEntity
                {
                    Id = secondTaskId,
                    BoardColumnId = graph.ColumnId,
                    CreatedByUserId = graph.UserId,
                    Title = "Stage 2 successor task",
                    Priority = TaskPriority.Medium,
                    Position = 2
                });
                context.TaskDependencies.Add(new TaskDependency
                {
                    Id = dependencyId,
                    PredecessorTaskId = graph.TaskId,
                    SuccessorTaskId = secondTaskId,
                    DependencyType = DependencyType.FinishToStart,
                    LagMinutes = 15,
                    CreatedAt = DateTime.UtcNow
                });
                await context.SaveChangesAsync();
            }

            await using (var readContext = fixture.CreateContext(connection))
            {
                readContext.Database.UseTransaction(transaction);
                var dependency = await readContext.TaskDependencies
                    .Include(d => d.PredecessorTask)
                    .Include(d => d.SuccessorTask)
                    .SingleAsync(d => d.Id == dependencyId);
                Assert.Equal(graph.TaskId, dependency.PredecessorTaskId);
                Assert.Equal(secondTaskId, dependency.SuccessorTaskId);
                Assert.Equal(DependencyType.FinishToStart, dependency.DependencyType);
            }

            await using (var differentTypeContext = fixture.CreateContext(connection))
            {
                differentTypeContext.Database.UseTransaction(transaction);
                differentTypeContext.TaskDependencies.Add(new TaskDependency
                {
                    Id = Guid.NewGuid(),
                    PredecessorTaskId = graph.TaskId,
                    SuccessorTaskId = secondTaskId,
                    DependencyType = DependencyType.StartToStart,
                    LagMinutes = 0,
                    CreatedAt = DateTime.UtcNow
                });
                await differentTypeContext.SaveChangesAsync();
            }

            await using var duplicateContext = fixture.CreateContext(connection);
            duplicateContext.Database.UseTransaction(transaction);
            duplicateContext.TaskDependencies.Add(new TaskDependency
            {
                Id = Guid.NewGuid(),
                PredecessorTaskId = graph.TaskId,
                SuccessorTaskId = secondTaskId,
                DependencyType = DependencyType.FinishToStart,
                LagMinutes = 30,
                CreatedAt = DateTime.UtcNow
            });
            var exception = await Assert.ThrowsAsync<DbUpdateException>(() => duplicateContext.SaveChangesAsync());
            Stage2Assertions.AssertUniqueViolation(exception);
        }
        catch (Exception exception)
        {
            originalException = exception;
            throw;
        }
        finally
        {
            try
            {
                await transaction.RollbackAsync();
            }
            catch when (originalException is not null)
            {
            }
        }
    }

    [Fact]
    [Trait("Category", "Integration")]
    public async AsyncTask PersistsChecklistItemAndRejectsDuplicatePosition()
    {
        await using var connection = fixture.CreateConnection();
        await connection.OpenAsync();
        var transaction = await connection.BeginTransactionAsync();
        Exception? originalException = null;

        try
        {
            var graph = await Stage2TestData.CreateStage1GraphAsync(fixture, connection, transaction);
            await using (var context = fixture.CreateContext(connection))
            {
                context.Database.UseTransaction(transaction);
                context.ChecklistItems.Add(new ChecklistItem
                {
                    Id = Guid.NewGuid(),
                    TaskId = graph.TaskId,
                    Title = "Stage 2 checklist item",
                    Position = 1,
                    CreatedAt = DateTime.UtcNow
                });
                await context.SaveChangesAsync();
            }

            await using var duplicateContext = fixture.CreateContext(connection);
            duplicateContext.Database.UseTransaction(transaction);
            duplicateContext.ChecklistItems.Add(new ChecklistItem
            {
                Id = Guid.NewGuid(),
                TaskId = graph.TaskId,
                Title = "Duplicate checklist position",
                Position = 1,
                CreatedAt = DateTime.UtcNow
            });
            var exception = await Assert.ThrowsAsync<DbUpdateException>(() => duplicateContext.SaveChangesAsync());
            Stage2Assertions.AssertUniqueViolation(exception);
        }
        catch (Exception exception)
        {
            originalException = exception;
            throw;
        }
        finally
        {
            try
            {
                await transaction.RollbackAsync();
            }
            catch when (originalException is not null)
            {
            }
        }
    }
}

internal sealed record Stage2Graph(
    string UserId,
    Guid WorkspaceId,
    Guid TeamId,
    Guid ProjectId,
    Guid BoardId,
    Guid ColumnId,
    Guid TaskId);

internal static class Stage2TestData
{
    public static async System.Threading.Tasks.Task<Stage2Graph> CreateStage1GraphAsync(
        SqlServerIntegrationFixture fixture,
        SqlConnection connection,
        System.Data.Common.DbTransaction transaction)
    {
        var userId = $"stage2-{Guid.NewGuid():N}";
        var workspaceId = Guid.NewGuid();
        var teamId = Guid.NewGuid();
        var projectId = Guid.NewGuid();
        var boardId = Guid.NewGuid();
        var columnId = Guid.NewGuid();
        var taskId = Guid.NewGuid();

        await using var context = fixture.CreateContext(connection);
        context.Database.UseTransaction(transaction);
        context.AddRange(
            new ApplicationUser
            {
                Id = userId,
                UserName = $"{userId}@example.test",
                NormalizedUserName = userId.ToUpperInvariant(),
                Email = $"{userId}@example.test",
                NormalizedEmail = userId.ToUpperInvariant(),
                FirstName = "Stage",
                LastName = "Two"
            },
            new Workspace
            {
                Id = workspaceId,
                Name = $"Stage 2 Workspace {workspaceId:N}"
            },
            new WorkspaceMember
            {
                Id = Guid.NewGuid(),
                WorkspaceId = workspaceId,
                UserId = userId,
                Role = WorkspaceRole.Owner
            },
            new Team
            {
                Id = teamId,
                WorkspaceId = workspaceId,
                Name = $"Stage 2 Team {teamId:N}"
            },
            new TeamMember
            {
                Id = Guid.NewGuid(),
                TeamId = teamId,
                UserId = userId,
                Role = TeamRole.Lead
            },
            new Project
            {
                Id = projectId,
                TeamId = teamId,
                Name = $"Stage 2 Project {projectId:N}",
                Status = ProjectStatus.Active
            },
            new ProjectMember
            {
                Id = Guid.NewGuid(),
                ProjectId = projectId,
                UserId = userId,
                Role = ProjectRole.Manager
            },
            new Board
            {
                Id = boardId,
                ProjectId = projectId,
                Name = $"Stage 2 Board {boardId:N}",
                IsDefault = true
            },
            new BoardColumn
            {
                Id = columnId,
                BoardId = boardId,
                Name = "Stage 2 Column",
                Position = 1,
                IsDefault = true
            },
            new TaskEntity
            {
                Id = taskId,
                BoardColumnId = columnId,
                CreatedByUserId = userId,
                Title = "Stage 2 task",
                Priority = TaskPriority.Medium,
                Position = 1
            });
        await context.SaveChangesAsync();

        return new Stage2Graph(userId, workspaceId, teamId, projectId, boardId, columnId, taskId);
    }
}

internal static class Stage2Assertions
{
    public static void AssertUniqueViolation(DbUpdateException exception)
    {
        var sqlException = exception.InnerException as SqlException
            ?? exception.InnerException?.InnerException as SqlException;

        Assert.NotNull(sqlException);
        Assert.Contains(sqlException.Number, new[] { 2601, 2627 });
    }
}
