using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace CDA.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class ParameterTables : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "ParameterTables",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "char(36)", nullable: false, collation: "ascii_general_ci"),
                    TopicId = table.Column<Guid>(type: "char(36)", nullable: false, collation: "ascii_general_ci"),
                    OwnerId = table.Column<Guid>(type: "char(36)", nullable: false, collation: "ascii_general_ci"),
                    Name = table.Column<string>(type: "varchar(200)", maxLength: 200, nullable: false, collation: "utf8mb4_unicode_520_ci"),
                    IsShared = table.Column<bool>(type: "tinyint(1)", nullable: false),
                    CreatedAtUtc = table.Column<DateTime>(type: "datetime(6)", nullable: false),
                    UpdatedAtUtc = table.Column<DateTime>(type: "datetime(6)", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ParameterTables", x => x.Id);
                    table.ForeignKey(
                        name: "FK_ParameterTables_Topics_TopicId",
                        column: x => x.TopicId,
                        principalTable: "Topics",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                })
                .Annotation("Relational:Collation", "utf8mb4_unicode_520_ci");

            migrationBuilder.CreateTable(
                name: "Parameters",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "char(36)", nullable: false, collation: "ascii_general_ci"),
                    TableId = table.Column<Guid>(type: "char(36)", nullable: false, collation: "ascii_general_ci"),
                    Name = table.Column<string>(type: "varchar(120)", maxLength: 120, nullable: false, collation: "utf8mb4_unicode_520_ci"),
                    Order = table.Column<int>(type: "int", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Parameters", x => x.Id);
                    table.ForeignKey(
                        name: "FK_Parameters_ParameterTables_TableId",
                        column: x => x.TableId,
                        principalTable: "ParameterTables",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                })
                .Annotation("Relational:Collation", "utf8mb4_unicode_520_ci");

            migrationBuilder.CreateTable(
                name: "ParameterInfluences",
                columns: table => new
                {
                    TableId = table.Column<Guid>(type: "char(36)", nullable: false, collation: "ascii_general_ci"),
                    FromParameterId = table.Column<Guid>(type: "char(36)", nullable: false, collation: "ascii_general_ci"),
                    ToParameterId = table.Column<Guid>(type: "char(36)", nullable: false, collation: "ascii_general_ci"),
                    Effect = table.Column<int>(type: "int", nullable: false),
                    Note = table.Column<string>(type: "varchar(300)", maxLength: 300, nullable: true, collation: "utf8mb4_unicode_520_ci")
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ParameterInfluences", x => new { x.TableId, x.FromParameterId, x.ToParameterId });
                    table.CheckConstraint("CK_ParameterInfluences_NotSelf", "`FromParameterId` <> `ToParameterId`");
                    table.ForeignKey(
                        name: "FK_ParameterInfluences_ParameterTables_TableId",
                        column: x => x.TableId,
                        principalTable: "ParameterTables",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_ParameterInfluences_Parameters_FromParameterId",
                        column: x => x.FromParameterId,
                        principalTable: "Parameters",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_ParameterInfluences_Parameters_ToParameterId",
                        column: x => x.ToParameterId,
                        principalTable: "Parameters",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                })
                .Annotation("Relational:Collation", "utf8mb4_unicode_520_ci");

            migrationBuilder.CreateIndex(
                name: "IX_ParameterInfluences_FromParameterId",
                table: "ParameterInfluences",
                column: "FromParameterId");

            migrationBuilder.CreateIndex(
                name: "IX_ParameterInfluences_ToParameterId",
                table: "ParameterInfluences",
                column: "ToParameterId");

            migrationBuilder.CreateIndex(
                name: "IX_Parameters_TableId_Order",
                table: "Parameters",
                columns: new[] { "TableId", "Order" });

            migrationBuilder.CreateIndex(
                name: "IX_ParameterTables_TopicId_IsShared",
                table: "ParameterTables",
                columns: new[] { "TopicId", "IsShared" });

            migrationBuilder.CreateIndex(
                name: "IX_ParameterTables_TopicId_OwnerId",
                table: "ParameterTables",
                columns: new[] { "TopicId", "OwnerId" });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "ParameterInfluences");

            migrationBuilder.DropTable(
                name: "Parameters");

            migrationBuilder.DropTable(
                name: "ParameterTables");
        }
    }
}
