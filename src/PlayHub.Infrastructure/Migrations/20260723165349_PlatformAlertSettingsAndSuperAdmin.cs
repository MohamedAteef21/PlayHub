using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace PlayHub.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class PlatformAlertSettingsAndSuperAdmin : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "platform_alert_settings",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    TenantId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    SmtpUsername = table.Column<string>(type: "nvarchar(256)", maxLength: 256, nullable: true),
                    SmtpPassword = table.Column<string>(type: "nvarchar(500)", maxLength: 500, nullable: true),
                    SenderDisplayName = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: true),
                    WhatsAppIntegrationApiBaseUrl = table.Column<string>(type: "nvarchar(500)", maxLength: 500, nullable: true),
                    WhatsAppIntegrationApiKey = table.Column<string>(type: "nvarchar(500)", maxLength: 500, nullable: true),
                    WhatsAppIntegrationEnabled = table.Column<bool>(type: "bit", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "datetime2", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_platform_alert_settings", x => x.Id);
                    table.ForeignKey(
                        name: "FK_platform_alert_settings_tenants_TenantId",
                        column: x => x.TenantId,
                        principalTable: "tenants",
                        principalColumn: "Id");
                });

            migrationBuilder.CreateIndex(
                name: "IX_platform_alert_settings_TenantId",
                table: "platform_alert_settings",
                column: "TenantId",
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "platform_alert_settings");
        }
    }
}
