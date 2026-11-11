using Microsoft.EntityFrameworkCore;
using AsyncTask = System.Threading.Tasks.Task;
using TaskFlow.Data.Context;
using TaskFlow.Data.Entities;
using TaskFlow.Data.Enums;

namespace TaskFlow.Services.Tests;

[Collection(SqlServerIntegrationCollection.Name)]
public sealed class Stage1PersistenceIntegrationTests
{
    private readonly SqlServerIntegrationFixture fixture;

    public Stage1PersistenceIntegrationTests(SqlServerIntegrationFixture fixture)
    {
        this.fixture = fixture;
    }

    [Fact]
    [Trait("Category", "Integration")]
    public async AsyncTask TestDatabaseConnectionTargetsTheRequiredDisposableDatabase()
    {
        await using var connection = fixture.CreateConnection();
        await connection.OpenAsync();

        Assert.Equal(SqlServerIntegrationFixture.RequiredDatabaseName, connection.Database);
    }

    [Fact]
    [Trait("Category", "Integration")]
    public async AsyncTask PersistsAndReloadsTheStage1WorkspaceHierarchyAcrossContexts()
    {
        await using var connection = fixture.CreateConnection();
        await connection.OpenAsync();
        await using var transaction = await connection.BeginTransactionAsync();

        try
        {
            var userId = $"stage1-{Guid.NewGuid():N}";
            var workspaceId = Guid.NewGuid();
            var teamId = Guid.NewGuid();
            var projectId = Guid.NewGuid();
            var boardId = Guid.NewGuid();
            var columnId = Guid.NewGuid();
            var taskId = Guid.NewGuid();

            await using (var writeContext = fixture.CreateContext(connection))
            {
                writeContext.Database.UseTransaction(transaction);

                var user = new ApplicationUser
                {
                    Id = userId,
                    UserName = $"{userId}@example.test",
                    NormalizedUserName = userId.ToUpperInvariant(),
                    Email = $"{userId}@example.test",
                    NormalizedEmail = userId.ToUpperInvariant(),
                    FirstName = "Stage",
                    LastName = "One"
                };

                var workspace = new Workspace
                {
                    Id = workspaceId,
                    Name = $"Stage 1 Workspace {workspaceId:N}",
                    Description = "Disposable SQL Server integration-test workspace"
                };

                var workspaceMember = new WorkspaceMember
                {
                    Id = Guid.NewGuid(),
                    WorkspaceId = workspaceId,
                    UserId = userId,
                    Role = WorkspaceRole.Owner
                };

                var team = new Team
                {
                    Id = teamId,
                    WorkspaceId = workspaceId,
                    Name = $"Stage 1 Team {teamId:N}"
                };

                var teamMember = new TeamMember
                {
                    Id = Guid.NewGuid(),
                    TeamId = teamId,
                    UserId = userId,
                    Role = TeamRole.Lead
                };

                var project = new Project
                {
                    Id = projectId,
                    TeamId = teamId,
                    Name = $"Stage 1 Project {projectId:N}",
                    Status = ProjectStatus.Active
                };

                var projectMember = new ProjectMember
                {
                    Id = Guid.NewGuid(),
                    ProjectId = projectId,
                    UserId = userId,
                    Role = ProjectRole.Manager
                };

                var board = new Board
                {
                    Id = boardId,
                    ProjectId = projectId,
                    Name = $"Stage 1 Board {boardId:N}",
                    IsDefault = true
                };

                var boardColumn = new BoardColumn
                {
                    Id = columnId,
                    BoardId = boardId,
                    Name = "Stage 1 Column",
                    Position = 1,
                    IsDefault = true
                };

                var task = new TaskFlow.Data.Entities.Task
                {
                    Id = taskId,
                    BoardColumnId = columnId,
                    CreatedByUserId = userId,
                    Title = "Stage 1 persisted task",
                    Priority = TaskPriority.High,
                    Position = 1
                };

                writeContext.AddRange(
                    user,
                    workspace,
                    workspaceMember,
                    team,
                    teamMember,
                    project,
                    projectMember,
                    board,
                    boardColumn,
                    task);

                await writeContext.SaveChangesAsync();
            }

            await using (var readContext = fixture.CreateContext(connection))
            {
                readContext.Database.UseTransaction(transaction);

                var user = await readContext.Users.SingleAsync(u => u.Id == userId);
                var workspace = await readContext.Workspaces
                    .Include(w => w.Members)
                    .Include(w => w.Teams)
                    .SingleAsync(w => w.Id == workspaceId);
                var workspaceMember = await readContext.WorkspaceMembers
                    .SingleAsync(m => m.WorkspaceId == workspaceId && m.UserId == userId);
                var team = await readContext.Teams
                    .Include(t => t.Workspace)
                    .Include(t => t.Members)
                    .Include(t => t.Projects)
                    .SingleAsync(t => t.Id == teamId);
                var teamMember = await readContext.TeamMembers
                    .SingleAsync(m => m.TeamId == teamId && m.UserId == userId);
                var project = await readContext.Projects
                    .Include(p => p.Team)
                    .Include(p => p.Members)
                    .Include(p => p.Boards)
                    .SingleAsync(p => p.Id == projectId);
                var projectMember = await readContext.ProjectMembers
                    .SingleAsync(m => m.ProjectId == projectId && m.UserId == userId);
                var board = await readContext.Boards
                    .Include(b => b.Project)
                    .Include(b => b.Columns)
                    .SingleAsync(b => b.Id == boardId);
                var boardColumn = await readContext.BoardColumns
                    .Include(c => c.Board)
                    .Include(c => c.Tasks)
                    .SingleAsync(c => c.Id == columnId);
                var task = await readContext.Tasks
                    .Include(t => t.BoardColumn)
                    .Include(t => t.CreatedByUser)
                    .SingleAsync(t => t.Id == taskId);

                Assert.Equal(userId, user.Id);
                Assert.Equal(workspaceId, workspaceMember.WorkspaceId);
                Assert.Equal(userId, workspaceMember.UserId);
                Assert.Equal(workspaceId, team.WorkspaceId);
                Assert.Equal(workspaceId, team.Workspace.Id);
                Assert.Equal(teamId, teamMember.TeamId);
                Assert.Equal(userId, teamMember.UserId);
                Assert.Equal(teamId, project.TeamId);
                Assert.Equal(teamId, project.Team.Id);
                Assert.Equal(projectId, projectMember.ProjectId);
                Assert.Equal(userId, projectMember.UserId);
                Assert.Equal(projectId, board.ProjectId);
                Assert.Equal(projectId, board.Project.Id);
                Assert.Equal(boardId, boardColumn.BoardId);
                Assert.Equal(boardId, boardColumn.Board.Id);
                Assert.Equal(columnId, task.BoardColumnId);
                Assert.Equal(columnId, task.BoardColumn.Id);
                Assert.Equal(userId, task.CreatedByUserId);
                Assert.Equal(userId, task.CreatedByUser.Id);
                Assert.Contains(workspace.Members, m => m.Id == workspaceMember.Id);
                Assert.Contains(workspace.Teams, t => t.Id == teamId);
                Assert.Contains(team.Members, m => m.Id == teamMember.Id);
                Assert.Contains(team.Projects, p => p.Id == projectId);
                Assert.Contains(project.Members, m => m.Id == projectMember.Id);
                Assert.Contains(project.Boards, b => b.Id == boardId);
                Assert.Contains(board.Columns, c => c.Id == columnId);
                Assert.Contains(boardColumn.Tasks, t => t.Id == taskId);
            }
        }
        finally
        {
            await transaction.RollbackAsync();
        }
    }
}
