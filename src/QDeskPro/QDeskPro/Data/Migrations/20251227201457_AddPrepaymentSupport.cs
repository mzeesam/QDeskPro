using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace QDeskPro.Data.Migrations
{
    /// <inheritdoc />
    public partial class AddPrepaymentSupport : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<bool>(
                name: "IsPrepaymentSale",
                table: "Sales",
                type: "bit",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<double>(
                name: "PrepaymentApplied",
                table: "Sales",
                type: "float",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "PrepaymentId",
                table: "Sales",
                type: "nvarchar(450)",
                nullable: true);

            migrationBuilder.CreateTable(
                name: "Prepayments",
                columns: table => new
                {
                    Id = table.Column<string>(type: "nvarchar(450)", nullable: false),
                    VehicleRegistration = table.Column<string>(type: "nvarchar(50)", maxLength: 50, nullable: false),
                    ClientName = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: true),
                    ClientPhone = table.Column<string>(type: "nvarchar(20)", maxLength: 20, nullable: true),
                    PrepaymentDate = table.Column<DateTime>(type: "datetime2", nullable: false),
                    TotalAmountPaid = table.Column<double>(type: "float", nullable: false),
                    AmountUsed = table.Column<double>(type: "float", nullable: false),
                    IntendedProductId = table.Column<string>(type: "nvarchar(450)", nullable: true),
                    IntendedQuantity = table.Column<double>(type: "float", nullable: true),
                    IntendedPricePerUnit = table.Column<double>(type: "float", nullable: true),
                    PaymentMode = table.Column<string>(type: "nvarchar(50)", maxLength: 50, nullable: false),
                    PaymentReference = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: true),
                    Status = table.Column<string>(type: "nvarchar(20)", maxLength: 20, nullable: false),
                    FullyFulfilledDate = table.Column<DateTime>(type: "datetime2", nullable: true),
                    ApplicationUserId = table.Column<string>(type: "nvarchar(450)", nullable: false),
                    ClerkName = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: false),
                    Notes = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    IsActive = table.Column<bool>(type: "bit", nullable: false),
                    DateCreated = table.Column<DateTime>(type: "datetime2", nullable: false),
                    CreatedBy = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    DateModified = table.Column<DateTime>(type: "datetime2", nullable: true),
                    ModifiedBy = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    DateStamp = table.Column<string>(type: "nvarchar(450)", nullable: true),
                    QId = table.Column<string>(type: "nvarchar(450)", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Prepayments", x => x.Id);
                    table.ForeignKey(
                        name: "FK_Prepayments_Products_IntendedProductId",
                        column: x => x.IntendedProductId,
                        principalTable: "Products",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.SetNull);
                });

            migrationBuilder.CreateIndex(
                name: "IX_Sales_PrepaymentId",
                table: "Sales",
                column: "PrepaymentId");

            migrationBuilder.CreateIndex(
                name: "IX_Prepayments_ApplicationUserId",
                table: "Prepayments",
                column: "ApplicationUserId");

            migrationBuilder.CreateIndex(
                name: "IX_Prepayments_DateStamp",
                table: "Prepayments",
                column: "DateStamp");

            migrationBuilder.CreateIndex(
                name: "IX_Prepayments_IntendedProductId",
                table: "Prepayments",
                column: "IntendedProductId");

            migrationBuilder.CreateIndex(
                name: "IX_Prepayments_PrepaymentDate",
                table: "Prepayments",
                column: "PrepaymentDate");

            migrationBuilder.CreateIndex(
                name: "IX_Prepayments_QId",
                table: "Prepayments",
                column: "QId");

            migrationBuilder.CreateIndex(
                name: "IX_Prepayments_Status",
                table: "Prepayments",
                column: "Status");

            migrationBuilder.CreateIndex(
                name: "IX_Prepayments_VehicleRegistration",
                table: "Prepayments",
                column: "VehicleRegistration");

            migrationBuilder.AddForeignKey(
                name: "FK_Sales_Prepayments_PrepaymentId",
                table: "Sales",
                column: "PrepaymentId",
                principalTable: "Prepayments",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_Sales_Prepayments_PrepaymentId",
                table: "Sales");

            migrationBuilder.DropTable(
                name: "Prepayments");

            migrationBuilder.DropIndex(
                name: "IX_Sales_PrepaymentId",
                table: "Sales");

            migrationBuilder.DropColumn(
                name: "IsPrepaymentSale",
                table: "Sales");

            migrationBuilder.DropColumn(
                name: "PrepaymentApplied",
                table: "Sales");

            migrationBuilder.DropColumn(
                name: "PrepaymentId",
                table: "Sales");
        }
    }
}
