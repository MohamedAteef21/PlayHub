using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace PlayHub.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AlertRecipientsMultiselect : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "master_alert_recipients",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    MasterAlertSettingsId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    Email = table.Column<string>(type: "nvarchar(256)", maxLength: 256, nullable: false),
                    DisplayName = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: true),
                    NotifyLowStock = table.Column<bool>(type: "bit", nullable: false),
                    NotifySubscription = table.Column<bool>(type: "bit", nullable: false),
                    NotifyDeviceMaintenance = table.Column<bool>(type: "bit", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "datetime2", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_master_alert_recipients", x => x.Id);
                    table.ForeignKey(
                        name: "FK_master_alert_recipients_master_alert_settings_MasterAlertSettingsId",
                        column: x => x.MasterAlertSettingsId,
                        principalTable: "master_alert_settings",
                        principalColumn: "Id");
                });

            migrationBuilder.CreateIndex(
                name: "IX_master_alert_recipients_MasterAlertSettingsId_Email",
                table: "master_alert_recipients",
                columns: new[] { "MasterAlertSettingsId", "Email" },
                unique: true);

            // Migrate the old single recipient + global flags into per-recipient rows.
            migrationBuilder.Sql("""
                INSERT INTO master_alert_recipients
                    (Id, MasterAlertSettingsId, Email, DisplayName, NotifyLowStock, NotifySubscription, NotifyDeviceMaintenance, CreatedAt)
                SELECT
                    NEWID(),
                    s.Id,
                    LTRIM(RTRIM(s.AlertRecipientEmail)),
                    NULL,
                    s.NotifyLowStock,
                    s.NotifySubscription,
                    s.NotifyDeviceMaintenance,
                    SYSUTCDATETIME()
                FROM master_alert_settings s
                WHERE s.AlertRecipientEmail IS NOT NULL
                  AND LTRIM(RTRIM(s.AlertRecipientEmail)) <> '';
                """);

            migrationBuilder.Sql("""
                UPDATE master_alert_settings
                SET SmtpHost = 'smtp.gmail.com',
                    SmtpPort = 587,
                    SenderDisplayName = 'PlayHub System';
                """);

            migrationBuilder.DropColumn(
                name: "AlertRecipientEmail",
                table: "master_alert_settings");

            migrationBuilder.DropColumn(
                name: "NotifyDeviceMaintenance",
                table: "master_alert_settings");

            migrationBuilder.DropColumn(
                name: "NotifyLowStock",
                table: "master_alert_settings");

            migrationBuilder.DropColumn(
                name: "NotifySubscription",
                table: "master_alert_settings");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "AlertRecipientEmail",
                table: "master_alert_settings",
                type: "nvarchar(256)",
                maxLength: 256,
                nullable: true);

            migrationBuilder.AddColumn<bool>(
                name: "NotifyDeviceMaintenance",
                table: "master_alert_settings",
                type: "bit",
                nullable: false,
                defaultValue: true);

            migrationBuilder.AddColumn<bool>(
                name: "NotifyLowStock",
                table: "master_alert_settings",
                type: "bit",
                nullable: false,
                defaultValue: true);

            migrationBuilder.AddColumn<bool>(
                name: "NotifySubscription",
                table: "master_alert_settings",
                type: "bit",
                nullable: false,
                defaultValue: true);

            migrationBuilder.Sql("""
                UPDATE s
                SET
                    s.AlertRecipientEmail = r.Email,
                    s.NotifyLowStock = r.NotifyLowStock,
                    s.NotifySubscription = r.NotifySubscription,
                    s.NotifyDeviceMaintenance = r.NotifyDeviceMaintenance
                FROM master_alert_settings s
                CROSS APPLY (
                    SELECT TOP 1 *
                    FROM master_alert_recipients x
                    WHERE x.MasterAlertSettingsId = s.Id
                    ORDER BY x.CreatedAt
                ) r;
                """);

            migrationBuilder.DropTable(
                name: "master_alert_recipients");
        }
    }
}
