using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace PlayHub.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class UserRolesVenueAssetsSessionConvert : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<Guid>(
                name: "ParentUserId",
                table: "users",
                type: "uniqueidentifier",
                nullable: true);

            migrationBuilder.AddColumn<short>(
                name: "Role",
                table: "users",
                type: "smallint",
                nullable: false,
                defaultValue: (short)0);

            // Existing masters become SuperAdmin (Role=2); staff stay Staff (0)
            migrationBuilder.Sql("UPDATE users SET Role = 2 WHERE IsMaster = 1");

            migrationBuilder.AddColumn<decimal>(
                name: "AccruedTimeCost",
                table: "sessions",
                type: "decimal(18,2)",
                precision: 18,
                scale: 2,
                nullable: false,
                defaultValue: 0m);

            migrationBuilder.AddColumn<DateTime>(
                name: "OriginalStartedAt",
                table: "sessions",
                type: "datetime2",
                nullable: true);

            migrationBuilder.CreateTable(
                name: "venue_asset_types",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    TenantId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    Name = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: false),
                    Description = table.Column<string>(type: "nvarchar(300)", maxLength: 300, nullable: true),
                    IsActive = table.Column<bool>(type: "bit", nullable: false),
                    IsDeleted = table.Column<bool>(type: "bit", nullable: false),
                    DeletedAt = table.Column<DateTime>(type: "datetime2", nullable: true),
                    DeletedByUserId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    CreatedAt = table.Column<DateTime>(type: "datetime2", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_venue_asset_types", x => x.Id);
                    table.ForeignKey(
                        name: "FK_venue_asset_types_tenants_TenantId",
                        column: x => x.TenantId,
                        principalTable: "tenants",
                        principalColumn: "Id");
                });

            migrationBuilder.CreateTable(
                name: "room_assets",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    RoomId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    VenueAssetTypeId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    Quantity = table.Column<int>(type: "int", nullable: false),
                    WorkingCount = table.Column<int>(type: "int", nullable: false),
                    Notes = table.Column<string>(type: "nvarchar(300)", maxLength: 300, nullable: true),
                    CreatedAt = table.Column<DateTime>(type: "datetime2", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_room_assets", x => x.Id);
                    table.ForeignKey(
                        name: "FK_room_assets_Rooms_RoomId",
                        column: x => x.RoomId,
                        principalTable: "Rooms",
                        principalColumn: "Id");
                    table.ForeignKey(
                        name: "FK_room_assets_venue_asset_types_VenueAssetTypeId",
                        column: x => x.VenueAssetTypeId,
                        principalTable: "venue_asset_types",
                        principalColumn: "Id");
                });

            migrationBuilder.CreateIndex(
                name: "IX_users_ParentUserId",
                table: "users",
                column: "ParentUserId");

            migrationBuilder.CreateIndex(
                name: "IX_room_assets_RoomId_VenueAssetTypeId",
                table: "room_assets",
                columns: new[] { "RoomId", "VenueAssetTypeId" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_room_assets_VenueAssetTypeId",
                table: "room_assets",
                column: "VenueAssetTypeId");

            migrationBuilder.CreateIndex(
                name: "IX_venue_asset_types_TenantId_Name",
                table: "venue_asset_types",
                columns: new[] { "TenantId", "Name" });

            migrationBuilder.AddForeignKey(
                name: "FK_users_users_ParentUserId",
                table: "users",
                column: "ParentUserId",
                principalTable: "users",
                principalColumn: "Id");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_users_users_ParentUserId",
                table: "users");

            migrationBuilder.DropTable(
                name: "room_assets");

            migrationBuilder.DropTable(
                name: "venue_asset_types");

            migrationBuilder.DropIndex(
                name: "IX_users_ParentUserId",
                table: "users");

            migrationBuilder.DropColumn(
                name: "ParentUserId",
                table: "users");

            migrationBuilder.DropColumn(
                name: "Role",
                table: "users");

            migrationBuilder.DropColumn(
                name: "AccruedTimeCost",
                table: "sessions");

            migrationBuilder.DropColumn(
                name: "OriginalStartedAt",
                table: "sessions");
        }
    }
}
