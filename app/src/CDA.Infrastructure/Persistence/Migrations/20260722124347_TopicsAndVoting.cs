using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace CDA.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class TopicsAndVoting : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "Topics",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "char(36)", nullable: false, collation: "ascii_general_ci"),
                    Subject = table.Column<string>(type: "varchar(200)", maxLength: 200, nullable: false, collation: "utf8mb4_unicode_520_ci"),
                    Description = table.Column<string>(type: "varchar(8000)", maxLength: 8000, nullable: false, collation: "utf8mb4_unicode_520_ci"),
                    CreatedByUserId = table.Column<Guid>(type: "char(36)", nullable: false, collation: "ascii_general_ci"),
                    CreatedAtUtc = table.Column<DateTime>(type: "datetime(6)", nullable: false),
                    ClosesAtUtc = table.Column<DateTime>(type: "datetime(6)", nullable: true),
                    Phase = table.Column<int>(type: "int", nullable: false),
                    Visibility = table.Column<int>(type: "int", nullable: false),
                    HideVoteCountsUntilClose = table.Column<bool>(type: "tinyint(1)", nullable: false),
                    DefaultSimilarityThreshold = table.Column<int>(type: "int", nullable: false),
                    ScoreSum = table.Column<int>(type: "int", nullable: false),
                    VoteCount = table.Column<int>(type: "int", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Topics", x => x.Id);
                })
                .Annotation("Relational:Collation", "utf8mb4_unicode_520_ci");

            migrationBuilder.CreateTable(
                name: "TopicMembers",
                columns: table => new
                {
                    TopicId = table.Column<Guid>(type: "char(36)", nullable: false, collation: "ascii_general_ci"),
                    UserId = table.Column<Guid>(type: "char(36)", nullable: false, collation: "ascii_general_ci"),
                    Role = table.Column<int>(type: "int", nullable: false),
                    JoinedAtUtc = table.Column<DateTime>(type: "datetime(6)", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_TopicMembers", x => new { x.TopicId, x.UserId });
                    table.ForeignKey(
                        name: "FK_TopicMembers_Topics_TopicId",
                        column: x => x.TopicId,
                        principalTable: "Topics",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                })
                .Annotation("Relational:Collation", "utf8mb4_unicode_520_ci");

            migrationBuilder.CreateTable(
                name: "Votes",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "char(36)", nullable: false, collation: "ascii_general_ci"),
                    UserId = table.Column<Guid>(type: "char(36)", nullable: false, collation: "ascii_general_ci"),
                    Value = table.Column<short>(type: "smallint", nullable: false),
                    CastAtUtc = table.Column<DateTime>(type: "datetime(6)", nullable: false),
                    TopicId = table.Column<Guid>(type: "char(36)", nullable: true, collation: "ascii_general_ci")
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Votes", x => x.Id);
                    table.CheckConstraint("CK_Votes_SingleTarget", "(`TopicId` IS NOT NULL) = 1");
                    table.CheckConstraint("CK_Votes_Value", "`Value` IN (-1, 0, 1)");
                    table.ForeignKey(
                        name: "FK_Votes_Topics_TopicId",
                        column: x => x.TopicId,
                        principalTable: "Topics",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                })
                .Annotation("Relational:Collation", "utf8mb4_unicode_520_ci");

            migrationBuilder.CreateIndex(
                name: "IX_TopicMembers_UserId",
                table: "TopicMembers",
                column: "UserId");

            migrationBuilder.CreateIndex(
                name: "IX_Topics_Created",
                table: "Topics",
                columns: new[] { "CreatedAtUtc", "Id" });

            migrationBuilder.CreateIndex(
                name: "IX_Topics_Score",
                table: "Topics",
                columns: new[] { "ScoreSum", "Id" });

            migrationBuilder.CreateIndex(
                name: "IX_Topics_Visibility",
                table: "Topics",
                column: "Visibility");

            migrationBuilder.CreateIndex(
                name: "IX_Votes_TopicId",
                table: "Votes",
                column: "TopicId");

            migrationBuilder.CreateIndex(
                name: "UX_Votes_User_Topic",
                table: "Votes",
                columns: new[] { "UserId", "TopicId" },
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "TopicMembers");

            migrationBuilder.DropTable(
                name: "Votes");

            migrationBuilder.DropTable(
                name: "Topics");
        }
    }
}
