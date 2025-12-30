using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace QDeskPro.Data.Migrations
{
    /// <inheritdoc />
    public partial class AddManagerHierarchy : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "CreatedByManagerId",
                table: "AspNetUsers",
                type: "nvarchar(450)",
                nullable: true);

            migrationBuilder.CreateIndex(
                name: "IX_AspNetUsers_CreatedByManagerId",
                table: "AspNetUsers",
                column: "CreatedByManagerId");

            migrationBuilder.AddForeignKey(
                name: "FK_AspNetUsers_AspNetUsers_CreatedByManagerId",
                table: "AspNetUsers",
                column: "CreatedByManagerId",
                principalTable: "AspNetUsers",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_AspNetUsers_AspNetUsers_CreatedByManagerId",
                table: "AspNetUsers");

            migrationBuilder.DropIndex(
                name: "IX_AspNetUsers_CreatedByManagerId",
                table: "AspNetUsers");

            migrationBuilder.DropColumn(
                name: "CreatedByManagerId",
                table: "AspNetUsers");
        }
    }
}
