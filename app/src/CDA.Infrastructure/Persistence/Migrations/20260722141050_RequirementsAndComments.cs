using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace CDA.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class RequirementsAndComments : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "Comments",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "char(36)", nullable: false, collation: "ascii_general_ci"),
                    AuthorId = table.Column<Guid>(type: "char(36)", nullable: false, collation: "ascii_general_ci"),
                    Body = table.Column<string>(type: "TEXT", nullable: false, collation: "utf8mb4_unicode_520_ci"),
                    CreatedAtUtc = table.Column<DateTime>(type: "datetime(6)", nullable: false),
                    EditedAtUtc = table.Column<DateTime>(type: "datetime(6)", nullable: true),
                    DeletedAtUtc = table.Column<DateTime>(type: "datetime(6)", nullable: true),
                    TopicId = table.Column<Guid>(type: "char(36)", nullable: true, collation: "ascii_general_ci")
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Comments", x => x.Id);
                    table.CheckConstraint("CK_Comments_SingleTarget", "(`TopicId` IS NOT NULL) = 1");
                    table.ForeignKey(
                        name: "FK_Comments_Topics_TopicId",
                        column: x => x.TopicId,
                        principalTable: "Topics",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                })
                .Annotation("Relational:Collation", "utf8mb4_unicode_520_ci");

            migrationBuilder.CreateTable(
                name: "Requirements",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "char(36)", nullable: false, collation: "ascii_general_ci"),
                    TopicId = table.Column<Guid>(type: "char(36)", nullable: false, collation: "ascii_general_ci"),
                    Text = table.Column<string>(type: "varchar(500)", maxLength: 500, nullable: false, collation: "utf8mb4_unicode_520_ci"),
                    Order = table.Column<int>(type: "int", nullable: false),
                    CreatedAtUtc = table.Column<DateTime>(type: "datetime(6)", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Requirements", x => x.Id);
                    table.ForeignKey(
                        name: "FK_Requirements_Topics_TopicId",
                        column: x => x.TopicId,
                        principalTable: "Topics",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                })
                .Annotation("Relational:Collation", "utf8mb4_unicode_520_ci");

            migrationBuilder.CreateIndex(
                name: "IX_Comments_AuthorId",
                table: "Comments",
                column: "AuthorId");

            migrationBuilder.CreateIndex(
                name: "IX_Comments_TopicId_CreatedAtUtc",
                table: "Comments",
                columns: new[] { "TopicId", "CreatedAtUtc" });

            migrationBuilder.CreateIndex(
                name: "IX_Requirements_TopicId_Order",
                table: "Requirements",
                columns: new[] { "TopicId", "Order" });

            // Full-text index for the comment search that Phase 9 builds on. EF Core has no
            // API for MariaDB FULLTEXT, so it is declared here in raw SQL.
            //
            // Note innodb_ft_min_token_size on this server is 3: terms of one or two
            // characters are not indexed and will never match. The query parser has to say so
            // rather than return an empty result and let the reader assume there is nothing.
            migrationBuilder.Sql("ALTER TABLE `Comments` ADD FULLTEXT INDEX `FT_Comments_Body` (`Body`)");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {

            migrationBuilder.Sql("ALTER TABLE `Comments` DROP INDEX `FT_Comments_Body`");
            migrationBuilder.DropTable(
                name: "Comments");

            migrationBuilder.DropTable(
                name: "Requirements");
        }
    }
}
