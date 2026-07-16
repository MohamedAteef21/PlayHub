using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

#pragma warning disable CA1814 // Prefer jagged arrays over multidimensional

namespace PlayHub.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AddInventoryManageItemsAndSessionsHistoryPermissions : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.UpdateData(
                table: "permissions",
                keyColumn: "Id",
                keyValue: new Guid("550e8400-e29b-41d4-a716-446655440004"),
                column: "Description",
                value: "View live floor and active sessions");

            migrationBuilder.UpdateData(
                table: "permissions",
                keyColumn: "Id",
                keyValue: new Guid("550e8400-e29b-41d4-a716-446655440005"),
                column: "Description",
                value: "Dispense cafeteria sales (walk-in or session), including unit choice");

            migrationBuilder.UpdateData(
                table: "permissions",
                keyColumn: "Id",
                keyValue: new Guid("550e8400-e29b-41d4-a716-446655440007"),
                column: "Description",
                value: "View cafeteria catalog and sales");

            migrationBuilder.UpdateData(
                table: "permissions",
                keyColumn: "Id",
                keyValue: new Guid("550e8400-e29b-41d4-a716-446655440008"),
                column: "Description",
                value: "View inventory levels, units, and vouchers");

            migrationBuilder.UpdateData(
                table: "permissions",
                keyColumn: "Id",
                keyValue: new Guid("550e8400-e29b-41d4-a716-446655440009"),
                column: "Description",
                value: "Create stock-in / count / settlement vouchers and manual stock adjust");

            migrationBuilder.InsertData(
                table: "permissions",
                columns: new[] { "Id", "Action", "Code", "CreatedAt", "Description", "IsSystem", "Module" },
                values: new object[,]
                {
                    { new Guid("550e8400-e29b-41d4-a716-446655440019"), "ManageItems", "Inventory.ManageItems", new DateTime(2026, 1, 1, 0, 0, 0, 0, DateTimeKind.Utc), "Create/edit items, units & conversion, activate/deactivate, soft-delete", true, "Inventory" },
                    { new Guid("550e8400-e29b-41d4-a716-446655440020"), "History", "Sessions.History", new DateTime(2026, 1, 1, 0, 0, 0, 0, DateTimeKind.Utc), "View full session open/close history (who and when)", true, "Sessions" }
                });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DeleteData(
                table: "permissions",
                keyColumn: "Id",
                keyValue: new Guid("550e8400-e29b-41d4-a716-446655440019"));

            migrationBuilder.DeleteData(
                table: "permissions",
                keyColumn: "Id",
                keyValue: new Guid("550e8400-e29b-41d4-a716-446655440020"));

            migrationBuilder.UpdateData(
                table: "permissions",
                keyColumn: "Id",
                keyValue: new Guid("550e8400-e29b-41d4-a716-446655440004"),
                column: "Description",
                value: "View sessions and live dashboard");

            migrationBuilder.UpdateData(
                table: "permissions",
                keyColumn: "Id",
                keyValue: new Guid("550e8400-e29b-41d4-a716-446655440005"),
                column: "Description",
                value: "Record cafeteria sales");

            migrationBuilder.UpdateData(
                table: "permissions",
                keyColumn: "Id",
                keyValue: new Guid("550e8400-e29b-41d4-a716-446655440007"),
                column: "Description",
                value: "View cafeteria items and sales");

            migrationBuilder.UpdateData(
                table: "permissions",
                keyColumn: "Id",
                keyValue: new Guid("550e8400-e29b-41d4-a716-446655440008"),
                column: "Description",
                value: "View inventory levels");

            migrationBuilder.UpdateData(
                table: "permissions",
                keyColumn: "Id",
                keyValue: new Guid("550e8400-e29b-41d4-a716-446655440009"),
                column: "Description",
                value: "Manually adjust inventory");
        }
    }
}
