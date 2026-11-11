using Microsoft.EntityFrameworkCore;
using TaskFlow.Data.Entities;
using TaskFlow.Data.Enums;
using AsyncTask = System.Threading.Tasks.Task;

namespace TaskFlow.Services.Tests;

[Collection(SqlServerIntegrationCollection.Name)]
public sealed class TaskCollaborationIntegrationTests
{
    private readonly SqlServerIntegrationFixture fixture;

    public TaskCollaborationIntegrationTests(SqlServerIntegrationFixture fixture)
    {
        this.fixture = fixture;
    }

    [Fact]
    [Trait("Category", "Integration")]
    public async AsyncTask PersistsThreadedCommentsAndAttachments()
    {
        await using var connection = fixture.CreateConnection();
        await connection.OpenAsync();
        var transaction = await connection.BeginTransactionAsync();
        var rootCommentId = Guid.NewGuid();
        var replyId = Guid.NewGuid();
        var taskAttachmentId = Guid.NewGuid();
        var commentAttachmentId = Guid.NewGuid();
        Exception? originalException = null;

        try
        {
            var graph = await Stage2TestData.CreateStage1GraphAsync(fixture, connection, transaction);
            await using (var context = fixture.CreateContext(connection))
            {
                context.Database.UseTransaction(transaction);
                context.AddRange(
                    new TaskComment
                    {
                        Id = rootCommentId,
                        TaskId = graph.TaskId,
                        UserId = graph.UserId,
                        Content = "Root comment",
                        CreatedAt = DateTime.UtcNow
                    },
                    new TaskComment
                    {
                        Id = replyId,
                        TaskId = graph.TaskId,
                        UserId = graph.UserId,
                        ParentCommentId = rootCommentId,
                        Content = "Threaded reply",
                        CreatedAt = DateTime.UtcNow
                    },
                    CreateAttachment(taskAttachmentId, graph, null),
                    CreateAttachment(commentAttachmentId, graph, rootCommentId));
                await context.SaveChangesAsync();
            }

            await using var readContext = fixture.CreateContext(connection);
            readContext.Database.UseTransaction(transaction);
            var root = await readContext.TaskComments
                .Include(c => c.Replies)
                .Include(c => c.Attachments)
                .SingleAsync(c => c.Id == rootCommentId);
            var reply = await readContext.TaskComments.SingleAsync(c => c.Id == replyId);
            var taskAttachment = await readContext.Attachments.SingleAsync(a => a.Id == taskAttachmentId);
            var commentAttachment = await readContext.Attachments.SingleAsync(a => a.Id == commentAttachmentId);

            Assert.Contains(root.Replies, c => c.Id == replyId);
            Assert.Equal(rootCommentId, reply.ParentCommentId);
            Assert.Contains(root.Attachments, a => a.Id == commentAttachmentId);
            Assert.Equal(graph.TaskId, taskAttachment.TaskId);
            Assert.Null(taskAttachment.TaskCommentId);
            Assert.Equal(rootCommentId, commentAttachment.TaskCommentId);
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
    public async AsyncTask PersistsNullableAndUserLinkedTaskHistory()
    {
        await using var connection = fixture.CreateConnection();
        await connection.OpenAsync();
        var transaction = await connection.BeginTransactionAsync();
        var userHistoryId = Guid.NewGuid();
        var systemHistoryId = Guid.NewGuid();
        Exception? originalException = null;

        try
        {
            var graph = await Stage2TestData.CreateStage1GraphAsync(fixture, connection, transaction);
            await using (var context = fixture.CreateContext(connection))
            {
                context.Database.UseTransaction(transaction);
                context.AddRange(
                    new TaskHistory
                    {
                        Id = userHistoryId,
                        TaskId = graph.TaskId,
                        UserId = graph.UserId,
                        ActionType = TaskHistoryActionType.Created,
                        Description = "Created by user",
                        CreatedAt = DateTime.UtcNow
                    },
                    new TaskHistory
                    {
                        Id = systemHistoryId,
                        TaskId = graph.TaskId,
                        UserId = null,
                        ActionType = TaskHistoryActionType.Updated,
                        Description = "Generated by system",
                        CreatedAt = DateTime.UtcNow
                    });
                await context.SaveChangesAsync();
            }

            var historyOnlyUserId = $"history-{Guid.NewGuid():N}";
            await using (var historyUserContext = fixture.CreateContext(connection))
            {
                historyUserContext.Database.UseTransaction(transaction);
                historyUserContext.Users.Add(new ApplicationUser
                {
                    Id = historyOnlyUserId,
                    UserName = $"{historyOnlyUserId}@example.test",
                    NormalizedUserName = historyOnlyUserId.ToUpperInvariant(),
                    Email = $"{historyOnlyUserId}@example.test",
                    NormalizedEmail = historyOnlyUserId.ToUpperInvariant(),
                    FirstName = "History",
                    LastName = "Only"
                });
                historyUserContext.TaskHistories.Add(new TaskHistory
                {
                    Id = Guid.NewGuid(),
                    TaskId = graph.TaskId,
                    UserId = historyOnlyUserId,
                    ActionType = TaskHistoryActionType.Updated,
                    CreatedAt = DateTime.UtcNow
                });
                await historyUserContext.SaveChangesAsync();
            }

            await using var readContext = fixture.CreateContext(connection);
            readContext.Database.UseTransaction(transaction);
            var userHistory = await readContext.TaskHistories.SingleAsync(h => h.Id == userHistoryId);
            var systemHistory = await readContext.TaskHistories.SingleAsync(h => h.Id == systemHistoryId);
            Assert.Equal(graph.UserId, userHistory.UserId);
            Assert.Null(systemHistory.UserId);

            await using var deleteUserCommand = connection.CreateCommand();
            deleteUserCommand.Transaction = (Microsoft.Data.SqlClient.SqlTransaction)transaction;
            deleteUserCommand.CommandText = "DELETE FROM [AspNetUsers] WHERE [Id] = @userId";
            var parameter = deleteUserCommand.CreateParameter();
            parameter.ParameterName = "@userId";
            parameter.Value = historyOnlyUserId;
            deleteUserCommand.Parameters.Add(parameter);
            Assert.Equal(1, await deleteUserCommand.ExecuteNonQueryAsync());

            await using var verifyContext = fixture.CreateContext(connection);
            verifyContext.Database.UseTransaction(transaction);
            var nulledHistory = await verifyContext.TaskHistories
                .SingleAsync(h => h.UserId == null && h.TaskId == graph.TaskId && h.Id != systemHistoryId);
            Assert.Null(nulledHistory.UserId);
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
    public async AsyncTask DirectSqlTaskDeleteCascadesConfiguredCollaborationRows()
    {
        await using var connection = fixture.CreateConnection();
        await connection.OpenAsync();
        var transaction = await connection.BeginTransactionAsync();
        var commentId = Guid.NewGuid();
        var attachmentId = Guid.NewGuid();
        var historyId = Guid.NewGuid();
        Exception? originalException = null;

        try
        {
            var graph = await Stage2TestData.CreateStage1GraphAsync(fixture, connection, transaction);
            await using (var context = fixture.CreateContext(connection))
            {
                context.Database.UseTransaction(transaction);
                context.AddRange(
                    new TaskComment
                    {
                        Id = commentId,
                        TaskId = graph.TaskId,
                        UserId = graph.UserId,
                        Content = "Cascade comment",
                        CreatedAt = DateTime.UtcNow
                    },
                    CreateAttachment(attachmentId, graph, null),
                    new TaskHistory
                    {
                        Id = historyId,
                        TaskId = graph.TaskId,
                        UserId = null,
                        ActionType = TaskHistoryActionType.Created,
                        CreatedAt = DateTime.UtcNow
                    });
                await context.SaveChangesAsync();
            }

            await using var deleteCommand = connection.CreateCommand();
            deleteCommand.Transaction = (Microsoft.Data.SqlClient.SqlTransaction)transaction;
            deleteCommand.CommandText = "DELETE FROM [Tasks] WHERE [Id] = @taskId";
            var parameter = deleteCommand.CreateParameter();
            parameter.ParameterName = "@taskId";
            parameter.Value = graph.TaskId;
            deleteCommand.Parameters.Add(parameter);
            Assert.Equal(1, await deleteCommand.ExecuteNonQueryAsync());

            await using var verifyContext = fixture.CreateContext(connection);
            verifyContext.Database.UseTransaction(transaction);
            Assert.False(await verifyContext.TaskComments.AnyAsync(c => c.Id == commentId));
            Assert.False(await verifyContext.Attachments.AnyAsync(a => a.Id == attachmentId));
            Assert.False(await verifyContext.TaskHistories.AnyAsync(h => h.Id == historyId));
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

    private static Attachment CreateAttachment(Guid id, Stage2Graph graph, Guid? commentId)
    {
        return new Attachment
        {
            Id = id,
            TaskId = graph.TaskId,
            TaskCommentId = commentId,
            UploadedByUserId = graph.UserId,
            OriginalFileName = "stage2.txt",
            StoredFileName = $"{id:N}.txt",
            StoragePath = $"stage2/{id:N}.txt",
            ContentType = "text/plain",
            FileSizeBytes = 10
        };
    }
}
