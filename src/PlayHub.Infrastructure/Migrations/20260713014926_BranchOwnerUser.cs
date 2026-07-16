using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace PlayHub.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class BranchOwnerUser : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<Guid>(
                name: "OwnerUserId",
                table: "branches",
                type: "uniqueidentifier",
                nullable: true);

            migrationBuilder.CreateIndex(
                name: "IX_branches_OwnerUserId",
                table: "branches",
                column: "OwnerUserId");

            migrationBuilder.AddForeignKey(
                name: "FK_branches_users_OwnerUserId",
                table: "branches",
                column: "OwnerUserId",
                principalTable: "users",
                principalColumn: "Id");

            // Backfill: link each branch to the earliest master user in the same tenant
            migrationBuilder.Sql("""
                UPDATE b
                SET OwnerUserId = (
                    SELECT TOP 1 u.Id
                    FROM users u
                    WHERE u.TenantId = b.TenantId
                      AND u.IsMaster = 1
                      AND u.IsDeleted = 0
                    ORDER BY u.CreatedAt
                )
                FROM branches b
                WHERE b.OwnerUserId IS NULL;
                """);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_branches_users_OwnerUserId",
                table: "branches");

            migrationBuilder.DropIndex(
                name: "IX_branches_OwnerUserId",
                table: "branches");

            migrationBuilder.DropColumn(
                name: "OwnerUserId",
                table: "branches");
        }
    }
}
