using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace QDeskPro.Data.Migrations
{
    /// <inheritdoc />
    public partial class SyncModelChanges : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<double>(
                name: "DailyProductionCapacity",
                table: "Quarries",
                type: "float",
                nullable: true);

            migrationBuilder.AddColumn<double>(
                name: "EstimatedMonthlyFixedCosts",
                table: "Quarries",
                type: "float",
                nullable: true);

            migrationBuilder.AddColumn<double>(
                name: "FuelCostPerLiter",
                table: "Quarries",
                type: "float",
                nullable: true);

            migrationBuilder.AddColumn<double>(
                name: "InitialCapitalInvestment",
                table: "Quarries",
                type: "float",
                nullable: true);

            migrationBuilder.AddColumn<DateTime>(
                name: "OperationsStartDate",
                table: "Quarries",
                type: "datetime2",
                nullable: true);

            migrationBuilder.AddColumn<double>(
                name: "TargetProfitMargin",
                table: "Quarries",
                type: "float",
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "DailyProductionCapacity",
                table: "Quarries");

            migrationBuilder.DropColumn(
                name: "EstimatedMonthlyFixedCosts",
                table: "Quarries");

            migrationBuilder.DropColumn(
                name: "FuelCostPerLiter",
                table: "Quarries");

            migrationBuilder.DropColumn(
                name: "InitialCapitalInvestment",
                table: "Quarries");

            migrationBuilder.DropColumn(
                name: "OperationsStartDate",
                table: "Quarries");

            migrationBuilder.DropColumn(
                name: "TargetProfitMargin",
                table: "Quarries");
        }
    }
}
