using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace CDA.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class Similarity : Migration
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
                name: "SimilarityId",
                table: "Votes",
                type: "char(36)",
                nullable: true,
                collation: "ascii_general_ci");

            migrationBuilder.AddColumn<Guid>(
                name: "SimilarityId",
                table: "Comments",
                type: "char(36)",
                nullable: true,
                collation: "ascii_general_ci");

            migrationBuilder.CreateTable(
                name: "SimilarityReports",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "char(36)", nullable: false, collation: "ascii_general_ci"),
                    TopicId = table.Column<Guid>(type: "char(36)", nullable: false, collation: "ascii_general_ci"),
                    ProposalAId = table.Column<Guid>(type: "char(36)", nullable: false, collation: "ascii_general_ci"),
                    ProposalBId = table.Column<Guid>(type: "char(36)", nullable: false, collation: "ascii_general_ci"),
                    ReportedByUserId = table.Column<Guid>(type: "char(36)", nullable: false, collation: "ascii_general_ci"),
                    BetterWrittenProposalId = table.Column<Guid>(type: "char(36)", nullable: true, collation: "ascii_general_ci"),
                    Justification = table.Column<string>(type: "varchar(1000)", maxLength: 1000, nullable: true, collation: "utf8mb4_unicode_520_ci"),
                    CreatedAtUtc = table.Column<DateTime>(type: "datetime(6)", nullable: false),
                    ScoreSum = table.Column<int>(type: "int", nullable: false),
                    VoteCount = table.Column<int>(type: "int", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_SimilarityReports", x => x.Id);
                    table.CheckConstraint("CK_Similarity_Ordered", "`ProposalAId` < `ProposalBId`");
                    table.ForeignKey(
                        name: "FK_SimilarityReports_Proposals_ProposalAId",
                        column: x => x.ProposalAId,
                        principalTable: "Proposals",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_SimilarityReports_Proposals_ProposalBId",
                        column: x => x.ProposalBId,
                        principalTable: "Proposals",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_SimilarityReports_Topics_TopicId",
                        column: x => x.TopicId,
                        principalTable: "Topics",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                })
                .Annotation("Relational:Collation", "utf8mb4_unicode_520_ci");

            migrationBuilder.CreateIndex(
                name: "IX_Votes_SimilarityId",
                table: "Votes",
                column: "SimilarityId");

            migrationBuilder.CreateIndex(
                name: "UX_Votes_User_Similarity",
                table: "Votes",
                columns: new[] { "UserId", "SimilarityId" },
                unique: true);

            migrationBuilder.AddCheckConstraint(
                name: "CK_Votes_SingleTarget",
                table: "Votes",
                sql: "(`TopicId` IS NOT NULL) + (`ProposalId` IS NOT NULL) + (`ReferenceId` IS NOT NULL) + (`SimilarityId` IS NOT NULL) = 1");

            migrationBuilder.CreateIndex(
                name: "IX_Comments_SimilarityId_CreatedAtUtc",
                table: "Comments",
                columns: new[] { "SimilarityId", "CreatedAtUtc" });

            migrationBuilder.AddCheckConstraint(
                name: "CK_Comments_SingleTarget",
                table: "Comments",
                sql: "(`TopicId` IS NOT NULL) + (`ProposalId` IS NOT NULL) + (`SimilarityId` IS NOT NULL) = 1");

            migrationBuilder.CreateIndex(
                name: "IX_SimilarityReports_ProposalBId",
                table: "SimilarityReports",
                column: "ProposalBId");

            migrationBuilder.CreateIndex(
                name: "IX_SimilarityReports_TopicId_ScoreSum",
                table: "SimilarityReports",
                columns: new[] { "TopicId", "ScoreSum" });

            migrationBuilder.CreateIndex(
                name: "UX_Similarity_Pair",
                table: "SimilarityReports",
                columns: new[] { "ProposalAId", "ProposalBId" },
                unique: true);

            migrationBuilder.AddForeignKey(
                name: "FK_Comments_SimilarityReports_SimilarityId",
                table: "Comments",
                column: "SimilarityId",
                principalTable: "SimilarityReports",
                principalColumn: "Id",
                onDelete: ReferentialAction.Cascade);

            migrationBuilder.AddForeignKey(
                name: "FK_Votes_SimilarityReports_SimilarityId",
                table: "Votes",
                column: "SimilarityId",
                principalTable: "SimilarityReports",
                principalColumn: "Id",
                onDelete: ReferentialAction.Cascade);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_Comments_SimilarityReports_SimilarityId",
                table: "Comments");

            migrationBuilder.DropForeignKey(
                name: "FK_Votes_SimilarityReports_SimilarityId",
                table: "Votes");

            migrationBuilder.DropTable(
                name: "SimilarityReports");

            migrationBuilder.DropIndex(
                name: "IX_Votes_SimilarityId",
                table: "Votes");

            migrationBuilder.DropIndex(
                name: "UX_Votes_User_Similarity",
                table: "Votes");

            migrationBuilder.DropCheckConstraint(
                name: "CK_Votes_SingleTarget",
                table: "Votes");

            migrationBuilder.DropIndex(
                name: "IX_Comments_SimilarityId_CreatedAtUtc",
                table: "Comments");

            migrationBuilder.DropCheckConstraint(
                name: "CK_Comments_SingleTarget",
                table: "Comments");

            migrationBuilder.DropColumn(
                name: "SimilarityId",
                table: "Votes");

            migrationBuilder.DropColumn(
                name: "SimilarityId",
                table: "Comments");

            migrationBuilder.AddCheckConstraint(
                name: "CK_Votes_SingleTarget",
                table: "Votes",
                sql: "(`TopicId` IS NOT NULL) + (`ProposalId` IS NOT NULL) + (`ReferenceId` IS NOT NULL) = 1");

            migrationBuilder.AddCheckConstraint(
                name: "CK_Comments_SingleTarget",
                table: "Comments",
                sql: "(`TopicId` IS NOT NULL) + (`ProposalId` IS NOT NULL) = 1");
        }
    }
}
