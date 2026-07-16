using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace PlayHub.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class CafeteriaItemUnits : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "BaseUnitName",
                table: "cafeteria_items",
                type: "nvarchar(max)",
                nullable: false,
                defaultValue: "");

            migrationBuilder.AddColumn<string>(
                name: "LargeUnitName",
                table: "cafeteria_items",
                type: "nvarchar(max)",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "UnitsPerLarge",
                table: "cafeteria_items",
                type: "int",
                nullable: false,
                defaultValue: 0);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "BaseUnitName",
                table: "cafeteria_items");

            migrationBuilder.DropColumn(
                name: "LargeUnitName",
                table: "cafeteria_items");

            migrationBuilder.DropColumn(
                name: "UnitsPerLarge",
                table: "cafeteria_items");
        }
    }
}
