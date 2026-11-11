using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace TaskFlow.Data.Migrations
{
    /// <inheritdoc />
    public partial class AddPhase2DatabaseEntities : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "ActivityLogs",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    WorkspaceId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    UserId = table.Column<string>(type: "nvarchar(450)", nullable: true),
                    EntityType = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: false),
                    EntityId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    ActionType = table.Column<int>(type: "int", nullable: false),
                    Description = table.Column<string>(type: "nvarchar(2000)", maxLength: 2000, nullable: true),
                    OldValue = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    NewValue = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    IpAddress = table.Column<string>(type: "nvarchar(45)", maxLength: 45, nullable: true),
                    UserAgent = table.Column<string>(type: "nvarchar(1000)", maxLength: 1000, nullable: true),
                    CreatedAt = table.Column<DateTime>(type: "datetime2", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ActivityLogs", x => x.Id);
                    table.ForeignKey(
                        name: "FK_ActivityLogs_AspNetUsers_UserId",
                        column: x => x.UserId,
                        principalTable: "AspNetUsers",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.SetNull);
                    table.ForeignKey(
                        name: "FK_ActivityLogs_Workspaces_WorkspaceId",
                        column: x => x.WorkspaceId,
                        principalTable: "Workspaces",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.SetNull);
                });

            migrationBuilder.CreateTable(
                name: "AIAnalysisSessions",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    WorkspaceId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    RequestedByUserId = table.Column<string>(type: "nvarchar(450)", nullable: false),
                    Status = table.Column<int>(type: "int", nullable: false),
                    ModelProvider = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: false),
                    ModelName = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: true),
                    InputSummary = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    ResultSummary = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    ErrorMessage = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    StartedAt = table.Column<DateTime>(type: "datetime2", nullable: false),
                    CompletedAt = table.Column<DateTime>(type: "datetime2", nullable: true),
                    CreatedAt = table.Column<DateTime>(type: "datetime2", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_AIAnalysisSessions", x => x.Id);
                    table.ForeignKey(
                        name: "FK_AIAnalysisSessions_AspNetUsers_RequestedByUserId",
                        column: x => x.RequestedByUserId,
                        principalTable: "AspNetUsers",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_AIAnalysisSessions_Workspaces_WorkspaceId",
                        column: x => x.WorkspaceId,
                        principalTable: "Workspaces",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "AutomationRules",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    WorkspaceId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    CreatedByUserId = table.Column<string>(type: "nvarchar(450)", nullable: false),
                    Name = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: false),
                    Description = table.Column<string>(type: "nvarchar(2000)", maxLength: 2000, nullable: true),
                    TriggerType = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: false),
                    ConditionsJson = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    ActionsJson = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    IsEnabled = table.Column<bool>(type: "bit", nullable: false),
                    ExecutionCount = table.Column<int>(type: "int", nullable: false),
                    LastExecutedAt = table.Column<DateTime>(type: "datetime2", nullable: true),
                    CreatedAt = table.Column<DateTime>(type: "datetime2", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "datetime2", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_AutomationRules", x => x.Id);
                    table.ForeignKey(
                        name: "FK_AutomationRules_AspNetUsers_CreatedByUserId",
                        column: x => x.CreatedByUserId,
                        principalTable: "AspNetUsers",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_AutomationRules_Workspaces_WorkspaceId",
                        column: x => x.WorkspaceId,
                        principalTable: "Workspaces",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "CustomFieldDefinitions",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    WorkspaceId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    ProjectId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    Name = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: false),
                    FieldType = table.Column<int>(type: "int", nullable: false),
                    OptionsJson = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    CreatedAt = table.Column<DateTime>(type: "datetime2", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "datetime2", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_CustomFieldDefinitions", x => x.Id);
                    table.ForeignKey(
                        name: "FK_CustomFieldDefinitions_Projects_ProjectId",
                        column: x => x.ProjectId,
                        principalTable: "Projects",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_CustomFieldDefinitions_Workspaces_WorkspaceId",
                        column: x => x.WorkspaceId,
                        principalTable: "Workspaces",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "Notifications",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    UserId = table.Column<string>(type: "nvarchar(450)", nullable: false),
                    Type = table.Column<int>(type: "int", nullable: false),
                    Title = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: false),
                    Message = table.Column<string>(type: "nvarchar(2000)", maxLength: 2000, nullable: false),
                    RelatedEntityType = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: true),
                    RelatedEntityId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    ActionUrl = table.Column<string>(type: "nvarchar(2048)", maxLength: 2048, nullable: true),
                    IsRead = table.Column<bool>(type: "bit", nullable: false),
                    ReadAt = table.Column<DateTime>(type: "datetime2", nullable: true),
                    CreatedAt = table.Column<DateTime>(type: "datetime2", nullable: false),
                    ExpiresAt = table.Column<DateTime>(type: "datetime2", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Notifications", x => x.Id);
                    table.ForeignKey(
                        name: "FK_Notifications_AspNetUsers_UserId",
                        column: x => x.UserId,
                        principalTable: "AspNetUsers",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "ProjectMilestones",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    ProjectId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    Title = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: false),
                    Description = table.Column<string>(type: "nvarchar(2000)", maxLength: 2000, nullable: true),
                    DueDate = table.Column<DateTime>(type: "datetime2", nullable: true),
                    Status = table.Column<int>(type: "int", nullable: false),
                    CompletedAt = table.Column<DateTime>(type: "datetime2", nullable: true),
                    CreatedAt = table.Column<DateTime>(type: "datetime2", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "datetime2", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ProjectMilestones", x => x.Id);
                    table.ForeignKey(
                        name: "FK_ProjectMilestones_Projects_ProjectId",
                        column: x => x.ProjectId,
                        principalTable: "Projects",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "SavedFilters",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    UserId = table.Column<string>(type: "nvarchar(450)", nullable: false),
                    WorkspaceId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    ProjectId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    Name = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: false),
                    Description = table.Column<string>(type: "nvarchar(2000)", maxLength: 2000, nullable: true),
                    FilterDefinitionJson = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    IsDefault = table.Column<bool>(type: "bit", nullable: false),
                    IsShared = table.Column<bool>(type: "bit", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "datetime2", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "datetime2", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_SavedFilters", x => x.Id);
                    table.ForeignKey(
                        name: "FK_SavedFilters_AspNetUsers_UserId",
                        column: x => x.UserId,
                        principalTable: "AspNetUsers",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_SavedFilters_Projects_ProjectId",
                        column: x => x.ProjectId,
                        principalTable: "Projects",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.SetNull);
                    table.ForeignKey(
                        name: "FK_SavedFilters_Workspaces_WorkspaceId",
                        column: x => x.WorkspaceId,
                        principalTable: "Workspaces",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.SetNull);
                });

            migrationBuilder.CreateTable(
                name: "SavedViews",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    UserId = table.Column<string>(type: "nvarchar(450)", nullable: false),
                    WorkspaceId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    ProjectId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    Name = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: false),
                    Description = table.Column<string>(type: "nvarchar(2000)", maxLength: 2000, nullable: true),
                    ViewType = table.Column<int>(type: "int", nullable: false),
                    ConfigurationJson = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    IsDefault = table.Column<bool>(type: "bit", nullable: false),
                    IsShared = table.Column<bool>(type: "bit", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "datetime2", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "datetime2", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_SavedViews", x => x.Id);
                    table.ForeignKey(
                        name: "FK_SavedViews_AspNetUsers_UserId",
                        column: x => x.UserId,
                        principalTable: "AspNetUsers",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_SavedViews_Projects_ProjectId",
                        column: x => x.ProjectId,
                        principalTable: "Projects",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.SetNull);
                    table.ForeignKey(
                        name: "FK_SavedViews_Workspaces_WorkspaceId",
                        column: x => x.WorkspaceId,
                        principalTable: "Workspaces",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.SetNull);
                });

            migrationBuilder.CreateTable(
                name: "TaskAIRecommendations",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    TaskId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    RequestedByUserId = table.Column<string>(type: "nvarchar(450)", nullable: false),
                    SuggestedPriority = table.Column<int>(type: "int", nullable: false),
                    PriorityScore = table.Column<int>(type: "int", nullable: false),
                    Explanation = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    RecommendedOrder = table.Column<int>(type: "int", nullable: true),
                    ModelProvider = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: false),
                    ModelName = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: true),
                    IsApproved = table.Column<bool>(type: "bit", nullable: true),
                    ApprovedByUserId = table.Column<string>(type: "nvarchar(450)", nullable: true),
                    ApprovedAt = table.Column<DateTime>(type: "datetime2", nullable: true),
                    CreatedAt = table.Column<DateTime>(type: "datetime2", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_TaskAIRecommendations", x => x.Id);
                    table.CheckConstraint("CK_TaskAIRecommendations_PriorityScore", "[PriorityScore] BETWEEN 0 AND 100");
                    table.ForeignKey(
                        name: "FK_TaskAIRecommendations_AspNetUsers_ApprovedByUserId",
                        column: x => x.ApprovedByUserId,
                        principalTable: "AspNetUsers",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.SetNull);
                    table.ForeignKey(
                        name: "FK_TaskAIRecommendations_AspNetUsers_RequestedByUserId",
                        column: x => x.RequestedByUserId,
                        principalTable: "AspNetUsers",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_TaskAIRecommendations_Tasks_TaskId",
                        column: x => x.TaskId,
                        principalTable: "Tasks",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "TaskCommentMentions",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    TaskCommentId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    MentionedUserId = table.Column<string>(type: "nvarchar(450)", nullable: false),
                    MentionedAt = table.Column<DateTime>(type: "datetime2", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_TaskCommentMentions", x => x.Id);
                    table.ForeignKey(
                        name: "FK_TaskCommentMentions_AspNetUsers_MentionedUserId",
                        column: x => x.MentionedUserId,
                        principalTable: "AspNetUsers",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_TaskCommentMentions_TaskComments_TaskCommentId",
                        column: x => x.TaskCommentId,
                        principalTable: "TaskComments",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "TaskTimers",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    TaskId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    UserId = table.Column<string>(type: "nvarchar(450)", nullable: false),
                    StartedAt = table.Column<DateTime>(type: "datetime2", nullable: false),
                    PausedAt = table.Column<DateTime>(type: "datetime2", nullable: true),
                    TotalPausedSeconds = table.Column<int>(type: "int", nullable: false),
                    IsRunning = table.Column<bool>(type: "bit", nullable: false),
                    IsActive = table.Column<bool>(type: "bit", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "datetime2", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "datetime2", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_TaskTimers", x => x.Id);
                    table.ForeignKey(
                        name: "FK_TaskTimers_AspNetUsers_UserId",
                        column: x => x.UserId,
                        principalTable: "AspNetUsers",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_TaskTimers_Tasks_TaskId",
                        column: x => x.TaskId,
                        principalTable: "Tasks",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "WebhookSubscriptions",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    WorkspaceId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    CreatedByUserId = table.Column<string>(type: "nvarchar(450)", nullable: false),
                    EndpointUrl = table.Column<string>(type: "nvarchar(2048)", maxLength: 2048, nullable: false),
                    Secret = table.Column<string>(type: "nvarchar(512)", maxLength: 512, nullable: false),
                    EventTypesJson = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    Status = table.Column<int>(type: "int", nullable: false),
                    LastDeliveryAt = table.Column<DateTime>(type: "datetime2", nullable: true),
                    CreatedAt = table.Column<DateTime>(type: "datetime2", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "datetime2", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_WebhookSubscriptions", x => x.Id);
                    table.ForeignKey(
                        name: "FK_WebhookSubscriptions_AspNetUsers_CreatedByUserId",
                        column: x => x.CreatedByUserId,
                        principalTable: "AspNetUsers",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_WebhookSubscriptions_Workspaces_WorkspaceId",
                        column: x => x.WorkspaceId,
                        principalTable: "Workspaces",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "WorkspaceInvitations",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    WorkspaceId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    InvitedEmail = table.Column<string>(type: "nvarchar(320)", maxLength: 320, nullable: false),
                    InvitedRole = table.Column<int>(type: "int", nullable: false),
                    TokenHash = table.Column<string>(type: "nvarchar(128)", maxLength: 128, nullable: false),
                    ExpiresAt = table.Column<DateTime>(type: "datetime2", nullable: false),
                    AcceptedAt = table.Column<DateTime>(type: "datetime2", nullable: true),
                    RevokedAt = table.Column<DateTime>(type: "datetime2", nullable: true),
                    Status = table.Column<int>(type: "int", nullable: false),
                    InvitedByUserId = table.Column<string>(type: "nvarchar(450)", nullable: true),
                    CreatedAt = table.Column<DateTime>(type: "datetime2", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_WorkspaceInvitations", x => x.Id);
                    table.ForeignKey(
                        name: "FK_WorkspaceInvitations_AspNetUsers_InvitedByUserId",
                        column: x => x.InvitedByUserId,
                        principalTable: "AspNetUsers",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.SetNull);
                    table.ForeignKey(
                        name: "FK_WorkspaceInvitations_Workspaces_WorkspaceId",
                        column: x => x.WorkspaceId,
                        principalTable: "Workspaces",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "AIAnalysisItems",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    AIAnalysisSessionId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    TaskId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    SuggestedPriority = table.Column<int>(type: "int", nullable: false),
                    PriorityScore = table.Column<int>(type: "int", nullable: false),
                    Explanation = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    RecommendedOrder = table.Column<int>(type: "int", nullable: true),
                    RiskLevel = table.Column<string>(type: "nvarchar(50)", maxLength: 50, nullable: true),
                    CreatedAt = table.Column<DateTime>(type: "datetime2", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_AIAnalysisItems", x => x.Id);
                    table.CheckConstraint("CK_AIAnalysisItems_PriorityScore", "[PriorityScore] BETWEEN 0 AND 100");
                    table.ForeignKey(
                        name: "FK_AIAnalysisItems_AIAnalysisSessions_AIAnalysisSessionId",
                        column: x => x.AIAnalysisSessionId,
                        principalTable: "AIAnalysisSessions",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_AIAnalysisItems_Tasks_TaskId",
                        column: x => x.TaskId,
                        principalTable: "Tasks",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "CustomFieldValues",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    CustomFieldDefinitionId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    TaskId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    TextValue = table.Column<string>(type: "nvarchar(4000)", maxLength: 4000, nullable: true),
                    NumberValue = table.Column<decimal>(type: "decimal(18,4)", nullable: true),
                    DateValue = table.Column<DateTime>(type: "datetime2", nullable: true),
                    BooleanValue = table.Column<bool>(type: "bit", nullable: true),
                    SelectedValue = table.Column<string>(type: "nvarchar(500)", maxLength: 500, nullable: true),
                    CreatedAt = table.Column<DateTime>(type: "datetime2", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "datetime2", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_CustomFieldValues", x => x.Id);
                    table.ForeignKey(
                        name: "FK_CustomFieldValues_CustomFieldDefinitions_CustomFieldDefinitionId",
                        column: x => x.CustomFieldDefinitionId,
                        principalTable: "CustomFieldDefinitions",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_CustomFieldValues_Tasks_TaskId",
                        column: x => x.TaskId,
                        principalTable: "Tasks",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "WebhookDeliveries",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    WebhookSubscriptionId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    EventType = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: false),
                    AttemptedAt = table.Column<DateTime>(type: "datetime2", nullable: false),
                    ResponseStatusCode = table.Column<int>(type: "int", nullable: true),
                    ResponseBody = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    Succeeded = table.Column<bool>(type: "bit", nullable: false),
                    ErrorMessage = table.Column<string>(type: "nvarchar(2000)", maxLength: 2000, nullable: true),
                    CompletedAt = table.Column<DateTime>(type: "datetime2", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_WebhookDeliveries", x => x.Id);
                    table.ForeignKey(
                        name: "FK_WebhookDeliveries_WebhookSubscriptions_WebhookSubscriptionId",
                        column: x => x.WebhookSubscriptionId,
                        principalTable: "WebhookSubscriptions",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_ActivityLogs_CreatedAt",
                table: "ActivityLogs",
                column: "CreatedAt");

            migrationBuilder.CreateIndex(
                name: "IX_ActivityLogs_EntityType_EntityId",
                table: "ActivityLogs",
                columns: new[] { "EntityType", "EntityId" });

            migrationBuilder.CreateIndex(
                name: "IX_ActivityLogs_UserId",
                table: "ActivityLogs",
                column: "UserId");

            migrationBuilder.CreateIndex(
                name: "IX_ActivityLogs_WorkspaceId_CreatedAt",
                table: "ActivityLogs",
                columns: new[] { "WorkspaceId", "CreatedAt" });

            migrationBuilder.CreateIndex(
                name: "IX_AIAnalysisItems_AIAnalysisSessionId_TaskId",
                table: "AIAnalysisItems",
                columns: new[] { "AIAnalysisSessionId", "TaskId" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_AIAnalysisItems_TaskId",
                table: "AIAnalysisItems",
                column: "TaskId");

            migrationBuilder.CreateIndex(
                name: "IX_AIAnalysisSessions_RequestedByUserId_CreatedAt",
                table: "AIAnalysisSessions",
                columns: new[] { "RequestedByUserId", "CreatedAt" });

            migrationBuilder.CreateIndex(
                name: "IX_AIAnalysisSessions_Status",
                table: "AIAnalysisSessions",
                column: "Status");

            migrationBuilder.CreateIndex(
                name: "IX_AIAnalysisSessions_WorkspaceId_CreatedAt",
                table: "AIAnalysisSessions",
                columns: new[] { "WorkspaceId", "CreatedAt" });

            migrationBuilder.CreateIndex(
                name: "IX_AutomationRules_CreatedByUserId",
                table: "AutomationRules",
                column: "CreatedByUserId");

            migrationBuilder.CreateIndex(
                name: "IX_AutomationRules_WorkspaceId_IsEnabled",
                table: "AutomationRules",
                columns: new[] { "WorkspaceId", "IsEnabled" });

            migrationBuilder.CreateIndex(
                name: "IX_AutomationRules_WorkspaceId_TriggerType",
                table: "AutomationRules",
                columns: new[] { "WorkspaceId", "TriggerType" });

            migrationBuilder.CreateIndex(
                name: "IX_CustomFieldDefinitions_ProjectId",
                table: "CustomFieldDefinitions",
                column: "ProjectId");

            migrationBuilder.CreateIndex(
                name: "IX_CustomFieldDefinitions_WorkspaceId_ProjectId_Name",
                table: "CustomFieldDefinitions",
                columns: new[] { "WorkspaceId", "ProjectId", "Name" });

            migrationBuilder.CreateIndex(
                name: "IX_CustomFieldValues_CustomFieldDefinitionId_TaskId",
                table: "CustomFieldValues",
                columns: new[] { "CustomFieldDefinitionId", "TaskId" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_CustomFieldValues_TaskId",
                table: "CustomFieldValues",
                column: "TaskId");

            migrationBuilder.CreateIndex(
                name: "IX_Notifications_ExpiresAt",
                table: "Notifications",
                column: "ExpiresAt");

            migrationBuilder.CreateIndex(
                name: "IX_Notifications_UserId_CreatedAt",
                table: "Notifications",
                columns: new[] { "UserId", "CreatedAt" });

            migrationBuilder.CreateIndex(
                name: "IX_Notifications_UserId_IsRead_CreatedAt",
                table: "Notifications",
                columns: new[] { "UserId", "IsRead", "CreatedAt" });

            migrationBuilder.CreateIndex(
                name: "IX_ProjectMilestones_ProjectId_DueDate",
                table: "ProjectMilestones",
                columns: new[] { "ProjectId", "DueDate" });

            migrationBuilder.CreateIndex(
                name: "IX_ProjectMilestones_ProjectId_Status",
                table: "ProjectMilestones",
                columns: new[] { "ProjectId", "Status" });

            migrationBuilder.CreateIndex(
                name: "IX_SavedFilters_ProjectId",
                table: "SavedFilters",
                column: "ProjectId");

            migrationBuilder.CreateIndex(
                name: "IX_SavedFilters_UserId_IsDefault",
                table: "SavedFilters",
                columns: new[] { "UserId", "IsDefault" });

            migrationBuilder.CreateIndex(
                name: "IX_SavedFilters_UserId_WorkspaceId_ProjectId_Name",
                table: "SavedFilters",
                columns: new[] { "UserId", "WorkspaceId", "ProjectId", "Name" });

            migrationBuilder.CreateIndex(
                name: "IX_SavedFilters_WorkspaceId",
                table: "SavedFilters",
                column: "WorkspaceId");

            migrationBuilder.CreateIndex(
                name: "IX_SavedViews_ProjectId",
                table: "SavedViews",
                column: "ProjectId");

            migrationBuilder.CreateIndex(
                name: "IX_SavedViews_UserId_IsDefault",
                table: "SavedViews",
                columns: new[] { "UserId", "IsDefault" });

            migrationBuilder.CreateIndex(
                name: "IX_SavedViews_UserId_WorkspaceId_ProjectId_Name",
                table: "SavedViews",
                columns: new[] { "UserId", "WorkspaceId", "ProjectId", "Name" });

            migrationBuilder.CreateIndex(
                name: "IX_SavedViews_WorkspaceId",
                table: "SavedViews",
                column: "WorkspaceId");

            migrationBuilder.CreateIndex(
                name: "IX_TaskAIRecommendations_ApprovedByUserId",
                table: "TaskAIRecommendations",
                column: "ApprovedByUserId");

            migrationBuilder.CreateIndex(
                name: "IX_TaskAIRecommendations_RequestedByUserId",
                table: "TaskAIRecommendations",
                column: "RequestedByUserId");

            migrationBuilder.CreateIndex(
                name: "IX_TaskAIRecommendations_TaskId_CreatedAt",
                table: "TaskAIRecommendations",
                columns: new[] { "TaskId", "CreatedAt" });

            migrationBuilder.CreateIndex(
                name: "IX_TaskAIRecommendations_TaskId_IsApproved",
                table: "TaskAIRecommendations",
                columns: new[] { "TaskId", "IsApproved" });

            migrationBuilder.CreateIndex(
                name: "IX_TaskCommentMentions_MentionedUserId",
                table: "TaskCommentMentions",
                column: "MentionedUserId");

            migrationBuilder.CreateIndex(
                name: "IX_TaskCommentMentions_TaskCommentId_MentionedUserId",
                table: "TaskCommentMentions",
                columns: new[] { "TaskCommentId", "MentionedUserId" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_TaskTimers_TaskId_IsActive",
                table: "TaskTimers",
                columns: new[] { "TaskId", "IsActive" });

            migrationBuilder.CreateIndex(
                name: "IX_TaskTimers_UserId",
                table: "TaskTimers",
                column: "UserId",
                unique: true,
                filter: "[IsActive] = 1");

            migrationBuilder.CreateIndex(
                name: "IX_WebhookDeliveries_Succeeded_AttemptedAt",
                table: "WebhookDeliveries",
                columns: new[] { "Succeeded", "AttemptedAt" });

            migrationBuilder.CreateIndex(
                name: "IX_WebhookDeliveries_WebhookSubscriptionId_AttemptedAt",
                table: "WebhookDeliveries",
                columns: new[] { "WebhookSubscriptionId", "AttemptedAt" });

            migrationBuilder.CreateIndex(
                name: "IX_WebhookSubscriptions_CreatedByUserId",
                table: "WebhookSubscriptions",
                column: "CreatedByUserId");

            migrationBuilder.CreateIndex(
                name: "IX_WebhookSubscriptions_WorkspaceId_Status",
                table: "WebhookSubscriptions",
                columns: new[] { "WorkspaceId", "Status" });

            migrationBuilder.CreateIndex(
                name: "IX_WorkspaceInvitations_ExpiresAt",
                table: "WorkspaceInvitations",
                column: "ExpiresAt");

            migrationBuilder.CreateIndex(
                name: "IX_WorkspaceInvitations_InvitedByUserId",
                table: "WorkspaceInvitations",
                column: "InvitedByUserId");

            migrationBuilder.CreateIndex(
                name: "IX_WorkspaceInvitations_TokenHash",
                table: "WorkspaceInvitations",
                column: "TokenHash",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_WorkspaceInvitations_WorkspaceId_InvitedEmail_Status",
                table: "WorkspaceInvitations",
                columns: new[] { "WorkspaceId", "InvitedEmail", "Status" });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "ActivityLogs");

            migrationBuilder.DropTable(
                name: "AIAnalysisItems");

            migrationBuilder.DropTable(
                name: "AutomationRules");

            migrationBuilder.DropTable(
                name: "CustomFieldValues");

            migrationBuilder.DropTable(
                name: "Notifications");

            migrationBuilder.DropTable(
                name: "ProjectMilestones");

            migrationBuilder.DropTable(
                name: "SavedFilters");

            migrationBuilder.DropTable(
                name: "SavedViews");

            migrationBuilder.DropTable(
                name: "TaskAIRecommendations");

            migrationBuilder.DropTable(
                name: "TaskCommentMentions");

            migrationBuilder.DropTable(
                name: "TaskTimers");

            migrationBuilder.DropTable(
                name: "WebhookDeliveries");

            migrationBuilder.DropTable(
                name: "WorkspaceInvitations");

            migrationBuilder.DropTable(
                name: "AIAnalysisSessions");

            migrationBuilder.DropTable(
                name: "CustomFieldDefinitions");

            migrationBuilder.DropTable(
                name: "WebhookSubscriptions");
        }
    }
}
