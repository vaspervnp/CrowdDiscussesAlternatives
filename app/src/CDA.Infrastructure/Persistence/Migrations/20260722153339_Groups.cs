using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace CDA.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class Groups : Migration
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
                name: "GroupId",
                table: "Votes",
                type: "char(36)",
                nullable: true,
                collation: "ascii_general_ci");

            migrationBuilder.AddColumn<Guid>(
                name: "GroupId",
                table: "Comments",
                type: "char(36)",
                nullable: true,
                collation: "ascii_general_ci");

            migrationBuilder.CreateTable(
                name: "ProposalGroups",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "char(36)", nullable: false, collation: "ascii_general_ci"),
                    TopicId = table.Column<Guid>(type: "char(36)", nullable: false, collation: "ascii_general_ci"),
                    CreatedByUserId = table.Column<Guid>(type: "char(36)", nullable: false, collation: "ascii_general_ci"),
                    Description = table.Column<string>(type: "varchar(2000)", maxLength: 2000, nullable: false, collation: "utf8mb4_unicode_520_ci"),
                    CreatedAtUtc = table.Column<DateTime>(type: "datetime(6)", nullable: false),
                    EditedAtUtc = table.Column<DateTime>(type: "datetime(6)", nullable: true),
                    ImprovesGroupId = table.Column<Guid>(type: "char(36)", nullable: true, collation: "ascii_general_ci"),
                    ScoreSum = table.Column<int>(type: "int", nullable: false),
                    VoteCount = table.Column<int>(type: "int", nullable: false),
                    CommentCount = table.Column<int>(type: "int", nullable: false),
                    LastCommentAtUtc = table.Column<DateTime>(type: "datetime(6)", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ProposalGroups", x => x.Id);
                    table.ForeignKey(
                        name: "FK_ProposalGroups_ProposalGroups_ImprovesGroupId",
                        column: x => x.ImprovesGroupId,
                        principalTable: "ProposalGroups",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_ProposalGroups_Topics_TopicId",
                        column: x => x.TopicId,
                        principalTable: "Topics",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                })
                .Annotation("Relational:Collation", "utf8mb4_unicode_520_ci");

            migrationBuilder.CreateTable(
                name: "GroupItems",
                columns: table => new
                {
                    GroupId = table.Column<Guid>(type: "char(36)", nullable: false, collation: "ascii_general_ci"),
                    ProposalId = table.Column<Guid>(type: "char(36)", nullable: false, collation: "ascii_general_ci")
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_GroupItems", x => new { x.GroupId, x.ProposalId });
                    table.ForeignKey(
                        name: "FK_GroupItems_ProposalGroups_GroupId",
                        column: x => x.GroupId,
                        principalTable: "ProposalGroups",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_GroupItems_Proposals_ProposalId",
                        column: x => x.ProposalId,
                        principalTable: "Proposals",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                })
                .Annotation("Relational:Collation", "utf8mb4_unicode_520_ci");

            migrationBuilder.CreateIndex(
                name: "IX_Votes_GroupId",
                table: "Votes",
                column: "GroupId");

            migrationBuilder.CreateIndex(
                name: "UX_Votes_User_Group",
                table: "Votes",
                columns: new[] { "UserId", "GroupId" },
                unique: true);

            migrationBuilder.AddCheckConstraint(
                name: "CK_Votes_SingleTarget",
                table: "Votes",
                sql: "(`TopicId` IS NOT NULL) + (`ProposalId` IS NOT NULL) + (`ReferenceId` IS NOT NULL) + (`SimilarityId` IS NOT NULL) + (`GroupId` IS NOT NULL) = 1");

            migrationBuilder.CreateIndex(
                name: "IX_Comments_GroupId_CreatedAtUtc",
                table: "Comments",
                columns: new[] { "GroupId", "CreatedAtUtc" });

            migrationBuilder.AddCheckConstraint(
                name: "CK_Comments_SingleTarget",
                table: "Comments",
                sql: "(`TopicId` IS NOT NULL) + (`ProposalId` IS NOT NULL) + (`SimilarityId` IS NOT NULL) + (`GroupId` IS NOT NULL) = 1");

            migrationBuilder.CreateIndex(
                name: "IX_GroupItems_ProposalId",
                table: "GroupItems",
                column: "ProposalId");

            migrationBuilder.CreateIndex(
                name: "IX_Groups_Created",
                table: "ProposalGroups",
                columns: new[] { "TopicId", "CreatedAtUtc", "Id" });

            migrationBuilder.CreateIndex(
                name: "IX_Groups_Score",
                table: "ProposalGroups",
                columns: new[] { "TopicId", "ScoreSum", "Id" });

            migrationBuilder.CreateIndex(
                name: "IX_ProposalGroups_ImprovesGroupId",
                table: "ProposalGroups",
                column: "ImprovesGroupId");

            migrationBuilder.CreateIndex(
                name: "IX_ProposalGroups_TopicId_CreatedByUserId",
                table: "ProposalGroups",
                columns: new[] { "TopicId", "CreatedByUserId" });

            migrationBuilder.AddForeignKey(
                name: "FK_Comments_ProposalGroups_GroupId",
                table: "Comments",
                column: "GroupId",
                principalTable: "ProposalGroups",
                principalColumn: "Id",
                onDelete: ReferentialAction.Cascade);

            migrationBuilder.AddForeignKey(
                name: "FK_Votes_ProposalGroups_GroupId",
                table: "Votes",
                column: "GroupId",
                principalTable: "ProposalGroups",
                principalColumn: "Id",
                onDelete: ReferentialAction.Cascade);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_Comments_ProposalGroups_GroupId",
                table: "Comments");

            migrationBuilder.DropForeignKey(
                name: "FK_Votes_ProposalGroups_GroupId",
                table: "Votes");

            migrationBuilder.DropTable(
                name: "GroupItems");

            migrationBuilder.DropTable(
                name: "ProposalGroups");

            migrationBuilder.DropIndex(
                name: "IX_Votes_GroupId",
                table: "Votes");

            migrationBuilder.DropIndex(
                name: "UX_Votes_User_Group",
                table: "Votes");

            migrationBuilder.DropCheckConstraint(
                name: "CK_Votes_SingleTarget",
                table: "Votes");

            migrationBuilder.DropIndex(
                name: "IX_Comments_GroupId_CreatedAtUtc",
                table: "Comments");

            migrationBuilder.DropCheckConstraint(
                name: "CK_Comments_SingleTarget",
                table: "Comments");

            migrationBuilder.DropColumn(
                name: "GroupId",
                table: "Votes");

            migrationBuilder.DropColumn(
                name: "GroupId",
                table: "Comments");

            migrationBuilder.AddCheckConstraint(
                name: "CK_Votes_SingleTarget",
                table: "Votes",
                sql: "(`TopicId` IS NOT NULL) + (`ProposalId` IS NOT NULL) + (`ReferenceId` IS NOT NULL) + (`SimilarityId` IS NOT NULL) = 1");

            migrationBuilder.AddCheckConstraint(
                name: "CK_Comments_SingleTarget",
                table: "Comments",
                sql: "(`TopicId` IS NOT NULL) + (`ProposalId` IS NOT NULL) + (`SimilarityId` IS NOT NULL) = 1");
        }
    }
}
