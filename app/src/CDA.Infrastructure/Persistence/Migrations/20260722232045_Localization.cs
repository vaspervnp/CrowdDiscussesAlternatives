using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace CDA.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class Localization : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "LocalizedTexts",
                columns: table => new
                {
                    Key = table.Column<string>(type: "varchar(512)", maxLength: 512, nullable: false, collation: "utf8mb4_bin"),
                    Culture = table.Column<string>(type: "varchar(35)", maxLength: 35, nullable: false, collation: "utf8mb4_bin"),
                    Value = table.Column<string>(type: "longtext", nullable: false, collation: "utf8mb4_unicode_520_ci")
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_LocalizedTexts", x => new { x.Key, x.Culture });
                })
                .Annotation("Relational:Collation", "utf8mb4_unicode_520_ci");

            migrationBuilder.CreateIndex(
                name: "IX_LocalizedTexts_Culture",
                table: "LocalizedTexts",
                column: "Culture");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "LocalizedTexts");
        }
    }
}
