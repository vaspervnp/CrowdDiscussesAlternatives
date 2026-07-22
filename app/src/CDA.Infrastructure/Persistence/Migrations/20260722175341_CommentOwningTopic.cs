using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace CDA.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class CommentOwningTopic : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<Guid>(
                name: "OwningTopicId",
                table: "Comments",
                type: "char(36)",
                nullable: false,
                defaultValue: new Guid("00000000-0000-0000-0000-000000000000"),
                collation: "ascii_general_ci");


            // Existing comments predate the column. Each already points at exactly one target,
            // so its topic can be recovered from whichever target that is — without this the
            // whole table would carry an empty topic id and search would return nothing.
            migrationBuilder.Sql(
                """
                UPDATE `Comments` c
                LEFT JOIN `Proposals` p ON c.`ProposalId` = p.`Id`
                LEFT JOIN `ProposalGroups` g ON c.`GroupId` = g.`Id`
                LEFT JOIN `SimilarityReports` s ON c.`SimilarityId` = s.`Id`
                SET c.`OwningTopicId` = COALESCE(c.`TopicId`, p.`TopicId`, g.`TopicId`, s.`TopicId`)
                """);

            migrationBuilder.CreateIndex(
                name: "IX_Comments_OwningTopicId_AuthorId",
                table: "Comments",
                columns: new[] { "OwningTopicId", "AuthorId" });

            migrationBuilder.AddForeignKey(
                name: "FK_Comments_Topics_OwningTopicId",
                table: "Comments",
                column: "OwningTopicId",
                principalTable: "Topics",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_Comments_Topics_OwningTopicId",
                table: "Comments");

            migrationBuilder.DropIndex(
                name: "IX_Comments_OwningTopicId_AuthorId",
                table: "Comments");

            migrationBuilder.DropColumn(
                name: "OwningTopicId",
                table: "Comments");
        }
    }
}
