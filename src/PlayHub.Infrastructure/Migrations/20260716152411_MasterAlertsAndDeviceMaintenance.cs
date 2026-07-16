using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace PlayHub.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class MasterAlertsAndDeviceMaintenance : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<short>(
                name: "AllowedNotificationChannels",
                table: "users",
                type: "smallint",
                nullable: false,
                defaultValue: (short)0);

            migrationBuilder.CreateTable(
                name: "device_maintenances",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    TenantId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    BranchId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    DeviceId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    Reason = table.Column<string>(type: "nvarchar(500)", maxLength: 500, nullable: false),
                    Notes = table.Column<string>(type: "nvarchar(1000)", maxLength: 1000, nullable: true),
                    StartedAt = table.Column<DateTime>(type: "datetime2", nullable: false),
                    CompletedAt = table.Column<DateTime>(type: "datetime2", nullable: true),
                    ReportedByUserId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    IsDeleted = table.Column<bool>(type: "bit", nullable: false),
                    DeletedAt = table.Column<DateTime>(type: "datetime2", nullable: true),
                    DeletedByUserId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    CreatedAt = table.Column<DateTime>(type: "datetime2", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_device_maintenances", x => x.Id);
                    table.ForeignKey(
                        name: "FK_device_maintenances_Devices_DeviceId",
                        column: x => x.DeviceId,
                        principalTable: "Devices",
                        principalColumn: "Id");
                    table.ForeignKey(
                        name: "FK_device_maintenances_branches_BranchId",
                        column: x => x.BranchId,
                        principalTable: "branches",
                        principalColumn: "Id");
                    table.ForeignKey(
                        name: "FK_device_maintenances_users_ReportedByUserId",
                        column: x => x.ReportedByUserId,
                        principalTable: "users",
                        principalColumn: "Id");
                });

            migrationBuilder.CreateTable(
                name: "master_alert_settings",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    TenantId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    UserId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    SmtpHost = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: true),
                    SmtpPort = table.Column<int>(type: "int", nullable: false),
                    SmtpUsername = table.Column<string>(type: "nvarchar(256)", maxLength: 256, nullable: true),
                    SmtpPassword = table.Column<string>(type: "nvarchar(500)", maxLength: 500, nullable: true),
                    SenderDisplayName = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: true),
                    AlertRecipientEmail = table.Column<string>(type: "nvarchar(256)", maxLength: 256, nullable: true),
                    OwnerWhatsAppPhone = table.Column<string>(type: "nvarchar(30)", maxLength: 30, nullable: true),
                    NotifyLowStock = table.Column<bool>(type: "bit", nullable: false),
                    NotifySubscription = table.Column<bool>(type: "bit", nullable: false),
                    NotifyDeviceMaintenance = table.Column<bool>(type: "bit", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "datetime2", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_master_alert_settings", x => x.Id);
                    table.ForeignKey(
                        name: "FK_master_alert_settings_tenants_TenantId",
                        column: x => x.TenantId,
                        principalTable: "tenants",
                        principalColumn: "Id");
                    table.ForeignKey(
                        name: "FK_master_alert_settings_users_UserId",
                        column: x => x.UserId,
                        principalTable: "users",
                        principalColumn: "Id");
                });

            migrationBuilder.CreateIndex(
                name: "IX_device_maintenances_BranchId",
                table: "device_maintenances",
                column: "BranchId");

            migrationBuilder.CreateIndex(
                name: "IX_device_maintenances_DeviceId_CompletedAt",
                table: "device_maintenances",
                columns: new[] { "DeviceId", "CompletedAt" });

            migrationBuilder.CreateIndex(
                name: "IX_device_maintenances_ReportedByUserId",
                table: "device_maintenances",
                column: "ReportedByUserId");

            migrationBuilder.CreateIndex(
                name: "IX_device_maintenances_TenantId_BranchId_StartedAt",
                table: "device_maintenances",
                columns: new[] { "TenantId", "BranchId", "StartedAt" });

            migrationBuilder.CreateIndex(
                name: "IX_master_alert_settings_TenantId",
                table: "master_alert_settings",
                column: "TenantId");

            migrationBuilder.CreateIndex(
                name: "IX_master_alert_settings_UserId",
                table: "master_alert_settings",
                column: "UserId",
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "device_maintenances");

            migrationBuilder.DropTable(
                name: "master_alert_settings");

            migrationBuilder.DropColumn(
                name: "AllowedNotificationChannels",
                table: "users");
        }
    }
}
