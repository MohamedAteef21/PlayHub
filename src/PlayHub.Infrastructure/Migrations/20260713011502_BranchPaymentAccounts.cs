using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace PlayHub.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class BranchPaymentAccounts : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "BankTransferAccount",
                table: "branches",
                type: "nvarchar(max)",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "DigitalWalletAccount",
                table: "branches",
                type: "nvarchar(max)",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "SharedTransferAccount",
                table: "branches",
                type: "nvarchar(max)",
                nullable: true);

            migrationBuilder.AddColumn<bool>(
                name: "UseSharedTransferAccount",
                table: "branches",
                type: "bit",
                nullable: false,
                defaultValue: false);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "BankTransferAccount",
                table: "branches");

            migrationBuilder.DropColumn(
                name: "DigitalWalletAccount",
                table: "branches");

            migrationBuilder.DropColumn(
                name: "SharedTransferAccount",
                table: "branches");

            migrationBuilder.DropColumn(
                name: "UseSharedTransferAccount",
                table: "branches");
        }
    }
}
