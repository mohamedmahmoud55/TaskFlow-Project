using Microsoft.EntityFrameworkCore;
using TaskFlow.Data.Entities;
using AsyncTask = System.Threading.Tasks.Task;
using TaskEntity = TaskFlow.Data.Entities.Task;

namespace TaskFlow.Services.Tests;

[Collection(SqlServerIntegrationCollection.Name)]
public sealed class TaskTimeTrackingIntegrationTests
{
    private readonly SqlServerIntegrationFixture fixture;

    public TaskTimeTrackingIntegrationTests(SqlServerIntegrationFixture fixture)
    {
        this.fixture = fixture;
    }

    [Fact]
    [Trait("Category", "Integration")]
    public async AsyncTask PersistsTimeEntryAndTimerRecords()
    {
        await using var connection = fixture.CreateConnection();
        await connection.OpenAsync();
        var transaction = await connection.BeginTransactionAsync();
        var start = DateTime.UtcNow.AddMinutes(-30);
        Exception? originalException = null;

        try
        {
            var graph = await Stage2TestData.CreateStage1GraphAsync(fixture, connection, transaction);
            await using (var context = fixture.CreateContext(connection))
            {
                context.Database.UseTransaction(transaction);
                context.AddRange(
                    new TimeEntry
                    {
                        Id = Guid.NewGuid(),
                        TaskId = graph.TaskId,
                        UserId = graph.UserId,
                        Description = "Stage 2 time entry",
                        StartTime = start,
                        EndTime = start.AddMinutes(30),
                        DurationMinutes = 30
                    },
                    new TaskTimer
                    {
                        Id = Guid.NewGuid(),
                        TaskId = graph.TaskId,
                        UserId = graph.UserId,
                        StartedAt = start,
                        TotalPausedSeconds = 0,
                        IsRunning = true,
                        IsActive = true
                    });
                await context.SaveChangesAsync();
            }

            await using var readContext = fixture.CreateContext(connection);
            readContext.Database.UseTransaction(transaction);
            var timeEntry = await readContext.TimeEntries.SingleAsync(e => e.TaskId == graph.TaskId);
            var timer = await readContext.TaskTimers.SingleAsync(t => t.TaskId == graph.TaskId);
            Assert.Equal(graph.UserId, timeEntry.UserId);
            Assert.Equal(30, timeEntry.DurationMinutes);
            Assert.Equal(graph.UserId, timer.UserId);
            Assert.True(timer.IsActive);
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
    public async AsyncTask AllowsMultipleInactiveTimersButRejectsSecondActiveTimerForTheSameUser()
    {
        await using var connection = fixture.CreateConnection();
        await connection.OpenAsync();
        var transaction = await connection.BeginTransactionAsync();
        var firstTaskId = Guid.NewGuid();
        var secondTaskId = Guid.NewGuid();
        Exception? originalException = null;

        try
        {
            var graph = await Stage2TestData.CreateStage1GraphAsync(fixture, connection, transaction);
            await using (var context = fixture.CreateContext(connection))
            {
                context.Database.UseTransaction(transaction);
                context.Tasks.AddRange(
                    CreateAdditionalTask(firstTaskId, graph, 2),
                    CreateAdditionalTask(secondTaskId, graph, 3));
                context.TaskTimers.AddRange(
                    CreateTimer(Guid.NewGuid(), firstTaskId, graph.UserId, false),
                    CreateTimer(Guid.NewGuid(), secondTaskId, graph.UserId, false),
                    CreateTimer(Guid.NewGuid(), graph.TaskId, graph.UserId, true));
                await context.SaveChangesAsync();
            }

            await using var duplicateContext = fixture.CreateContext(connection);
            duplicateContext.Database.UseTransaction(transaction);
            duplicateContext.TaskTimers.Add(new TaskTimer
            {
                Id = Guid.NewGuid(),
                TaskId = firstTaskId,
                UserId = graph.UserId,
                StartedAt = DateTime.UtcNow,
                TotalPausedSeconds = 0,
                IsRunning = true,
                IsActive = true
            });
            var exception = await Assert.ThrowsAsync<DbUpdateException>(() => duplicateContext.SaveChangesAsync());
            Stage2Assertions.AssertUniqueViolation(exception);

            await using var verifyContext = fixture.CreateContext(connection);
            verifyContext.Database.UseTransaction(transaction);
            var inactiveTimers = await verifyContext.TaskTimers
                .Where(t => t.UserId == graph.UserId && !t.IsActive)
                .CountAsync();
            Assert.Equal(2, inactiveTimers);
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

    private static TaskEntity CreateAdditionalTask(Guid taskId, Stage2Graph graph, int position)
    {
        return new TaskEntity
        {
            Id = taskId,
            BoardColumnId = graph.ColumnId,
            CreatedByUserId = graph.UserId,
            Title = $"Stage 2 timer task {position}",
            Priority = TaskFlow.Data.Enums.TaskPriority.Medium,
            Position = position
        };
    }

    private static TaskTimer CreateTimer(Guid id, Guid taskId, string userId, bool isActive)
    {
        return new TaskTimer
        {
            Id = id,
            TaskId = taskId,
            UserId = userId,
            StartedAt = DateTime.UtcNow,
            TotalPausedSeconds = 0,
            IsRunning = isActive,
            IsActive = isActive
        };
    }
}
