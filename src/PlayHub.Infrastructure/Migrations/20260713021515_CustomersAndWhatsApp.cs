using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

#pragma warning disable CA1814 // Prefer jagged arrays over multidimensional

namespace PlayHub.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class CustomersAndWhatsApp : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<int>(
                name: "NextCustomerNumber",
                table: "tenants",
                type: "int",
                nullable: false,
                defaultValue: 1);

            migrationBuilder.AddColumn<string>(
                name: "WhatsAppApiBaseUrl",
                table: "tenants",
                type: "nvarchar(300)",
                maxLength: 300,
                nullable: true);

            migrationBuilder.AddColumn<DateTime>(
                name: "WhatsAppConnectedAt",
                table: "tenants",
                type: "datetime2",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "WhatsAppConnectedPhone",
                table: "tenants",
                type: "nvarchar(30)",
                maxLength: 30,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "WhatsAppSessionId",
                table: "tenants",
                type: "nvarchar(200)",
                maxLength: 200,
                nullable: true);

            migrationBuilder.AddColumn<Guid>(
                name: "CustomerId",
                table: "sessions",
                type: "uniqueidentifier",
                nullable: true);

            migrationBuilder.AddColumn<bool>(
                name: "IsQuickGuest",
                table: "sessions",
                type: "bit",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<string>(
                name: "QuickGuestName",
                table: "sessions",
                type: "nvarchar(200)",
                maxLength: 200,
                nullable: true);

            migrationBuilder.CreateTable(
                name: "customer_offers",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    TenantId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    Title = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: false),
                    Message = table.Column<string>(type: "nvarchar(2000)", maxLength: 2000, nullable: false),
                    IsActive = table.Column<bool>(type: "bit", nullable: false),
                    IsDeleted = table.Column<bool>(type: "bit", nullable: false),
                    DeletedAt = table.Column<DateTime>(type: "datetime2", nullable: true),
                    DeletedByUserId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    CreatedAt = table.Column<DateTime>(type: "datetime2", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_customer_offers", x => x.Id);
                    table.ForeignKey(
                        name: "FK_customer_offers_tenants_TenantId",
                        column: x => x.TenantId,
                        principalTable: "tenants",
                        principalColumn: "Id");
                });

            migrationBuilder.CreateTable(
                name: "customers",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    TenantId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    Code = table.Column<string>(type: "nvarchar(20)", maxLength: 20, nullable: false),
                    Name = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: false),
                    Phone = table.Column<string>(type: "nvarchar(30)", maxLength: 30, nullable: false),
                    Notes = table.Column<string>(type: "nvarchar(1000)", maxLength: 1000, nullable: true),
                    IsActive = table.Column<bool>(type: "bit", nullable: false),
                    IsDeleted = table.Column<bool>(type: "bit", nullable: false),
                    DeletedAt = table.Column<DateTime>(type: "datetime2", nullable: true),
                    DeletedByUserId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    CreatedAt = table.Column<DateTime>(type: "datetime2", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_customers", x => x.Id);
                    table.ForeignKey(
                        name: "FK_customers_tenants_TenantId",
                        column: x => x.TenantId,
                        principalTable: "tenants",
                        principalColumn: "Id");
                });

            migrationBuilder.InsertData(
                table: "permissions",
                columns: new[] { "Id", "Action", "Code", "CreatedAt", "Description", "IsSystem", "Module" },
                values: new object[,]
                {
                    { new Guid("550e8400-e29b-41d4-a716-446655440021"), "View", "Customers.View", new DateTime(2026, 1, 1, 0, 0, 0, 0, DateTimeKind.Utc), "View and search customers", true, "Customers" },
                    { new Guid("550e8400-e29b-41d4-a716-446655440022"), "Manage", "Customers.Manage", new DateTime(2026, 1, 1, 0, 0, 0, 0, DateTimeKind.Utc), "Create, update, and delete customers; send WhatsApp messages", true, "Customers" },
                    { new Guid("550e8400-e29b-41d4-a716-446655440023"), "Manage", "Offers.Manage", new DateTime(2026, 1, 1, 0, 0, 0, 0, DateTimeKind.Utc), "Create and manage customer offers", true, "Offers" }
                });

            migrationBuilder.CreateIndex(
                name: "IX_sessions_CustomerId",
                table: "sessions",
                column: "CustomerId");

            migrationBuilder.CreateIndex(
                name: "IX_customer_offers_TenantId_IsActive",
                table: "customer_offers",
                columns: new[] { "TenantId", "IsActive" });

            migrationBuilder.CreateIndex(
                name: "IX_customers_TenantId_Code",
                table: "customers",
                columns: new[] { "TenantId", "Code" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_customers_TenantId_Name",
                table: "customers",
                columns: new[] { "TenantId", "Name" });

            migrationBuilder.CreateIndex(
                name: "IX_customers_TenantId_Phone",
                table: "customers",
                columns: new[] { "TenantId", "Phone" },
                unique: true);

            migrationBuilder.AddForeignKey(
                name: "FK_sessions_customers_CustomerId",
                table: "sessions",
                column: "CustomerId",
                principalTable: "customers",
                principalColumn: "Id");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_sessions_customers_CustomerId",
                table: "sessions");

            migrationBuilder.DropTable(
                name: "customer_offers");

            migrationBuilder.DropTable(
                name: "customers");

            migrationBuilder.DropIndex(
                name: "IX_sessions_CustomerId",
                table: "sessions");

            migrationBuilder.DeleteData(
                table: "permissions",
                keyColumn: "Id",
                keyValue: new Guid("550e8400-e29b-41d4-a716-446655440021"));

            migrationBuilder.DeleteData(
                table: "permissions",
                keyColumn: "Id",
                keyValue: new Guid("550e8400-e29b-41d4-a716-446655440022"));

            migrationBuilder.DeleteData(
                table: "permissions",
                keyColumn: "Id",
                keyValue: new Guid("550e8400-e29b-41d4-a716-446655440023"));

            migrationBuilder.DropColumn(
                name: "NextCustomerNumber",
                table: "tenants");

            migrationBuilder.DropColumn(
                name: "WhatsAppApiBaseUrl",
                table: "tenants");

            migrationBuilder.DropColumn(
                name: "WhatsAppConnectedAt",
                table: "tenants");

            migrationBuilder.DropColumn(
                name: "WhatsAppConnectedPhone",
                table: "tenants");

            migrationBuilder.DropColumn(
                name: "WhatsAppSessionId",
                table: "tenants");

            migrationBuilder.DropColumn(
                name: "CustomerId",
                table: "sessions");

            migrationBuilder.DropColumn(
                name: "IsQuickGuest",
                table: "sessions");

            migrationBuilder.DropColumn(
                name: "QuickGuestName",
                table: "sessions");
        }
    }
}
