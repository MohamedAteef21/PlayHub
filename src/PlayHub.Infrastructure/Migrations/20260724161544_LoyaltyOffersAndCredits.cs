using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace PlayHub.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class LoyaltyOffersAndCredits : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "loyalty_offers",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    TenantId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    OwnerUserId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    BranchId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    Title = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: false),
                    Description = table.Column<string>(type: "nvarchar(2000)", maxLength: 2000, nullable: true),
                    IsActive = table.Column<bool>(type: "bit", nullable: false),
                    StartsAt = table.Column<DateTime>(type: "datetime2", nullable: true),
                    EndsAt = table.Column<DateTime>(type: "datetime2", nullable: true),
                    PlayerScope = table.Column<short>(type: "smallint", nullable: false),
                    Fulfillment = table.Column<short>(type: "smallint", nullable: false),
                    ConditionLogic = table.Column<short>(type: "smallint", nullable: false),
                    IsDeleted = table.Column<bool>(type: "bit", nullable: false),
                    DeletedAt = table.Column<DateTime>(type: "datetime2", nullable: true),
                    DeletedByUserId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    CreatedAt = table.Column<DateTime>(type: "datetime2", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_loyalty_offers", x => x.Id);
                    table.ForeignKey(
                        name: "FK_loyalty_offers_branches_BranchId",
                        column: x => x.BranchId,
                        principalTable: "branches",
                        principalColumn: "Id");
                    table.ForeignKey(
                        name: "FK_loyalty_offers_tenants_TenantId",
                        column: x => x.TenantId,
                        principalTable: "tenants",
                        principalColumn: "Id");
                    table.ForeignKey(
                        name: "FK_loyalty_offers_users_OwnerUserId",
                        column: x => x.OwnerUserId,
                        principalTable: "users",
                        principalColumn: "Id");
                });

            migrationBuilder.CreateTable(
                name: "loyalty_credits",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    TenantId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    CustomerId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    OfferId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    SourceSessionId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    RewardMetric = table.Column<short>(type: "smallint", nullable: false),
                    QuantityOriginal = table.Column<decimal>(type: "decimal(18,2)", precision: 18, scale: 2, nullable: false),
                    QuantityRemaining = table.Column<decimal>(type: "decimal(18,2)", precision: 18, scale: 2, nullable: false),
                    CafeteriaItemId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    VariantId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    Status = table.Column<short>(type: "smallint", nullable: false),
                    ExpiresAt = table.Column<DateTime>(type: "datetime2", nullable: true),
                    RedeemedOnSessionId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    CreatedAt = table.Column<DateTime>(type: "datetime2", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_loyalty_credits", x => x.Id);
                    table.ForeignKey(
                        name: "FK_loyalty_credits_CafeteriaItemVariants_VariantId",
                        column: x => x.VariantId,
                        principalTable: "CafeteriaItemVariants",
                        principalColumn: "Id");
                    table.ForeignKey(
                        name: "FK_loyalty_credits_cafeteria_items_CafeteriaItemId",
                        column: x => x.CafeteriaItemId,
                        principalTable: "cafeteria_items",
                        principalColumn: "Id");
                    table.ForeignKey(
                        name: "FK_loyalty_credits_customers_CustomerId",
                        column: x => x.CustomerId,
                        principalTable: "customers",
                        principalColumn: "Id");
                    table.ForeignKey(
                        name: "FK_loyalty_credits_loyalty_offers_OfferId",
                        column: x => x.OfferId,
                        principalTable: "loyalty_offers",
                        principalColumn: "Id");
                    table.ForeignKey(
                        name: "FK_loyalty_credits_sessions_SourceSessionId",
                        column: x => x.SourceSessionId,
                        principalTable: "sessions",
                        principalColumn: "Id");
                    table.ForeignKey(
                        name: "FK_loyalty_credits_tenants_TenantId",
                        column: x => x.TenantId,
                        principalTable: "tenants",
                        principalColumn: "Id");
                });

            migrationBuilder.CreateTable(
                name: "loyalty_offer_conditions",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    OfferId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    Metric = table.Column<short>(type: "smallint", nullable: false),
                    RequiredQuantity = table.Column<decimal>(type: "decimal(18,2)", precision: 18, scale: 2, nullable: false),
                    WindowDays = table.Column<int>(type: "int", nullable: true),
                    CafeteriaItemId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    VariantId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    CreatedAt = table.Column<DateTime>(type: "datetime2", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_loyalty_offer_conditions", x => x.Id);
                    table.ForeignKey(
                        name: "FK_loyalty_offer_conditions_CafeteriaItemVariants_VariantId",
                        column: x => x.VariantId,
                        principalTable: "CafeteriaItemVariants",
                        principalColumn: "Id");
                    table.ForeignKey(
                        name: "FK_loyalty_offer_conditions_cafeteria_items_CafeteriaItemId",
                        column: x => x.CafeteriaItemId,
                        principalTable: "cafeteria_items",
                        principalColumn: "Id");
                    table.ForeignKey(
                        name: "FK_loyalty_offer_conditions_loyalty_offers_OfferId",
                        column: x => x.OfferId,
                        principalTable: "loyalty_offers",
                        principalColumn: "Id");
                });

            migrationBuilder.CreateTable(
                name: "loyalty_offer_devices",
                columns: table => new
                {
                    OfferId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    DeviceId = table.Column<Guid>(type: "uniqueidentifier", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_loyalty_offer_devices", x => new { x.OfferId, x.DeviceId });
                    table.ForeignKey(
                        name: "FK_loyalty_offer_devices_Devices_DeviceId",
                        column: x => x.DeviceId,
                        principalTable: "Devices",
                        principalColumn: "Id");
                    table.ForeignKey(
                        name: "FK_loyalty_offer_devices_loyalty_offers_OfferId",
                        column: x => x.OfferId,
                        principalTable: "loyalty_offers",
                        principalColumn: "Id");
                });

            migrationBuilder.CreateTable(
                name: "loyalty_offer_rewards",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    OfferId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    Metric = table.Column<short>(type: "smallint", nullable: false),
                    Quantity = table.Column<decimal>(type: "decimal(18,2)", precision: 18, scale: 2, nullable: false),
                    CafeteriaItemId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    VariantId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    CreatedAt = table.Column<DateTime>(type: "datetime2", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_loyalty_offer_rewards", x => x.Id);
                    table.ForeignKey(
                        name: "FK_loyalty_offer_rewards_CafeteriaItemVariants_VariantId",
                        column: x => x.VariantId,
                        principalTable: "CafeteriaItemVariants",
                        principalColumn: "Id");
                    table.ForeignKey(
                        name: "FK_loyalty_offer_rewards_cafeteria_items_CafeteriaItemId",
                        column: x => x.CafeteriaItemId,
                        principalTable: "cafeteria_items",
                        principalColumn: "Id");
                    table.ForeignKey(
                        name: "FK_loyalty_offer_rewards_loyalty_offers_OfferId",
                        column: x => x.OfferId,
                        principalTable: "loyalty_offers",
                        principalColumn: "Id");
                });

            migrationBuilder.CreateIndex(
                name: "IX_loyalty_credits_CafeteriaItemId",
                table: "loyalty_credits",
                column: "CafeteriaItemId");

            migrationBuilder.CreateIndex(
                name: "IX_loyalty_credits_CustomerId_Status",
                table: "loyalty_credits",
                columns: new[] { "CustomerId", "Status" });

            migrationBuilder.CreateIndex(
                name: "IX_loyalty_credits_OfferId",
                table: "loyalty_credits",
                column: "OfferId");

            migrationBuilder.CreateIndex(
                name: "IX_loyalty_credits_SourceSessionId_OfferId",
                table: "loyalty_credits",
                columns: new[] { "SourceSessionId", "OfferId" });

            migrationBuilder.CreateIndex(
                name: "IX_loyalty_credits_TenantId",
                table: "loyalty_credits",
                column: "TenantId");

            migrationBuilder.CreateIndex(
                name: "IX_loyalty_credits_VariantId",
                table: "loyalty_credits",
                column: "VariantId");

            migrationBuilder.CreateIndex(
                name: "IX_loyalty_offer_conditions_CafeteriaItemId",
                table: "loyalty_offer_conditions",
                column: "CafeteriaItemId");

            migrationBuilder.CreateIndex(
                name: "IX_loyalty_offer_conditions_OfferId",
                table: "loyalty_offer_conditions",
                column: "OfferId");

            migrationBuilder.CreateIndex(
                name: "IX_loyalty_offer_conditions_VariantId",
                table: "loyalty_offer_conditions",
                column: "VariantId");

            migrationBuilder.CreateIndex(
                name: "IX_loyalty_offer_devices_DeviceId",
                table: "loyalty_offer_devices",
                column: "DeviceId");

            migrationBuilder.CreateIndex(
                name: "IX_loyalty_offer_rewards_CafeteriaItemId",
                table: "loyalty_offer_rewards",
                column: "CafeteriaItemId");

            migrationBuilder.CreateIndex(
                name: "IX_loyalty_offer_rewards_OfferId",
                table: "loyalty_offer_rewards",
                column: "OfferId");

            migrationBuilder.CreateIndex(
                name: "IX_loyalty_offer_rewards_VariantId",
                table: "loyalty_offer_rewards",
                column: "VariantId");

            migrationBuilder.CreateIndex(
                name: "IX_loyalty_offers_BranchId",
                table: "loyalty_offers",
                column: "BranchId");

            migrationBuilder.CreateIndex(
                name: "IX_loyalty_offers_OwnerUserId",
                table: "loyalty_offers",
                column: "OwnerUserId");

            migrationBuilder.CreateIndex(
                name: "IX_loyalty_offers_TenantId_IsActive",
                table: "loyalty_offers",
                columns: new[] { "TenantId", "IsActive" });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "loyalty_credits");

            migrationBuilder.DropTable(
                name: "loyalty_offer_conditions");

            migrationBuilder.DropTable(
                name: "loyalty_offer_devices");

            migrationBuilder.DropTable(
                name: "loyalty_offer_rewards");

            migrationBuilder.DropTable(
                name: "loyalty_offers");
        }
    }
}
