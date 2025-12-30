using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace QDeskPro.Data.Migrations
{
    /// <inheritdoc />
    public partial class AddIncludeLandRateToSale : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<bool>(
                name: "IncludeLandRate",
                table: "Sales",
                type: "bit",
                nullable: false,
                defaultValue: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "IncludeLandRate",
                table: "Sales");
        }
    }
}
