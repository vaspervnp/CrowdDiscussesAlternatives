using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace CDA.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class References : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropCheckConstraint(
                name: "CK_Votes_SingleTarget",
                table: "Votes");

            migrationBuilder.AddColumn<int>(
                name: "ReferenceAspect",
                table: "Votes",
                type: "int",
                nullable: true);

            migrationBuilder.AddColumn<Guid>(
                name: "ReferenceId",
                table: "Votes",
                type: "char(36)",
                nullable: true,
                collation: "ascii_general_ci");

            migrationBuilder.CreateTable(
                name: "References",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "char(36)", nullable: false, collation: "ascii_general_ci"),
                    TopicId = table.Column<Guid>(type: "char(36)", nullable: false, collation: "ascii_general_ci"),
                    CanonicalUrl = table.Column<string>(type: "varchar(400)", maxLength: 400, nullable: false, collation: "utf8mb4_unicode_520_ci"),
                    Description = table.Column<string>(type: "varchar(500)", maxLength: 500, nullable: false, collation: "utf8mb4_unicode_520_ci"),
                    CreatedByUserId = table.Column<Guid>(type: "char(36)", nullable: false, collation: "ascii_general_ci"),
                    CreatedAtUtc = table.Column<DateTime>(type: "datetime(6)", nullable: false),
                    AccuracyScore = table.Column<int>(type: "int", nullable: false),
                    AccuracyVotes = table.Column<int>(type: "int", nullable: false),
                    ImportanceScore = table.Column<int>(type: "int", nullable: false),
                    ImportanceVotes = table.Column<int>(type: "int", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_References", x => x.Id);
                    table.ForeignKey(
                        name: "FK_References_Topics_TopicId",
                        column: x => x.TopicId,
                        principalTable: "Topics",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                })
                .Annotation("Relational:Collation", "utf8mb4_unicode_520_ci");

            migrationBuilder.CreateTable(
                name: "TopicUserReputations",
                columns: table => new
                {
                    TopicId = table.Column<Guid>(type: "char(36)", nullable: false, collation: "ascii_general_ci"),
                    UserId = table.Column<Guid>(type: "char(36)", nullable: false, collation: "ascii_general_ci"),
                    ReferenceScore = table.Column<int>(type: "int", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_TopicUserReputations", x => new { x.TopicId, x.UserId });
                    table.ForeignKey(
                        name: "FK_TopicUserReputations_Topics_TopicId",
                        column: x => x.TopicId,
                        principalTable: "Topics",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                })
                .Annotation("Relational:Collation", "utf8mb4_unicode_520_ci");

            migrationBuilder.CreateTable(
                name: "ProposalReferences",
                columns: table => new
                {
                    ProposalId = table.Column<Guid>(type: "char(36)", nullable: false, collation: "ascii_general_ci"),
                    ReferenceId = table.Column<Guid>(type: "char(36)", nullable: false, collation: "ascii_general_ci"),
                    AddedByUserId = table.Column<Guid>(type: "char(36)", nullable: false, collation: "ascii_general_ci"),
                    AddedAtUtc = table.Column<DateTime>(type: "datetime(6)", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ProposalReferences", x => new { x.ProposalId, x.ReferenceId });
                    table.ForeignKey(
                        name: "FK_ProposalReferences_Proposals_ProposalId",
                        column: x => x.ProposalId,
                        principalTable: "Proposals",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_ProposalReferences_References_ReferenceId",
                        column: x => x.ReferenceId,
                        principalTable: "References",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                })
                .Annotation("Relational:Collation", "utf8mb4_unicode_520_ci");

            migrationBuilder.CreateIndex(
                name: "IX_Votes_ReferenceId",
                table: "Votes",
                column: "ReferenceId");

            migrationBuilder.CreateIndex(
                name: "UX_Votes_User_Reference_Aspect",
                table: "Votes",
                columns: new[] { "UserId", "ReferenceId", "ReferenceAspect" },
                unique: true);

            migrationBuilder.AddCheckConstraint(
                name: "CK_Votes_AspectMatchesTarget",
                table: "Votes",
                sql: "(`ReferenceId` IS NOT NULL) = (`ReferenceAspect` IS NOT NULL)");

            migrationBuilder.AddCheckConstraint(
                name: "CK_Votes_SingleTarget",
                table: "Votes",
                sql: "(`TopicId` IS NOT NULL) + (`ProposalId` IS NOT NULL) + (`ReferenceId` IS NOT NULL) = 1");

            migrationBuilder.CreateIndex(
                name: "IX_ProposalReferences_ReferenceId",
                table: "ProposalReferences",
                column: "ReferenceId");

            migrationBuilder.CreateIndex(
                name: "IX_References_TopicId_CreatedByUserId",
                table: "References",
                columns: new[] { "TopicId", "CreatedByUserId" });

            migrationBuilder.CreateIndex(
                name: "UX_References_Topic_Url",
                table: "References",
                columns: new[] { "TopicId", "CanonicalUrl" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_TopicUserReputations_TopicId_ReferenceScore",
                table: "TopicUserReputations",
                columns: new[] { "TopicId", "ReferenceScore" });

            migrationBuilder.AddForeignKey(
                name: "FK_Votes_References_ReferenceId",
                table: "Votes",
                column: "ReferenceId",
                principalTable: "References",
                principalColumn: "Id",
                onDelete: ReferentialAction.Cascade);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_Votes_References_ReferenceId",
                table: "Votes");

            migrationBuilder.DropTable(
                name: "ProposalReferences");

            migrationBuilder.DropTable(
                name: "TopicUserReputations");

            migrationBuilder.DropTable(
                name: "References");

            migrationBuilder.DropIndex(
                name: "IX_Votes_ReferenceId",
                table: "Votes");

            migrationBuilder.DropIndex(
                name: "UX_Votes_User_Reference_Aspect",
                table: "Votes");

            migrationBuilder.DropCheckConstraint(
                name: "CK_Votes_AspectMatchesTarget",
                table: "Votes");

            migrationBuilder.DropCheckConstraint(
                name: "CK_Votes_SingleTarget",
                table: "Votes");

            migrationBuilder.DropColumn(
                name: "ReferenceAspect",
                table: "Votes");

            migrationBuilder.DropColumn(
                name: "ReferenceId",
                table: "Votes");

            migrationBuilder.AddCheckConstraint(
                name: "CK_Votes_SingleTarget",
                table: "Votes",
                sql: "(`TopicId` IS NOT NULL) + (`ProposalId` IS NOT NULL) = 1");
        }
    }
}
