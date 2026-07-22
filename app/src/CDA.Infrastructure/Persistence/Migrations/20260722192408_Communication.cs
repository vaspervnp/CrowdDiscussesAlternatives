using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace CDA.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class Communication : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "Attachments",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "char(36)", nullable: false, collation: "ascii_general_ci"),
                    TopicId = table.Column<Guid>(type: "char(36)", nullable: false, collation: "ascii_general_ci"),
                    ProposalId = table.Column<Guid>(type: "char(36)", nullable: false, collation: "ascii_general_ci"),
                    UploadedByUserId = table.Column<Guid>(type: "char(36)", nullable: false, collation: "ascii_general_ci"),
                    FileName = table.Column<string>(type: "varchar(260)", maxLength: 260, nullable: false, collation: "utf8mb4_unicode_520_ci"),
                    ContentType = table.Column<string>(type: "varchar(150)", maxLength: 150, nullable: false, collation: "utf8mb4_unicode_520_ci"),
                    StoredName = table.Column<string>(type: "varchar(100)", maxLength: 100, nullable: false, collation: "utf8mb4_unicode_520_ci"),
                    SizeBytes = table.Column<long>(type: "bigint", nullable: false),
                    UploadedAtUtc = table.Column<DateTime>(type: "datetime(6)", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Attachments", x => x.Id);
                    table.ForeignKey(
                        name: "FK_Attachments_Proposals_ProposalId",
                        column: x => x.ProposalId,
                        principalTable: "Proposals",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_Attachments_Topics_TopicId",
                        column: x => x.TopicId,
                        principalTable: "Topics",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                })
                .Annotation("Relational:Collation", "utf8mb4_unicode_520_ci");

            migrationBuilder.CreateTable(
                name: "NotificationPreferences",
                columns: table => new
                {
                    UserId = table.Column<Guid>(type: "char(36)", nullable: false, collation: "ascii_general_ci"),
                    Delivery = table.Column<int>(type: "int", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_NotificationPreferences", x => x.UserId);
                })
                .Annotation("Relational:Collation", "utf8mb4_unicode_520_ci");

            migrationBuilder.CreateTable(
                name: "Notifications",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "char(36)", nullable: false, collation: "ascii_general_ci"),
                    UserId = table.Column<Guid>(type: "char(36)", nullable: false, collation: "ascii_general_ci"),
                    ActorId = table.Column<Guid>(type: "char(36)", nullable: true, collation: "ascii_general_ci"),
                    TopicId = table.Column<Guid>(type: "char(36)", nullable: true, collation: "ascii_general_ci"),
                    Kind = table.Column<int>(type: "int", nullable: false),
                    Summary = table.Column<string>(type: "varchar(300)", maxLength: 300, nullable: false, collation: "utf8mb4_unicode_520_ci"),
                    Link = table.Column<string>(type: "varchar(500)", maxLength: 500, nullable: false, collation: "utf8mb4_unicode_520_ci"),
                    CreatedAtUtc = table.Column<DateTime>(type: "datetime(6)", nullable: false),
                    ReadAtUtc = table.Column<DateTime>(type: "datetime(6)", nullable: true),
                    EmailedAtUtc = table.Column<DateTime>(type: "datetime(6)", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Notifications", x => x.Id);
                    table.ForeignKey(
                        name: "FK_Notifications_Topics_TopicId",
                        column: x => x.TopicId,
                        principalTable: "Topics",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                })
                .Annotation("Relational:Collation", "utf8mb4_unicode_520_ci");

            migrationBuilder.CreateTable(
                name: "PrivateMessages",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "char(36)", nullable: false, collation: "ascii_general_ci"),
                    FromUserId = table.Column<Guid>(type: "char(36)", nullable: false, collation: "ascii_general_ci"),
                    ToUserId = table.Column<Guid>(type: "char(36)", nullable: false, collation: "ascii_general_ci"),
                    Body = table.Column<string>(type: "varchar(4000)", maxLength: 4000, nullable: false, collation: "utf8mb4_unicode_520_ci"),
                    SentAtUtc = table.Column<DateTime>(type: "datetime(6)", nullable: false),
                    ReadAtUtc = table.Column<DateTime>(type: "datetime(6)", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_PrivateMessages", x => x.Id);
                })
                .Annotation("Relational:Collation", "utf8mb4_unicode_520_ci");

            migrationBuilder.CreateIndex(
                name: "IX_Attachments_ProposalId",
                table: "Attachments",
                column: "ProposalId");

            migrationBuilder.CreateIndex(
                name: "IX_Attachments_TopicId_Id",
                table: "Attachments",
                columns: new[] { "TopicId", "Id" });

            migrationBuilder.CreateIndex(
                name: "IX_Notifications_Outbox",
                table: "Notifications",
                columns: new[] { "EmailedAtUtc", "CreatedAtUtc" });

            migrationBuilder.CreateIndex(
                name: "IX_Notifications_TopicId",
                table: "Notifications",
                column: "TopicId");

            migrationBuilder.CreateIndex(
                name: "IX_Notifications_UserId_CreatedAtUtc",
                table: "Notifications",
                columns: new[] { "UserId", "CreatedAtUtc" });

            migrationBuilder.CreateIndex(
                name: "IX_Notifications_UserId_ReadAtUtc",
                table: "Notifications",
                columns: new[] { "UserId", "ReadAtUtc" });

            migrationBuilder.CreateIndex(
                name: "IX_PrivateMessages_FromUserId_SentAtUtc",
                table: "PrivateMessages",
                columns: new[] { "FromUserId", "SentAtUtc" });

            migrationBuilder.CreateIndex(
                name: "IX_PrivateMessages_ToUserId_ReadAtUtc",
                table: "PrivateMessages",
                columns: new[] { "ToUserId", "ReadAtUtc" });

            migrationBuilder.CreateIndex(
                name: "IX_PrivateMessages_ToUserId_SentAtUtc",
                table: "PrivateMessages",
                columns: new[] { "ToUserId", "SentAtUtc" });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "Attachments");

            migrationBuilder.DropTable(
                name: "NotificationPreferences");

            migrationBuilder.DropTable(
                name: "Notifications");

            migrationBuilder.DropTable(
                name: "PrivateMessages");
        }
    }
}
