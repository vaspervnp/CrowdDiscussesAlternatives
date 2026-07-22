using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace CDA.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class Proposals : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropCheckConstraint(
                name: "CK_Votes_SingleTarget",
                table: "Votes");

            migrationBuilder.DropCheckConstraint(
                name: "CK_Comments_SingleTarget",
                table: "Comments");

            migrationBuilder.AddColumn<Guid>(
                name: "ProposalId",
                table: "Votes",
                type: "char(36)",
                nullable: true,
                collation: "ascii_general_ci");

            migrationBuilder.AddColumn<Guid>(
                name: "ProposalId",
                table: "Comments",
                type: "char(36)",
                nullable: true,
                collation: "ascii_general_ci");

            migrationBuilder.CreateTable(
                name: "Proposals",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "char(36)", nullable: false, collation: "ascii_general_ci"),
                    TopicId = table.Column<Guid>(type: "char(36)", nullable: false, collation: "ascii_general_ci"),
                    AuthorId = table.Column<Guid>(type: "char(36)", nullable: false, collation: "ascii_general_ci"),
                    Text = table.Column<string>(type: "varchar(500)", maxLength: 500, nullable: false, collation: "utf8mb4_unicode_520_ci"),
                    CreatedAtUtc = table.Column<DateTime>(type: "datetime(6)", nullable: false),
                    EditedAtUtc = table.Column<DateTime>(type: "datetime(6)", nullable: true),
                    EditableUntilUtc = table.Column<DateTime>(type: "datetime(6)", nullable: false),
                    ManuallyLocked = table.Column<bool>(type: "tinyint(1)", nullable: false),
                    ScoreSum = table.Column<int>(type: "int", nullable: false),
                    VoteCount = table.Column<int>(type: "int", nullable: false),
                    CommentCount = table.Column<int>(type: "int", nullable: false),
                    LastCommentAtUtc = table.Column<DateTime>(type: "datetime(6)", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Proposals", x => x.Id);
                    table.ForeignKey(
                        name: "FK_Proposals_Topics_TopicId",
                        column: x => x.TopicId,
                        principalTable: "Topics",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                })
                .Annotation("Relational:Collation", "utf8mb4_unicode_520_ci");

            migrationBuilder.CreateIndex(
                name: "IX_Votes_ProposalId",
                table: "Votes",
                column: "ProposalId");

            migrationBuilder.CreateIndex(
                name: "UX_Votes_User_Proposal",
                table: "Votes",
                columns: new[] { "UserId", "ProposalId" },
                unique: true);

            migrationBuilder.AddCheckConstraint(
                name: "CK_Votes_SingleTarget",
                table: "Votes",
                sql: "(`TopicId` IS NOT NULL) + (`ProposalId` IS NOT NULL) = 1");

            migrationBuilder.CreateIndex(
                name: "IX_Comments_ProposalId_CreatedAtUtc",
                table: "Comments",
                columns: new[] { "ProposalId", "CreatedAtUtc" });

            migrationBuilder.AddCheckConstraint(
                name: "CK_Comments_SingleTarget",
                table: "Comments",
                sql: "(`TopicId` IS NOT NULL) + (`ProposalId` IS NOT NULL) = 1");

            migrationBuilder.CreateIndex(
                name: "IX_Proposals_Author",
                table: "Proposals",
                columns: new[] { "TopicId", "AuthorId" });

            migrationBuilder.CreateIndex(
                name: "IX_Proposals_Created",
                table: "Proposals",
                columns: new[] { "TopicId", "CreatedAtUtc", "Id" });

            migrationBuilder.CreateIndex(
                name: "IX_Proposals_LastComment",
                table: "Proposals",
                columns: new[] { "TopicId", "LastCommentAtUtc", "Id" });

            migrationBuilder.CreateIndex(
                name: "IX_Proposals_Score",
                table: "Proposals",
                columns: new[] { "TopicId", "ScoreSum", "Id" });

            migrationBuilder.AddForeignKey(
                name: "FK_Comments_Proposals_ProposalId",
                table: "Comments",
                column: "ProposalId",
                principalTable: "Proposals",
                principalColumn: "Id",
                onDelete: ReferentialAction.Cascade);

            migrationBuilder.AddForeignKey(
                name: "FK_Votes_Proposals_ProposalId",
                table: "Votes",
                column: "ProposalId",
                principalTable: "Proposals",
                principalColumn: "Id",
                onDelete: ReferentialAction.Cascade);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_Comments_Proposals_ProposalId",
                table: "Comments");

            migrationBuilder.DropForeignKey(
                name: "FK_Votes_Proposals_ProposalId",
                table: "Votes");

            migrationBuilder.DropTable(
                name: "Proposals");

            migrationBuilder.DropIndex(
                name: "IX_Votes_ProposalId",
                table: "Votes");

            migrationBuilder.DropIndex(
                name: "UX_Votes_User_Proposal",
                table: "Votes");

            migrationBuilder.DropCheckConstraint(
                name: "CK_Votes_SingleTarget",
                table: "Votes");

            migrationBuilder.DropIndex(
                name: "IX_Comments_ProposalId_CreatedAtUtc",
                table: "Comments");

            migrationBuilder.DropCheckConstraint(
                name: "CK_Comments_SingleTarget",
                table: "Comments");

            migrationBuilder.DropColumn(
                name: "ProposalId",
                table: "Votes");

            migrationBuilder.DropColumn(
                name: "ProposalId",
                table: "Comments");

            migrationBuilder.AddCheckConstraint(
                name: "CK_Votes_SingleTarget",
                table: "Votes",
                sql: "(`TopicId` IS NOT NULL) = 1");

            migrationBuilder.AddCheckConstraint(
                name: "CK_Comments_SingleTarget",
                table: "Comments",
                sql: "(`TopicId` IS NOT NULL) = 1");
        }
    }
}
