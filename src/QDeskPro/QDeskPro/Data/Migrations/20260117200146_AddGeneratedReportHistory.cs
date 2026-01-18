using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace QDeskPro.Data.Migrations
{
    /// <inheritdoc />
    public partial class AddGeneratedReportHistory : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "GeneratedReports",
                columns: table => new
                {
                    Id = table.Column<string>(type: "nvarchar(450)", nullable: false),
                    ReportName = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: false),
                    ReportType = table.Column<string>(type: "nvarchar(50)", maxLength: 50, nullable: false),
                    FromDate = table.Column<DateTime>(type: "datetime2", nullable: false),
                    ToDate = table.Column<DateTime>(type: "datetime2", nullable: false),
                    QuarryId = table.Column<string>(type: "nvarchar(450)", nullable: false),
                    ClerkId = table.Column<string>(type: "nvarchar(50)", maxLength: 50, nullable: true),
                    ClerkName = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: true),
                    ReportDataJson = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    TotalSales = table.Column<double>(type: "float", nullable: true),
                    TotalExpenses = table.Column<double>(type: "float", nullable: true),
                    OrderCount = table.Column<int>(type: "int", nullable: true),
                    TotalQuantity = table.Column<double>(type: "float", nullable: true),
                    NetEarnings = table.Column<double>(type: "float", nullable: true),
                    GeneratedByUserId = table.Column<string>(type: "nvarchar(450)", nullable: false),
                    GeneratedByName = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: false),
                    ExportedFilePath = table.Column<string>(type: "nvarchar(500)", maxLength: 500, nullable: true),
                    ExportFormat = table.Column<string>(type: "nvarchar(20)", maxLength: 20, nullable: true),
                    IsActive = table.Column<bool>(type: "bit", nullable: false),
                    DateCreated = table.Column<DateTime>(type: "datetime2", nullable: false),
                    CreatedBy = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    DateModified = table.Column<DateTime>(type: "datetime2", nullable: true),
                    ModifiedBy = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    DateStamp = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    QId = table.Column<string>(type: "nvarchar(max)", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_GeneratedReports", x => x.Id);
                    table.ForeignKey(
                        name: "FK_GeneratedReports_AspNetUsers_GeneratedByUserId",
                        column: x => x.GeneratedByUserId,
                        principalTable: "AspNetUsers",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_GeneratedReports_Quarries_QuarryId",
                        column: x => x.QuarryId,
                        principalTable: "Quarries",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateIndex(
                name: "IX_GeneratedReports_DateCreated",
                table: "GeneratedReports",
                column: "DateCreated");

            migrationBuilder.CreateIndex(
                name: "IX_GeneratedReports_GeneratedByUserId",
                table: "GeneratedReports",
                column: "GeneratedByUserId");

            migrationBuilder.CreateIndex(
                name: "IX_GeneratedReports_QuarryId",
                table: "GeneratedReports",
                column: "QuarryId");

            migrationBuilder.CreateIndex(
                name: "IX_GeneratedReports_ReportType_FromDate_ToDate",
                table: "GeneratedReports",
                columns: new[] { "ReportType", "FromDate", "ToDate" });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "GeneratedReports");
        }
    }
}
