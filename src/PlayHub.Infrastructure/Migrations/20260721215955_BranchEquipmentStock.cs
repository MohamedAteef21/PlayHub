using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace PlayHub.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class BranchEquipmentStock : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "branch_equipment",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    TenantId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    BranchId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    Name = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: false),
                    Kind = table.Column<short>(type: "smallint", nullable: false),
                    TotalQuantity = table.Column<int>(type: "int", nullable: false),
                    MaintenanceQuantity = table.Column<int>(type: "int", nullable: false),
                    IsActive = table.Column<bool>(type: "bit", nullable: false),
                    IsDeleted = table.Column<bool>(type: "bit", nullable: false),
                    DeletedAt = table.Column<DateTime>(type: "datetime2", nullable: true),
                    DeletedByUserId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    CreatedAt = table.Column<DateTime>(type: "datetime2", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_branch_equipment", x => x.Id);
                    table.ForeignKey(
                        name: "FK_branch_equipment_branches_BranchId",
                        column: x => x.BranchId,
                        principalTable: "branches",
                        principalColumn: "Id");
                });

            migrationBuilder.CreateTable(
                name: "session_equipment_allocations",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    SessionId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    BranchEquipmentId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    Quantity = table.Column<int>(type: "int", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "datetime2", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_session_equipment_allocations", x => x.Id);
                    table.ForeignKey(
                        name: "FK_session_equipment_allocations_branch_equipment_BranchEquipmentId",
                        column: x => x.BranchEquipmentId,
                        principalTable: "branch_equipment",
                        principalColumn: "Id");
                    table.ForeignKey(
                        name: "FK_session_equipment_allocations_sessions_SessionId",
                        column: x => x.SessionId,
                        principalTable: "sessions",
                        principalColumn: "Id");
                });

            migrationBuilder.CreateIndex(
                name: "IX_branch_equipment_BranchId_Kind_Name",
                table: "branch_equipment",
                columns: new[] { "BranchId", "Kind", "Name" });

            migrationBuilder.CreateIndex(
                name: "IX_session_equipment_allocations_BranchEquipmentId",
                table: "session_equipment_allocations",
                column: "BranchEquipmentId");

            migrationBuilder.CreateIndex(
                name: "IX_session_equipment_allocations_SessionId_BranchEquipmentId",
                table: "session_equipment_allocations",
                columns: new[] { "SessionId", "BranchEquipmentId" });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "session_equipment_allocations");

            migrationBuilder.DropTable(
                name: "branch_equipment");
        }
    }
}
