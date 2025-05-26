using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace SignalRNotificationAPI.Migrations
{
    /// <inheritdoc />
    public partial class InitialSignalRScaling : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "PersistedMessages",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    TargetUserId = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: false),
                    Message = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    SenderUserId = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: true),
                    MessageType = table.Column<string>(type: "nvarchar(50)", maxLength: 50, nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "datetime2", nullable: false),
                    DeliveredAt = table.Column<DateTime>(type: "datetime2", nullable: true),
                    IsDelivered = table.Column<bool>(type: "bit", nullable: false),
                    IsPersistent = table.Column<bool>(type: "bit", nullable: false),
                    ExpiresAt = table.Column<DateTime>(type: "datetime2", nullable: true),
                    Metadata = table.Column<string>(type: "nvarchar(500)", maxLength: 500, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_PersistedMessages", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "UserConnections",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    UserId = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: false),
                    ConnectionId = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: false),
                    ServerInstance = table.Column<string>(type: "nvarchar(50)", maxLength: 50, nullable: false),
                    ConnectedAt = table.Column<DateTime>(type: "datetime2", nullable: false),
                    LastHeartbeat = table.Column<DateTime>(type: "datetime2", nullable: false),
                    IsActive = table.Column<bool>(type: "bit", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_UserConnections", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "UserSessions",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    UserId = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: false),
                    SessionStart = table.Column<DateTime>(type: "datetime2", nullable: false),
                    SessionEnd = table.Column<DateTime>(type: "datetime2", nullable: true),
                    IsActive = table.Column<bool>(type: "bit", nullable: false),
                    ServerInstance = table.Column<string>(type: "nvarchar(50)", maxLength: 50, nullable: false),
                    ConnectionCount = table.Column<int>(type: "int", nullable: false),
                    LastActivity = table.Column<DateTime>(type: "datetime2", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_UserSessions", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "MessageDeliveryStatuses",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    MessageId = table.Column<int>(type: "int", nullable: false),
                    ConnectionId = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: false),
                    AttemptedAt = table.Column<DateTime>(type: "datetime2", nullable: false),
                    IsSuccessful = table.Column<bool>(type: "bit", nullable: false),
                    ErrorMessage = table.Column<string>(type: "nvarchar(500)", maxLength: 500, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_MessageDeliveryStatuses", x => x.Id);
                    table.ForeignKey(
                        name: "FK_MessageDeliveryStatuses_PersistedMessages_MessageId",
                        column: x => x.MessageId,
                        principalTable: "PersistedMessages",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_MessageDeliveryStatuses_ConnectionId",
                table: "MessageDeliveryStatuses",
                column: "ConnectionId");

            migrationBuilder.CreateIndex(
                name: "IX_MessageDeliveryStatuses_MessageId",
                table: "MessageDeliveryStatuses",
                column: "MessageId");

            migrationBuilder.CreateIndex(
                name: "IX_MessageDeliveryStatuses_MessageId_IsSuccessful",
                table: "MessageDeliveryStatuses",
                columns: new[] { "MessageId", "IsSuccessful" });

            migrationBuilder.CreateIndex(
                name: "IX_PersistedMessages_CreatedAt",
                table: "PersistedMessages",
                column: "CreatedAt");

            migrationBuilder.CreateIndex(
                name: "IX_PersistedMessages_ExpiresAt",
                table: "PersistedMessages",
                column: "ExpiresAt");

            migrationBuilder.CreateIndex(
                name: "IX_PersistedMessages_IsDelivered_ExpiresAt",
                table: "PersistedMessages",
                columns: new[] { "IsDelivered", "ExpiresAt" });

            migrationBuilder.CreateIndex(
                name: "IX_PersistedMessages_TargetUserId",
                table: "PersistedMessages",
                column: "TargetUserId");

            migrationBuilder.CreateIndex(
                name: "IX_PersistedMessages_TargetUserId_IsDelivered",
                table: "PersistedMessages",
                columns: new[] { "TargetUserId", "IsDelivered" });

            migrationBuilder.CreateIndex(
                name: "IX_UserConnections_ConnectionId",
                table: "UserConnections",
                column: "ConnectionId",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_UserConnections_LastHeartbeat",
                table: "UserConnections",
                column: "LastHeartbeat");

            migrationBuilder.CreateIndex(
                name: "IX_UserConnections_UserId",
                table: "UserConnections",
                column: "UserId");

            migrationBuilder.CreateIndex(
                name: "IX_UserConnections_UserId_IsActive",
                table: "UserConnections",
                columns: new[] { "UserId", "IsActive" });

            migrationBuilder.CreateIndex(
                name: "IX_UserSessions_LastActivity",
                table: "UserSessions",
                column: "LastActivity");

            migrationBuilder.CreateIndex(
                name: "IX_UserSessions_SessionStart",
                table: "UserSessions",
                column: "SessionStart");

            migrationBuilder.CreateIndex(
                name: "IX_UserSessions_UserId",
                table: "UserSessions",
                column: "UserId");

            migrationBuilder.CreateIndex(
                name: "IX_UserSessions_UserId_IsActive",
                table: "UserSessions",
                columns: new[] { "UserId", "IsActive" });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "MessageDeliveryStatuses");

            migrationBuilder.DropTable(
                name: "UserConnections");

            migrationBuilder.DropTable(
                name: "UserSessions");

            migrationBuilder.DropTable(
                name: "PersistedMessages");
        }
    }
}
