using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

#pragma warning disable CA1814 // Prefer jagged arrays over multidimensional

namespace PlayHub.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class InitialCreate : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "permissions",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    Code = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: false),
                    Module = table.Column<string>(type: "nvarchar(50)", maxLength: 50, nullable: false),
                    Action = table.Column<string>(type: "nvarchar(50)", maxLength: 50, nullable: false),
                    Description = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    IsSystem = table.Column<bool>(type: "bit", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "datetime2", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_permissions", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "tenants",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    Name = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: false),
                    Slug = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: false),
                    DefaultLanguage = table.Column<string>(type: "nvarchar(5)", maxLength: 5, nullable: false),
                    DefaultCurrency = table.Column<string>(type: "nvarchar(3)", maxLength: 3, nullable: false),
                    Timezone = table.Column<string>(type: "nvarchar(50)", maxLength: 50, nullable: false),
                    BillingRoundUp = table.Column<bool>(type: "bit", nullable: false),
                    IsActive = table.Column<bool>(type: "bit", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "datetime2", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_tenants", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "branches",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    TenantId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    Name = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: false),
                    Address = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    Phone = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    InvoicePrefix = table.Column<string>(type: "nvarchar(20)", maxLength: 20, nullable: false),
                    NextInvoiceNumber = table.Column<int>(type: "int", nullable: false),
                    IsActive = table.Column<bool>(type: "bit", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "datetime2", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_branches", x => x.Id);
                    table.ForeignKey(
                        name: "FK_branches_tenants_TenantId",
                        column: x => x.TenantId,
                        principalTable: "tenants",
                        principalColumn: "Id");
                });

            migrationBuilder.CreateTable(
                name: "ControllerTypes",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    TenantId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    Name = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    Description = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    IsActive = table.Column<bool>(type: "bit", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "datetime2", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ControllerTypes", x => x.Id);
                    table.ForeignKey(
                        name: "FK_ControllerTypes_tenants_TenantId",
                        column: x => x.TenantId,
                        principalTable: "tenants",
                        principalColumn: "Id");
                });

            migrationBuilder.CreateTable(
                name: "ExpenseCategories",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    TenantId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    Name = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    NameAr = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    IsActive = table.Column<bool>(type: "bit", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "datetime2", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ExpenseCategories", x => x.Id);
                    table.ForeignKey(
                        name: "FK_ExpenseCategories_tenants_TenantId",
                        column: x => x.TenantId,
                        principalTable: "tenants",
                        principalColumn: "Id");
                });

            migrationBuilder.CreateTable(
                name: "users",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    TenantId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    Email = table.Column<string>(type: "nvarchar(256)", maxLength: 256, nullable: false),
                    PasswordHash = table.Column<string>(type: "nvarchar(500)", maxLength: 500, nullable: false),
                    FirstName = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: false),
                    LastName = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: false),
                    IsMaster = table.Column<bool>(type: "bit", nullable: false),
                    PreferredLanguage = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    IsActive = table.Column<bool>(type: "bit", nullable: false),
                    LastLoginAt = table.Column<DateTime>(type: "datetime2", nullable: true),
                    CreatedAt = table.Column<DateTime>(type: "datetime2", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_users", x => x.Id);
                    table.ForeignKey(
                        name: "FK_users_tenants_TenantId",
                        column: x => x.TenantId,
                        principalTable: "tenants",
                        principalColumn: "Id");
                });

            migrationBuilder.CreateTable(
                name: "cafeteria_items",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    TenantId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    BranchId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    Name = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    NameAr = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    SellPrice = table.Column<decimal>(type: "decimal(18,2)", precision: 18, scale: 2, nullable: false),
                    CurrentQuantity = table.Column<int>(type: "int", nullable: false),
                    MinThreshold = table.Column<int>(type: "int", nullable: false),
                    IsActive = table.Column<bool>(type: "bit", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "datetime2", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_cafeteria_items", x => x.Id);
                    table.ForeignKey(
                        name: "FK_cafeteria_items_branches_BranchId",
                        column: x => x.BranchId,
                        principalTable: "branches",
                        principalColumn: "Id");
                });

            migrationBuilder.CreateTable(
                name: "PricingPlans",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    TenantId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    BranchId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    Name = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    SessionMode = table.Column<short>(type: "smallint", nullable: false),
                    TimeUnit = table.Column<short>(type: "smallint", nullable: false),
                    IsActive = table.Column<bool>(type: "bit", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "datetime2", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_PricingPlans", x => x.Id);
                    table.ForeignKey(
                        name: "FK_PricingPlans_branches_BranchId",
                        column: x => x.BranchId,
                        principalTable: "branches",
                        principalColumn: "Id");
                    table.ForeignKey(
                        name: "FK_PricingPlans_tenants_TenantId",
                        column: x => x.TenantId,
                        principalTable: "tenants",
                        principalColumn: "Id");
                });

            migrationBuilder.CreateTable(
                name: "Rooms",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    TenantId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    BranchId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    Name = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    RoomNumber = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    MaxWatchingCapacity = table.Column<int>(type: "int", nullable: false),
                    IsActive = table.Column<bool>(type: "bit", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "datetime2", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Rooms", x => x.Id);
                    table.ForeignKey(
                        name: "FK_Rooms_branches_BranchId",
                        column: x => x.BranchId,
                        principalTable: "branches",
                        principalColumn: "Id");
                });

            migrationBuilder.CreateTable(
                name: "audit_logs",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    TenantId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    BranchId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    UserId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    UserName = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: false),
                    ActionType = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: false),
                    EntityType = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: false),
                    EntityId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    Details = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    IpAddress = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    Success = table.Column<bool>(type: "bit", nullable: false),
                    Timestamp = table.Column<DateTime>(type: "datetime2", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "datetime2", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_audit_logs", x => x.Id);
                    table.ForeignKey(
                        name: "FK_audit_logs_branches_BranchId",
                        column: x => x.BranchId,
                        principalTable: "branches",
                        principalColumn: "Id");
                    table.ForeignKey(
                        name: "FK_audit_logs_tenants_TenantId",
                        column: x => x.TenantId,
                        principalTable: "tenants",
                        principalColumn: "Id");
                    table.ForeignKey(
                        name: "FK_audit_logs_users_UserId",
                        column: x => x.UserId,
                        principalTable: "users",
                        principalColumn: "Id");
                });

            migrationBuilder.CreateTable(
                name: "Notifications",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    TenantId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    UserId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    Type = table.Column<short>(type: "smallint", nullable: false),
                    Title = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    TitleAr = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    Message = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    MessageAr = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    RelatedEntityType = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    RelatedEntityId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    IsRead = table.Column<bool>(type: "bit", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "datetime2", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Notifications", x => x.Id);
                    table.ForeignKey(
                        name: "FK_Notifications_tenants_TenantId",
                        column: x => x.TenantId,
                        principalTable: "tenants",
                        principalColumn: "Id");
                    table.ForeignKey(
                        name: "FK_Notifications_users_UserId",
                        column: x => x.UserId,
                        principalTable: "users",
                        principalColumn: "Id");
                });

            migrationBuilder.CreateTable(
                name: "PurchaseOrders",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    TenantId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    BranchId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    SupplierName = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    Status = table.Column<short>(type: "smallint", nullable: false),
                    TotalCost = table.Column<decimal>(type: "decimal(18,2)", nullable: false),
                    OrderedAt = table.Column<DateTime>(type: "datetime2", nullable: true),
                    ReceivedAt = table.Column<DateTime>(type: "datetime2", nullable: true),
                    ReceivedByUserId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    CreatedByUserId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "datetime2", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_PurchaseOrders", x => x.Id);
                    table.ForeignKey(
                        name: "FK_PurchaseOrders_branches_BranchId",
                        column: x => x.BranchId,
                        principalTable: "branches",
                        principalColumn: "Id");
                    table.ForeignKey(
                        name: "FK_PurchaseOrders_users_CreatedByUserId",
                        column: x => x.CreatedByUserId,
                        principalTable: "users",
                        principalColumn: "Id");
                    table.ForeignKey(
                        name: "FK_PurchaseOrders_users_ReceivedByUserId",
                        column: x => x.ReceivedByUserId,
                        principalTable: "users",
                        principalColumn: "Id");
                });

            migrationBuilder.CreateTable(
                name: "refresh_tokens",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    UserId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    TokenHash = table.Column<string>(type: "nvarchar(500)", maxLength: 500, nullable: false),
                    ExpiresAt = table.Column<DateTime>(type: "datetime2", nullable: false),
                    RevokedAt = table.Column<DateTime>(type: "datetime2", nullable: true),
                    ReplacedByTokenId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    CreatedAt = table.Column<DateTime>(type: "datetime2", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_refresh_tokens", x => x.Id);
                    table.ForeignKey(
                        name: "FK_refresh_tokens_users_UserId",
                        column: x => x.UserId,
                        principalTable: "users",
                        principalColumn: "Id");
                });

            migrationBuilder.CreateTable(
                name: "user_branches",
                columns: table => new
                {
                    UserId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    BranchId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    IsDefault = table.Column<bool>(type: "bit", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_user_branches", x => new { x.UserId, x.BranchId });
                    table.ForeignKey(
                        name: "FK_user_branches_branches_BranchId",
                        column: x => x.BranchId,
                        principalTable: "branches",
                        principalColumn: "Id");
                    table.ForeignKey(
                        name: "FK_user_branches_users_UserId",
                        column: x => x.UserId,
                        principalTable: "users",
                        principalColumn: "Id");
                });

            migrationBuilder.CreateTable(
                name: "user_permissions",
                columns: table => new
                {
                    UserId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    PermissionId = table.Column<Guid>(type: "uniqueidentifier", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_user_permissions", x => new { x.UserId, x.PermissionId });
                    table.ForeignKey(
                        name: "FK_user_permissions_permissions_PermissionId",
                        column: x => x.PermissionId,
                        principalTable: "permissions",
                        principalColumn: "Id");
                    table.ForeignKey(
                        name: "FK_user_permissions_users_UserId",
                        column: x => x.UserId,
                        principalTable: "users",
                        principalColumn: "Id");
                });

            migrationBuilder.CreateTable(
                name: "InventoryMovements",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    TenantId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    BranchId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    CafeteriaItemId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    MovementType = table.Column<short>(type: "smallint", nullable: false),
                    QuantityChange = table.Column<int>(type: "int", nullable: false),
                    ReferenceType = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    ReferenceId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    Notes = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    PerformedByUserId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "datetime2", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_InventoryMovements", x => x.Id);
                    table.ForeignKey(
                        name: "FK_InventoryMovements_branches_BranchId",
                        column: x => x.BranchId,
                        principalTable: "branches",
                        principalColumn: "Id");
                    table.ForeignKey(
                        name: "FK_InventoryMovements_cafeteria_items_CafeteriaItemId",
                        column: x => x.CafeteriaItemId,
                        principalTable: "cafeteria_items",
                        principalColumn: "Id");
                    table.ForeignKey(
                        name: "FK_InventoryMovements_users_PerformedByUserId",
                        column: x => x.PerformedByUserId,
                        principalTable: "users",
                        principalColumn: "Id");
                });

            migrationBuilder.CreateTable(
                name: "GamingRates",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    PricingPlanId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    ControllerCount = table.Column<int>(type: "int", nullable: false),
                    Rate = table.Column<decimal>(type: "decimal(18,2)", precision: 18, scale: 2, nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "datetime2", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_GamingRates", x => x.Id);
                    table.ForeignKey(
                        name: "FK_GamingRates_PricingPlans_PricingPlanId",
                        column: x => x.PricingPlanId,
                        principalTable: "PricingPlans",
                        principalColumn: "Id");
                });

            migrationBuilder.CreateTable(
                name: "WatchingRates",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    PricingPlanId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    RatePerPerson = table.Column<decimal>(type: "decimal(18,2)", precision: 18, scale: 2, nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "datetime2", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_WatchingRates", x => x.Id);
                    table.ForeignKey(
                        name: "FK_WatchingRates_PricingPlans_PricingPlanId",
                        column: x => x.PricingPlanId,
                        principalTable: "PricingPlans",
                        principalColumn: "Id");
                });

            migrationBuilder.CreateTable(
                name: "Devices",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    TenantId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    BranchId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    RoomId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    Identifier = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    Name = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    IsActive = table.Column<bool>(type: "bit", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "datetime2", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Devices", x => x.Id);
                    table.ForeignKey(
                        name: "FK_Devices_Rooms_RoomId",
                        column: x => x.RoomId,
                        principalTable: "Rooms",
                        principalColumn: "Id");
                    table.ForeignKey(
                        name: "FK_Devices_branches_BranchId",
                        column: x => x.BranchId,
                        principalTable: "branches",
                        principalColumn: "Id");
                });

            migrationBuilder.CreateTable(
                name: "expenses",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    TenantId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    BranchId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    CategoryId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    PurchaseOrderId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    Amount = table.Column<decimal>(type: "decimal(18,2)", precision: 18, scale: 2, nullable: false),
                    Description = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    ExpenseDate = table.Column<DateOnly>(type: "date", nullable: false),
                    RecordedByUserId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    IsDeleted = table.Column<bool>(type: "bit", nullable: false),
                    DeletedAt = table.Column<DateTime>(type: "datetime2", nullable: true),
                    DeletedByUserId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    CreatedAt = table.Column<DateTime>(type: "datetime2", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_expenses", x => x.Id);
                    table.ForeignKey(
                        name: "FK_expenses_ExpenseCategories_CategoryId",
                        column: x => x.CategoryId,
                        principalTable: "ExpenseCategories",
                        principalColumn: "Id");
                    table.ForeignKey(
                        name: "FK_expenses_PurchaseOrders_PurchaseOrderId",
                        column: x => x.PurchaseOrderId,
                        principalTable: "PurchaseOrders",
                        principalColumn: "Id");
                    table.ForeignKey(
                        name: "FK_expenses_branches_BranchId",
                        column: x => x.BranchId,
                        principalTable: "branches",
                        principalColumn: "Id");
                    table.ForeignKey(
                        name: "FK_expenses_users_RecordedByUserId",
                        column: x => x.RecordedByUserId,
                        principalTable: "users",
                        principalColumn: "Id");
                });

            migrationBuilder.CreateTable(
                name: "PurchaseOrderLines",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    PurchaseOrderId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    CafeteriaItemId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    OrderedQuantity = table.Column<int>(type: "int", nullable: false),
                    ReceivedQuantity = table.Column<int>(type: "int", nullable: false),
                    UnitCost = table.Column<decimal>(type: "decimal(18,2)", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "datetime2", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_PurchaseOrderLines", x => x.Id);
                    table.ForeignKey(
                        name: "FK_PurchaseOrderLines_PurchaseOrders_PurchaseOrderId",
                        column: x => x.PurchaseOrderId,
                        principalTable: "PurchaseOrders",
                        principalColumn: "Id");
                    table.ForeignKey(
                        name: "FK_PurchaseOrderLines_cafeteria_items_CafeteriaItemId",
                        column: x => x.CafeteriaItemId,
                        principalTable: "cafeteria_items",
                        principalColumn: "Id");
                });

            migrationBuilder.CreateTable(
                name: "device_pricing_plans",
                columns: table => new
                {
                    DeviceId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    PricingPlanId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    SessionMode = table.Column<short>(type: "smallint", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_device_pricing_plans", x => new { x.DeviceId, x.PricingPlanId, x.SessionMode });
                    table.ForeignKey(
                        name: "FK_device_pricing_plans_Devices_DeviceId",
                        column: x => x.DeviceId,
                        principalTable: "Devices",
                        principalColumn: "Id");
                    table.ForeignKey(
                        name: "FK_device_pricing_plans_PricingPlans_PricingPlanId",
                        column: x => x.PricingPlanId,
                        principalTable: "PricingPlans",
                        principalColumn: "Id");
                });

            migrationBuilder.CreateTable(
                name: "DeviceControllers",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    DeviceId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    ControllerTypeId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    Quantity = table.Column<int>(type: "int", nullable: false),
                    WorkingCount = table.Column<int>(type: "int", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "datetime2", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_DeviceControllers", x => x.Id);
                    table.ForeignKey(
                        name: "FK_DeviceControllers_ControllerTypes_ControllerTypeId",
                        column: x => x.ControllerTypeId,
                        principalTable: "ControllerTypes",
                        principalColumn: "Id");
                    table.ForeignKey(
                        name: "FK_DeviceControllers_Devices_DeviceId",
                        column: x => x.DeviceId,
                        principalTable: "Devices",
                        principalColumn: "Id");
                });

            migrationBuilder.CreateTable(
                name: "Screens",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    DeviceId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    Count = table.Column<int>(type: "int", nullable: false),
                    WorkingCount = table.Column<int>(type: "int", nullable: false),
                    Notes = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    CreatedAt = table.Column<DateTime>(type: "datetime2", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Screens", x => x.Id);
                    table.ForeignKey(
                        name: "FK_Screens_Devices_DeviceId",
                        column: x => x.DeviceId,
                        principalTable: "Devices",
                        principalColumn: "Id");
                });

            migrationBuilder.CreateTable(
                name: "sessions",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    TenantId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    BranchId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    DeviceId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    RoomId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    SessionMode = table.Column<short>(type: "smallint", nullable: false),
                    PricingPlanId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    ControllerCount = table.Column<int>(type: "int", nullable: true),
                    WatcherCount = table.Column<int>(type: "int", nullable: true),
                    RateSnapshot = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    Status = table.Column<short>(type: "smallint", nullable: false),
                    OpenedByUserId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    ClosedByUserId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    StartedAt = table.Column<DateTime>(type: "datetime2", nullable: false),
                    ClosedAt = table.Column<DateTime>(type: "datetime2", nullable: true),
                    TotalPausedSeconds = table.Column<int>(type: "int", nullable: false),
                    TimeCost = table.Column<decimal>(type: "decimal(18,2)", precision: 18, scale: 2, nullable: false),
                    CafeteriaCost = table.Column<decimal>(type: "decimal(18,2)", precision: 18, scale: 2, nullable: false),
                    TotalCost = table.Column<decimal>(type: "decimal(18,2)", precision: 18, scale: 2, nullable: false),
                    IsDeleted = table.Column<bool>(type: "bit", nullable: false),
                    DeletedAt = table.Column<DateTime>(type: "datetime2", nullable: true),
                    DeletedByUserId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    CreatedAt = table.Column<DateTime>(type: "datetime2", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_sessions", x => x.Id);
                    table.ForeignKey(
                        name: "FK_sessions_Devices_DeviceId",
                        column: x => x.DeviceId,
                        principalTable: "Devices",
                        principalColumn: "Id");
                    table.ForeignKey(
                        name: "FK_sessions_PricingPlans_PricingPlanId",
                        column: x => x.PricingPlanId,
                        principalTable: "PricingPlans",
                        principalColumn: "Id");
                    table.ForeignKey(
                        name: "FK_sessions_Rooms_RoomId",
                        column: x => x.RoomId,
                        principalTable: "Rooms",
                        principalColumn: "Id");
                    table.ForeignKey(
                        name: "FK_sessions_branches_BranchId",
                        column: x => x.BranchId,
                        principalTable: "branches",
                        principalColumn: "Id");
                    table.ForeignKey(
                        name: "FK_sessions_users_ClosedByUserId",
                        column: x => x.ClosedByUserId,
                        principalTable: "users",
                        principalColumn: "Id");
                    table.ForeignKey(
                        name: "FK_sessions_users_OpenedByUserId",
                        column: x => x.OpenedByUserId,
                        principalTable: "users",
                        principalColumn: "Id");
                });

            migrationBuilder.CreateTable(
                name: "CafeteriaSales",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    TenantId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    BranchId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    SessionId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    SoldByUserId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    TotalAmount = table.Column<decimal>(type: "decimal(18,2)", nullable: false),
                    Status = table.Column<short>(type: "smallint", nullable: false),
                    SoldAt = table.Column<DateTime>(type: "datetime2", nullable: false),
                    IsDeleted = table.Column<bool>(type: "bit", nullable: false),
                    DeletedAt = table.Column<DateTime>(type: "datetime2", nullable: true),
                    DeletedByUserId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    CreatedAt = table.Column<DateTime>(type: "datetime2", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_CafeteriaSales", x => x.Id);
                    table.ForeignKey(
                        name: "FK_CafeteriaSales_branches_BranchId",
                        column: x => x.BranchId,
                        principalTable: "branches",
                        principalColumn: "Id");
                    table.ForeignKey(
                        name: "FK_CafeteriaSales_sessions_SessionId",
                        column: x => x.SessionId,
                        principalTable: "sessions",
                        principalColumn: "Id");
                    table.ForeignKey(
                        name: "FK_CafeteriaSales_users_SoldByUserId",
                        column: x => x.SoldByUserId,
                        principalTable: "users",
                        principalColumn: "Id");
                });

            migrationBuilder.CreateTable(
                name: "SessionCafeteriaLines",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    SessionId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    CafeteriaItemId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    Quantity = table.Column<int>(type: "int", nullable: false),
                    UnitPrice = table.Column<decimal>(type: "decimal(18,2)", nullable: false),
                    LineTotal = table.Column<decimal>(type: "decimal(18,2)", nullable: false),
                    AddedByUserId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    AddedAt = table.Column<DateTime>(type: "datetime2", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "datetime2", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_SessionCafeteriaLines", x => x.Id);
                    table.ForeignKey(
                        name: "FK_SessionCafeteriaLines_cafeteria_items_CafeteriaItemId",
                        column: x => x.CafeteriaItemId,
                        principalTable: "cafeteria_items",
                        principalColumn: "Id");
                    table.ForeignKey(
                        name: "FK_SessionCafeteriaLines_sessions_SessionId",
                        column: x => x.SessionId,
                        principalTable: "sessions",
                        principalColumn: "Id");
                    table.ForeignKey(
                        name: "FK_SessionCafeteriaLines_users_AddedByUserId",
                        column: x => x.AddedByUserId,
                        principalTable: "users",
                        principalColumn: "Id");
                });

            migrationBuilder.CreateTable(
                name: "SessionPauses",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    SessionId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    PausedAt = table.Column<DateTime>(type: "datetime2", nullable: false),
                    ResumedAt = table.Column<DateTime>(type: "datetime2", nullable: true),
                    PausedByUserId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "datetime2", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_SessionPauses", x => x.Id);
                    table.ForeignKey(
                        name: "FK_SessionPauses_sessions_SessionId",
                        column: x => x.SessionId,
                        principalTable: "sessions",
                        principalColumn: "Id");
                    table.ForeignKey(
                        name: "FK_SessionPauses_users_PausedByUserId",
                        column: x => x.PausedByUserId,
                        principalTable: "users",
                        principalColumn: "Id");
                });

            migrationBuilder.CreateTable(
                name: "CafeteriaSaleLines",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    SaleId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    CafeteriaItemId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    Quantity = table.Column<int>(type: "int", nullable: false),
                    ReturnedQuantity = table.Column<int>(type: "int", nullable: false),
                    UnitPrice = table.Column<decimal>(type: "decimal(18,2)", nullable: false),
                    LineTotal = table.Column<decimal>(type: "decimal(18,2)", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "datetime2", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_CafeteriaSaleLines", x => x.Id);
                    table.ForeignKey(
                        name: "FK_CafeteriaSaleLines_CafeteriaSales_SaleId",
                        column: x => x.SaleId,
                        principalTable: "CafeteriaSales",
                        principalColumn: "Id");
                    table.ForeignKey(
                        name: "FK_CafeteriaSaleLines_cafeteria_items_CafeteriaItemId",
                        column: x => x.CafeteriaItemId,
                        principalTable: "cafeteria_items",
                        principalColumn: "Id");
                });

            migrationBuilder.CreateTable(
                name: "invoices",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    TenantId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    BranchId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    InvoiceNumber = table.Column<string>(type: "nvarchar(30)", maxLength: 30, nullable: false),
                    InvoiceType = table.Column<short>(type: "smallint", nullable: false),
                    SessionId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    CafeteriaSaleId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    Subtotal = table.Column<decimal>(type: "decimal(18,2)", precision: 18, scale: 2, nullable: false),
                    Total = table.Column<decimal>(type: "decimal(18,2)", precision: 18, scale: 2, nullable: false),
                    Status = table.Column<short>(type: "smallint", nullable: false),
                    ClosedByUserId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    ClosedAt = table.Column<DateTime>(type: "datetime2", nullable: false),
                    IsDeleted = table.Column<bool>(type: "bit", nullable: false),
                    DeletedAt = table.Column<DateTime>(type: "datetime2", nullable: true),
                    DeletedByUserId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    CreatedAt = table.Column<DateTime>(type: "datetime2", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_invoices", x => x.Id);
                    table.ForeignKey(
                        name: "FK_invoices_CafeteriaSales_CafeteriaSaleId",
                        column: x => x.CafeteriaSaleId,
                        principalTable: "CafeteriaSales",
                        principalColumn: "Id");
                    table.ForeignKey(
                        name: "FK_invoices_branches_BranchId",
                        column: x => x.BranchId,
                        principalTable: "branches",
                        principalColumn: "Id");
                    table.ForeignKey(
                        name: "FK_invoices_sessions_SessionId",
                        column: x => x.SessionId,
                        principalTable: "sessions",
                        principalColumn: "Id");
                    table.ForeignKey(
                        name: "FK_invoices_users_ClosedByUserId",
                        column: x => x.ClosedByUserId,
                        principalTable: "users",
                        principalColumn: "Id");
                });

            migrationBuilder.CreateTable(
                name: "CafeteriaReturns",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    SaleId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    SaleLineId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    Quantity = table.Column<int>(type: "int", nullable: false),
                    Reason = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    ReturnedByUserId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    ReturnedAt = table.Column<DateTime>(type: "datetime2", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "datetime2", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_CafeteriaReturns", x => x.Id);
                    table.ForeignKey(
                        name: "FK_CafeteriaReturns_CafeteriaSaleLines_SaleLineId",
                        column: x => x.SaleLineId,
                        principalTable: "CafeteriaSaleLines",
                        principalColumn: "Id");
                    table.ForeignKey(
                        name: "FK_CafeteriaReturns_CafeteriaSales_SaleId",
                        column: x => x.SaleId,
                        principalTable: "CafeteriaSales",
                        principalColumn: "Id");
                    table.ForeignKey(
                        name: "FK_CafeteriaReturns_users_ReturnedByUserId",
                        column: x => x.ReturnedByUserId,
                        principalTable: "users",
                        principalColumn: "Id");
                });

            migrationBuilder.CreateTable(
                name: "InvoicePayments",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    InvoiceId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    PaymentMethod = table.Column<short>(type: "smallint", nullable: false),
                    Amount = table.Column<decimal>(type: "decimal(18,2)", nullable: false),
                    Status = table.Column<short>(type: "smallint", nullable: false),
                    DebtorName = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    DebtorPhone = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    CollectedAt = table.Column<DateTime>(type: "datetime2", nullable: true),
                    CollectionMethod = table.Column<short>(type: "smallint", nullable: true),
                    CollectedByUserId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    CreatedAt = table.Column<DateTime>(type: "datetime2", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_InvoicePayments", x => x.Id);
                    table.ForeignKey(
                        name: "FK_InvoicePayments_invoices_InvoiceId",
                        column: x => x.InvoiceId,
                        principalTable: "invoices",
                        principalColumn: "Id");
                    table.ForeignKey(
                        name: "FK_InvoicePayments_users_CollectedByUserId",
                        column: x => x.CollectedByUserId,
                        principalTable: "users",
                        principalColumn: "Id");
                });

            migrationBuilder.CreateTable(
                name: "revenue_entries",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    TenantId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    BranchId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    InvoiceId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    Amount = table.Column<decimal>(type: "decimal(18,2)", precision: 18, scale: 2, nullable: false),
                    RevenueType = table.Column<short>(type: "smallint", nullable: false),
                    RecordedAt = table.Column<DateTime>(type: "datetime2", nullable: false),
                    IsDeleted = table.Column<bool>(type: "bit", nullable: false),
                    DeletedAt = table.Column<DateTime>(type: "datetime2", nullable: true),
                    DeletedByUserId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    CreatedAt = table.Column<DateTime>(type: "datetime2", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_revenue_entries", x => x.Id);
                    table.ForeignKey(
                        name: "FK_revenue_entries_branches_BranchId",
                        column: x => x.BranchId,
                        principalTable: "branches",
                        principalColumn: "Id");
                    table.ForeignKey(
                        name: "FK_revenue_entries_invoices_InvoiceId",
                        column: x => x.InvoiceId,
                        principalTable: "invoices",
                        principalColumn: "Id");
                });

            migrationBuilder.CreateTable(
                name: "PaymentProofs",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    InvoicePaymentId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    FileUrl = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    FileName = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    ContentType = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    UploadedByUserId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    UploadedAt = table.Column<DateTime>(type: "datetime2", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "datetime2", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_PaymentProofs", x => x.Id);
                    table.ForeignKey(
                        name: "FK_PaymentProofs_InvoicePayments_InvoicePaymentId",
                        column: x => x.InvoicePaymentId,
                        principalTable: "InvoicePayments",
                        principalColumn: "Id");
                    table.ForeignKey(
                        name: "FK_PaymentProofs_users_UploadedByUserId",
                        column: x => x.UploadedByUserId,
                        principalTable: "users",
                        principalColumn: "Id");
                });

            migrationBuilder.InsertData(
                table: "permissions",
                columns: new[] { "Id", "Action", "Code", "CreatedAt", "Description", "IsSystem", "Module" },
                values: new object[,]
                {
                    { new Guid("550e8400-e29b-41d4-a716-446655440001"), "Create", "Sessions.Create", new DateTime(2026, 1, 1, 0, 0, 0, 0, DateTimeKind.Utc), "Open a new gaming or watching session", true, "Sessions" },
                    { new Guid("550e8400-e29b-41d4-a716-446655440002"), "Pause", "Sessions.Pause", new DateTime(2026, 1, 1, 0, 0, 0, 0, DateTimeKind.Utc), "Pause or resume a session", true, "Sessions" },
                    { new Guid("550e8400-e29b-41d4-a716-446655440003"), "Close", "Sessions.Close", new DateTime(2026, 1, 1, 0, 0, 0, 0, DateTimeKind.Utc), "Close a session and generate invoice", true, "Sessions" },
                    { new Guid("550e8400-e29b-41d4-a716-446655440004"), "View", "Sessions.View", new DateTime(2026, 1, 1, 0, 0, 0, 0, DateTimeKind.Utc), "View sessions and live dashboard", true, "Sessions" },
                    { new Guid("550e8400-e29b-41d4-a716-446655440005"), "Sell", "Cafeteria.Sell", new DateTime(2026, 1, 1, 0, 0, 0, 0, DateTimeKind.Utc), "Record cafeteria sales", true, "Cafeteria" },
                    { new Guid("550e8400-e29b-41d4-a716-446655440006"), "Return", "Cafeteria.Return", new DateTime(2026, 1, 1, 0, 0, 0, 0, DateTimeKind.Utc), "Process cafeteria returns", true, "Cafeteria" },
                    { new Guid("550e8400-e29b-41d4-a716-446655440007"), "View", "Cafeteria.View", new DateTime(2026, 1, 1, 0, 0, 0, 0, DateTimeKind.Utc), "View cafeteria items and sales", true, "Cafeteria" },
                    { new Guid("550e8400-e29b-41d4-a716-446655440008"), "View", "Inventory.View", new DateTime(2026, 1, 1, 0, 0, 0, 0, DateTimeKind.Utc), "View inventory levels", true, "Inventory" },
                    { new Guid("550e8400-e29b-41d4-a716-446655440009"), "Adjust", "Inventory.Adjust", new DateTime(2026, 1, 1, 0, 0, 0, 0, DateTimeKind.Utc), "Manually adjust inventory", true, "Inventory" },
                    { new Guid("550e8400-e29b-41d4-a716-446655440010"), "Create", "PurchaseOrders.Create", new DateTime(2026, 1, 1, 0, 0, 0, 0, DateTimeKind.Utc), "Create purchase orders", true, "PurchaseOrders" },
                    { new Guid("550e8400-e29b-41d4-a716-446655440011"), "Receive", "PurchaseOrders.Receive", new DateTime(2026, 1, 1, 0, 0, 0, 0, DateTimeKind.Utc), "Receive purchase orders", true, "PurchaseOrders" },
                    { new Guid("550e8400-e29b-41d4-a716-446655440012"), "Add", "Expenses.Add", new DateTime(2026, 1, 1, 0, 0, 0, 0, DateTimeKind.Utc), "Record expenses", true, "Expenses" },
                    { new Guid("550e8400-e29b-41d4-a716-446655440013"), "View", "Expenses.View", new DateTime(2026, 1, 1, 0, 0, 0, 0, DateTimeKind.Utc), "View expenses", true, "Expenses" },
                    { new Guid("550e8400-e29b-41d4-a716-446655440014"), "View", "Reports.View", new DateTime(2026, 1, 1, 0, 0, 0, 0, DateTimeKind.Utc), "View financial and usage reports", true, "Reports" },
                    { new Guid("550e8400-e29b-41d4-a716-446655440015"), "Manage", "Assets.Manage", new DateTime(2026, 1, 1, 0, 0, 0, 0, DateTimeKind.Utc), "Manage rooms, devices, and controllers", true, "Assets" },
                    { new Guid("550e8400-e29b-41d4-a716-446655440016"), "Manage", "Settings.Manage", new DateTime(2026, 1, 1, 0, 0, 0, 0, DateTimeKind.Utc), "Manage pricing plans and tenant settings", true, "Settings" },
                    { new Guid("550e8400-e29b-41d4-a716-446655440017"), "EditClosed", "CanEditClosedRecords", new DateTime(2026, 1, 1, 0, 0, 0, 0, DateTimeKind.Utc), "Edit or delete closed financial records", true, "Security" },
                    { new Guid("550e8400-e29b-41d4-a716-446655440018"), "Manage", "Users.Manage", new DateTime(2026, 1, 1, 0, 0, 0, 0, DateTimeKind.Utc), "Create and manage sub-users", true, "Users" }
                });

            migrationBuilder.CreateIndex(
                name: "IX_audit_logs_BranchId",
                table: "audit_logs",
                column: "BranchId");

            migrationBuilder.CreateIndex(
                name: "IX_audit_logs_TenantId_BranchId_Timestamp",
                table: "audit_logs",
                columns: new[] { "TenantId", "BranchId", "Timestamp" });

            migrationBuilder.CreateIndex(
                name: "IX_audit_logs_UserId",
                table: "audit_logs",
                column: "UserId");

            migrationBuilder.CreateIndex(
                name: "IX_branches_TenantId_IsActive",
                table: "branches",
                columns: new[] { "TenantId", "IsActive" });

            migrationBuilder.CreateIndex(
                name: "IX_cafeteria_items_BranchId",
                table: "cafeteria_items",
                column: "BranchId");

            migrationBuilder.CreateIndex(
                name: "IX_cafeteria_items_TenantId_BranchId_IsActive",
                table: "cafeteria_items",
                columns: new[] { "TenantId", "BranchId", "IsActive" });

            migrationBuilder.CreateIndex(
                name: "IX_CafeteriaReturns_ReturnedByUserId",
                table: "CafeteriaReturns",
                column: "ReturnedByUserId");

            migrationBuilder.CreateIndex(
                name: "IX_CafeteriaReturns_SaleId",
                table: "CafeteriaReturns",
                column: "SaleId");

            migrationBuilder.CreateIndex(
                name: "IX_CafeteriaReturns_SaleLineId",
                table: "CafeteriaReturns",
                column: "SaleLineId");

            migrationBuilder.CreateIndex(
                name: "IX_CafeteriaSaleLines_CafeteriaItemId",
                table: "CafeteriaSaleLines",
                column: "CafeteriaItemId");

            migrationBuilder.CreateIndex(
                name: "IX_CafeteriaSaleLines_SaleId",
                table: "CafeteriaSaleLines",
                column: "SaleId");

            migrationBuilder.CreateIndex(
                name: "IX_CafeteriaSales_BranchId",
                table: "CafeteriaSales",
                column: "BranchId");

            migrationBuilder.CreateIndex(
                name: "IX_CafeteriaSales_SessionId",
                table: "CafeteriaSales",
                column: "SessionId");

            migrationBuilder.CreateIndex(
                name: "IX_CafeteriaSales_SoldByUserId",
                table: "CafeteriaSales",
                column: "SoldByUserId");

            migrationBuilder.CreateIndex(
                name: "IX_ControllerTypes_TenantId",
                table: "ControllerTypes",
                column: "TenantId");

            migrationBuilder.CreateIndex(
                name: "IX_device_pricing_plans_PricingPlanId",
                table: "device_pricing_plans",
                column: "PricingPlanId");

            migrationBuilder.CreateIndex(
                name: "IX_DeviceControllers_ControllerTypeId",
                table: "DeviceControllers",
                column: "ControllerTypeId");

            migrationBuilder.CreateIndex(
                name: "IX_DeviceControllers_DeviceId",
                table: "DeviceControllers",
                column: "DeviceId");

            migrationBuilder.CreateIndex(
                name: "IX_Devices_BranchId",
                table: "Devices",
                column: "BranchId");

            migrationBuilder.CreateIndex(
                name: "IX_Devices_RoomId",
                table: "Devices",
                column: "RoomId");

            migrationBuilder.CreateIndex(
                name: "IX_ExpenseCategories_TenantId",
                table: "ExpenseCategories",
                column: "TenantId");

            migrationBuilder.CreateIndex(
                name: "IX_expenses_BranchId",
                table: "expenses",
                column: "BranchId");

            migrationBuilder.CreateIndex(
                name: "IX_expenses_CategoryId",
                table: "expenses",
                column: "CategoryId");

            migrationBuilder.CreateIndex(
                name: "IX_expenses_PurchaseOrderId",
                table: "expenses",
                column: "PurchaseOrderId",
                unique: true,
                filter: "[PurchaseOrderId] IS NOT NULL");

            migrationBuilder.CreateIndex(
                name: "IX_expenses_RecordedByUserId",
                table: "expenses",
                column: "RecordedByUserId");

            migrationBuilder.CreateIndex(
                name: "IX_GamingRates_PricingPlanId_ControllerCount",
                table: "GamingRates",
                columns: new[] { "PricingPlanId", "ControllerCount" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_InventoryMovements_BranchId",
                table: "InventoryMovements",
                column: "BranchId");

            migrationBuilder.CreateIndex(
                name: "IX_InventoryMovements_CafeteriaItemId",
                table: "InventoryMovements",
                column: "CafeteriaItemId");

            migrationBuilder.CreateIndex(
                name: "IX_InventoryMovements_PerformedByUserId",
                table: "InventoryMovements",
                column: "PerformedByUserId");

            migrationBuilder.CreateIndex(
                name: "IX_InvoicePayments_CollectedByUserId",
                table: "InvoicePayments",
                column: "CollectedByUserId");

            migrationBuilder.CreateIndex(
                name: "IX_InvoicePayments_InvoiceId",
                table: "InvoicePayments",
                column: "InvoiceId");

            migrationBuilder.CreateIndex(
                name: "IX_invoices_BranchId_InvoiceNumber",
                table: "invoices",
                columns: new[] { "BranchId", "InvoiceNumber" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_invoices_CafeteriaSaleId",
                table: "invoices",
                column: "CafeteriaSaleId",
                unique: true,
                filter: "[CafeteriaSaleId] IS NOT NULL");

            migrationBuilder.CreateIndex(
                name: "IX_invoices_ClosedByUserId",
                table: "invoices",
                column: "ClosedByUserId");

            migrationBuilder.CreateIndex(
                name: "IX_invoices_SessionId",
                table: "invoices",
                column: "SessionId",
                unique: true,
                filter: "[SessionId] IS NOT NULL");

            migrationBuilder.CreateIndex(
                name: "IX_invoices_TenantId_BranchId_ClosedAt",
                table: "invoices",
                columns: new[] { "TenantId", "BranchId", "ClosedAt" });

            migrationBuilder.CreateIndex(
                name: "IX_Notifications_TenantId",
                table: "Notifications",
                column: "TenantId");

            migrationBuilder.CreateIndex(
                name: "IX_Notifications_UserId",
                table: "Notifications",
                column: "UserId");

            migrationBuilder.CreateIndex(
                name: "IX_PaymentProofs_InvoicePaymentId",
                table: "PaymentProofs",
                column: "InvoicePaymentId",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_PaymentProofs_UploadedByUserId",
                table: "PaymentProofs",
                column: "UploadedByUserId");

            migrationBuilder.CreateIndex(
                name: "IX_permissions_Code",
                table: "permissions",
                column: "Code",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_PricingPlans_BranchId",
                table: "PricingPlans",
                column: "BranchId");

            migrationBuilder.CreateIndex(
                name: "IX_PricingPlans_TenantId",
                table: "PricingPlans",
                column: "TenantId");

            migrationBuilder.CreateIndex(
                name: "IX_PurchaseOrderLines_CafeteriaItemId",
                table: "PurchaseOrderLines",
                column: "CafeteriaItemId");

            migrationBuilder.CreateIndex(
                name: "IX_PurchaseOrderLines_PurchaseOrderId",
                table: "PurchaseOrderLines",
                column: "PurchaseOrderId");

            migrationBuilder.CreateIndex(
                name: "IX_PurchaseOrders_BranchId",
                table: "PurchaseOrders",
                column: "BranchId");

            migrationBuilder.CreateIndex(
                name: "IX_PurchaseOrders_CreatedByUserId",
                table: "PurchaseOrders",
                column: "CreatedByUserId");

            migrationBuilder.CreateIndex(
                name: "IX_PurchaseOrders_ReceivedByUserId",
                table: "PurchaseOrders",
                column: "ReceivedByUserId");

            migrationBuilder.CreateIndex(
                name: "IX_refresh_tokens_UserId",
                table: "refresh_tokens",
                column: "UserId");

            migrationBuilder.CreateIndex(
                name: "IX_revenue_entries_BranchId",
                table: "revenue_entries",
                column: "BranchId");

            migrationBuilder.CreateIndex(
                name: "IX_revenue_entries_InvoiceId",
                table: "revenue_entries",
                column: "InvoiceId",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_Rooms_BranchId",
                table: "Rooms",
                column: "BranchId");

            migrationBuilder.CreateIndex(
                name: "IX_Screens_DeviceId",
                table: "Screens",
                column: "DeviceId");

            migrationBuilder.CreateIndex(
                name: "IX_SessionCafeteriaLines_AddedByUserId",
                table: "SessionCafeteriaLines",
                column: "AddedByUserId");

            migrationBuilder.CreateIndex(
                name: "IX_SessionCafeteriaLines_CafeteriaItemId",
                table: "SessionCafeteriaLines",
                column: "CafeteriaItemId");

            migrationBuilder.CreateIndex(
                name: "IX_SessionCafeteriaLines_SessionId",
                table: "SessionCafeteriaLines",
                column: "SessionId");

            migrationBuilder.CreateIndex(
                name: "IX_SessionPauses_PausedByUserId",
                table: "SessionPauses",
                column: "PausedByUserId");

            migrationBuilder.CreateIndex(
                name: "IX_SessionPauses_SessionId",
                table: "SessionPauses",
                column: "SessionId");

            migrationBuilder.CreateIndex(
                name: "IX_sessions_BranchId",
                table: "sessions",
                column: "BranchId");

            migrationBuilder.CreateIndex(
                name: "IX_sessions_ClosedByUserId",
                table: "sessions",
                column: "ClosedByUserId");

            migrationBuilder.CreateIndex(
                name: "IX_sessions_DeviceId_Status",
                table: "sessions",
                columns: new[] { "DeviceId", "Status" });

            migrationBuilder.CreateIndex(
                name: "IX_sessions_OpenedByUserId",
                table: "sessions",
                column: "OpenedByUserId");

            migrationBuilder.CreateIndex(
                name: "IX_sessions_PricingPlanId",
                table: "sessions",
                column: "PricingPlanId");

            migrationBuilder.CreateIndex(
                name: "IX_sessions_RoomId",
                table: "sessions",
                column: "RoomId");

            migrationBuilder.CreateIndex(
                name: "IX_sessions_TenantId_BranchId_Status",
                table: "sessions",
                columns: new[] { "TenantId", "BranchId", "Status" });

            migrationBuilder.CreateIndex(
                name: "IX_tenants_Slug",
                table: "tenants",
                column: "Slug",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_user_branches_BranchId",
                table: "user_branches",
                column: "BranchId");

            migrationBuilder.CreateIndex(
                name: "IX_user_permissions_PermissionId",
                table: "user_permissions",
                column: "PermissionId");

            migrationBuilder.CreateIndex(
                name: "IX_users_TenantId_Email",
                table: "users",
                columns: new[] { "TenantId", "Email" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_WatchingRates_PricingPlanId",
                table: "WatchingRates",
                column: "PricingPlanId");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "audit_logs");

            migrationBuilder.DropTable(
                name: "CafeteriaReturns");

            migrationBuilder.DropTable(
                name: "device_pricing_plans");

            migrationBuilder.DropTable(
                name: "DeviceControllers");

            migrationBuilder.DropTable(
                name: "expenses");

            migrationBuilder.DropTable(
                name: "GamingRates");

            migrationBuilder.DropTable(
                name: "InventoryMovements");

            migrationBuilder.DropTable(
                name: "Notifications");

            migrationBuilder.DropTable(
                name: "PaymentProofs");

            migrationBuilder.DropTable(
                name: "PurchaseOrderLines");

            migrationBuilder.DropTable(
                name: "refresh_tokens");

            migrationBuilder.DropTable(
                name: "revenue_entries");

            migrationBuilder.DropTable(
                name: "Screens");

            migrationBuilder.DropTable(
                name: "SessionCafeteriaLines");

            migrationBuilder.DropTable(
                name: "SessionPauses");

            migrationBuilder.DropTable(
                name: "user_branches");

            migrationBuilder.DropTable(
                name: "user_permissions");

            migrationBuilder.DropTable(
                name: "WatchingRates");

            migrationBuilder.DropTable(
                name: "CafeteriaSaleLines");

            migrationBuilder.DropTable(
                name: "ControllerTypes");

            migrationBuilder.DropTable(
                name: "ExpenseCategories");

            migrationBuilder.DropTable(
                name: "InvoicePayments");

            migrationBuilder.DropTable(
                name: "PurchaseOrders");

            migrationBuilder.DropTable(
                name: "permissions");

            migrationBuilder.DropTable(
                name: "cafeteria_items");

            migrationBuilder.DropTable(
                name: "invoices");

            migrationBuilder.DropTable(
                name: "CafeteriaSales");

            migrationBuilder.DropTable(
                name: "sessions");

            migrationBuilder.DropTable(
                name: "Devices");

            migrationBuilder.DropTable(
                name: "PricingPlans");

            migrationBuilder.DropTable(
                name: "users");

            migrationBuilder.DropTable(
                name: "Rooms");

            migrationBuilder.DropTable(
                name: "branches");

            migrationBuilder.DropTable(
                name: "tenants");
        }
    }
}
