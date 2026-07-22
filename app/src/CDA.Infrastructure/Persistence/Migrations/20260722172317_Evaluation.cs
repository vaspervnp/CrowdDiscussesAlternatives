using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace CDA.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class Evaluation : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "RequirementScores",
                columns: table => new
                {
                    UserId = table.Column<Guid>(type: "char(36)", nullable: false, collation: "ascii_general_ci"),
                    GroupId = table.Column<Guid>(type: "char(36)", nullable: false, collation: "ascii_general_ci"),
                    RequirementId = table.Column<Guid>(type: "char(36)", nullable: false, collation: "ascii_general_ci"),
                    TopicId = table.Column<Guid>(type: "char(36)", nullable: false, collation: "ascii_general_ci"),
                    Score = table.Column<int>(type: "int", nullable: false),
                    UpdatedAtUtc = table.Column<DateTime>(type: "datetime(6)", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_RequirementScores", x => new { x.UserId, x.GroupId, x.RequirementId });
                    table.CheckConstraint("CK_RequirementScores_Range", "`Score` BETWEEN 0 AND 5");
                    table.ForeignKey(
                        name: "FK_RequirementScores_ProposalGroups_GroupId",
                        column: x => x.GroupId,
                        principalTable: "ProposalGroups",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_RequirementScores_Requirements_RequirementId",
                        column: x => x.RequirementId,
                        principalTable: "Requirements",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_RequirementScores_Topics_TopicId",
                        column: x => x.TopicId,
                        principalTable: "Topics",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                })
                .Annotation("Relational:Collation", "utf8mb4_unicode_520_ci");

            migrationBuilder.CreateTable(
                name: "RequirementWeights",
                columns: table => new
                {
                    UserId = table.Column<Guid>(type: "char(36)", nullable: false, collation: "ascii_general_ci"),
                    RequirementId = table.Column<Guid>(type: "char(36)", nullable: false, collation: "ascii_general_ci"),
                    TopicId = table.Column<Guid>(type: "char(36)", nullable: false, collation: "ascii_general_ci"),
                    Weight = table.Column<int>(type: "int", nullable: false),
                    UpdatedAtUtc = table.Column<DateTime>(type: "datetime(6)", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_RequirementWeights", x => new { x.UserId, x.RequirementId });
                    table.CheckConstraint("CK_RequirementWeights_Range", "`Weight` BETWEEN 0 AND 5");
                    table.ForeignKey(
                        name: "FK_RequirementWeights_Requirements_RequirementId",
                        column: x => x.RequirementId,
                        principalTable: "Requirements",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_RequirementWeights_Topics_TopicId",
                        column: x => x.TopicId,
                        principalTable: "Topics",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                })
                .Annotation("Relational:Collation", "utf8mb4_unicode_520_ci");

            migrationBuilder.CreateIndex(
                name: "IX_RequirementScores_GroupId",
                table: "RequirementScores",
                column: "GroupId");

            migrationBuilder.CreateIndex(
                name: "IX_RequirementScores_RequirementId",
                table: "RequirementScores",
                column: "RequirementId");

            migrationBuilder.CreateIndex(
                name: "IX_RequirementScores_TopicId",
                table: "RequirementScores",
                column: "TopicId");

            migrationBuilder.CreateIndex(
                name: "IX_RequirementScores_UserId_TopicId",
                table: "RequirementScores",
                columns: new[] { "UserId", "TopicId" });

            migrationBuilder.CreateIndex(
                name: "IX_RequirementWeights_RequirementId",
                table: "RequirementWeights",
                column: "RequirementId");

            migrationBuilder.CreateIndex(
                name: "IX_RequirementWeights_TopicId",
                table: "RequirementWeights",
                column: "TopicId");

            migrationBuilder.CreateIndex(
                name: "IX_RequirementWeights_UserId_TopicId",
                table: "RequirementWeights",
                columns: new[] { "UserId", "TopicId" });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "RequirementScores");

            migrationBuilder.DropTable(
                name: "RequirementWeights");
        }
    }
}
