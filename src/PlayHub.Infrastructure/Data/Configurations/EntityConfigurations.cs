using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using PlayHub.Domain.Entities;

namespace PlayHub.Infrastructure.Data.Configurations;

public class TenantConfiguration : IEntityTypeConfiguration<Tenant>
{
    public void Configure(EntityTypeBuilder<Tenant> builder)
    {
        builder.ToTable("tenants");
        builder.HasKey(x => x.Id);
        builder.Property(x => x.Name).HasMaxLength(200).IsRequired();
        builder.Property(x => x.Slug).HasMaxLength(100).IsRequired();
        builder.HasIndex(x => x.Slug).IsUnique();
        builder.Property(x => x.DefaultLanguage).HasMaxLength(5);
        builder.Property(x => x.DefaultCurrency).HasMaxLength(3);
        builder.Property(x => x.Timezone).HasMaxLength(50);
        builder.Property(x => x.WhatsAppApiBaseUrl).HasMaxLength(300);
        builder.Property(x => x.WhatsAppSessionId).HasMaxLength(200);
        builder.Property(x => x.WhatsAppConnectedPhone).HasMaxLength(30);
    }
}

public class CustomerConfiguration : IEntityTypeConfiguration<Customer>
{
    public void Configure(EntityTypeBuilder<Customer> builder)
    {
        builder.ToTable("customers");
        builder.HasKey(x => x.Id);
        builder.Property(x => x.Code).HasMaxLength(20).IsRequired();
        builder.Property(x => x.Name).HasMaxLength(200).IsRequired();
        builder.Property(x => x.Phone).HasMaxLength(30).IsRequired();
        builder.Property(x => x.Notes).HasMaxLength(1000);
        builder.Property(x => x.WalletBalance).HasPrecision(18, 2);
        builder.HasIndex(x => new { x.TenantId, x.Code }).IsUnique();
        builder.HasIndex(x => new { x.TenantId, x.Phone }).IsUnique();
        builder.HasIndex(x => new { x.TenantId, x.Name });
        builder.HasOne(x => x.Tenant).WithMany(x => x.Customers).HasForeignKey(x => x.TenantId);
    }
}

public class CashCollectionConfiguration : IEntityTypeConfiguration<CashCollection>
{
    public void Configure(EntityTypeBuilder<CashCollection> builder)
    {
        builder.ToTable("cash_collections");
        builder.HasKey(x => x.Id);
        builder.Property(x => x.Amount).HasPrecision(18, 2);
        builder.Property(x => x.Note).HasMaxLength(500);
        builder.HasIndex(x => new { x.TenantId, x.BranchId, x.CollectedAt });
        builder.HasOne(x => x.Branch).WithMany().HasForeignKey(x => x.BranchId);
        builder.HasOne(x => x.CollectedByUser).WithMany().HasForeignKey(x => x.CollectedByUserId);
    }
}

public class WalletTransactionConfiguration : IEntityTypeConfiguration<WalletTransaction>
{
    public void Configure(EntityTypeBuilder<WalletTransaction> builder)
    {
        builder.ToTable("wallet_transactions");
        builder.HasKey(x => x.Id);
        builder.Property(x => x.Amount).HasPrecision(18, 2);
        builder.Property(x => x.BalanceAfter).HasPrecision(18, 2);
        builder.Property(x => x.Note).HasMaxLength(500);
        builder.HasIndex(x => new { x.TenantId, x.CustomerId, x.CreatedAt });
        builder.HasOne(x => x.Customer).WithMany(x => x.WalletTransactions).HasForeignKey(x => x.CustomerId);
        builder.HasOne(x => x.Invoice).WithMany().HasForeignKey(x => x.InvoiceId);
        builder.HasOne(x => x.CreatedByUser).WithMany().HasForeignKey(x => x.CreatedByUserId);
    }
}

public class CustomerOfferConfiguration : IEntityTypeConfiguration<CustomerOffer>
{
    public void Configure(EntityTypeBuilder<CustomerOffer> builder)
    {
        builder.ToTable("customer_offers");
        builder.HasKey(x => x.Id);
        builder.Property(x => x.Title).HasMaxLength(200).IsRequired();
        builder.Property(x => x.Message).HasMaxLength(2000).IsRequired();
        builder.HasIndex(x => new { x.TenantId, x.IsActive });
        builder.HasOne(x => x.Tenant).WithMany(x => x.CustomerOffers).HasForeignKey(x => x.TenantId);
    }
}

public class LoyaltyOfferConfiguration : IEntityTypeConfiguration<LoyaltyOffer>
{
    public void Configure(EntityTypeBuilder<LoyaltyOffer> builder)
    {
        builder.ToTable("loyalty_offers");
        builder.HasKey(x => x.Id);
        builder.Property(x => x.Title).HasMaxLength(200).IsRequired();
        builder.Property(x => x.Description).HasMaxLength(2000);
        builder.HasIndex(x => new { x.TenantId, x.IsActive });
        builder.HasOne(x => x.Tenant).WithMany(x => x.LoyaltyOffers).HasForeignKey(x => x.TenantId);
        builder.HasOne(x => x.OwnerUser).WithMany().HasForeignKey(x => x.OwnerUserId).OnDelete(DeleteBehavior.NoAction);
        builder.HasOne(x => x.Branch).WithMany().HasForeignKey(x => x.BranchId).OnDelete(DeleteBehavior.NoAction);
    }
}

public class LoyaltyOfferConditionConfiguration : IEntityTypeConfiguration<LoyaltyOfferCondition>
{
    public void Configure(EntityTypeBuilder<LoyaltyOfferCondition> builder)
    {
        builder.ToTable("loyalty_offer_conditions");
        builder.HasKey(x => x.Id);
        builder.Property(x => x.RequiredQuantity).HasPrecision(18, 2);
        builder.HasOne(x => x.Offer).WithMany(x => x.Conditions).HasForeignKey(x => x.OfferId).OnDelete(DeleteBehavior.Cascade);
        builder.HasOne(x => x.CafeteriaItem).WithMany().HasForeignKey(x => x.CafeteriaItemId).OnDelete(DeleteBehavior.NoAction);
        builder.HasOne(x => x.Variant).WithMany().HasForeignKey(x => x.VariantId).OnDelete(DeleteBehavior.NoAction);
    }
}

public class LoyaltyOfferRewardConfiguration : IEntityTypeConfiguration<LoyaltyOfferReward>
{
    public void Configure(EntityTypeBuilder<LoyaltyOfferReward> builder)
    {
        builder.ToTable("loyalty_offer_rewards");
        builder.HasKey(x => x.Id);
        builder.Property(x => x.Quantity).HasPrecision(18, 2);
        builder.HasOne(x => x.Offer).WithMany(x => x.Rewards).HasForeignKey(x => x.OfferId).OnDelete(DeleteBehavior.Cascade);
        builder.HasOne(x => x.CafeteriaItem).WithMany().HasForeignKey(x => x.CafeteriaItemId).OnDelete(DeleteBehavior.NoAction);
        builder.HasOne(x => x.Variant).WithMany().HasForeignKey(x => x.VariantId).OnDelete(DeleteBehavior.NoAction);
    }
}

public class LoyaltyOfferDeviceConfiguration : IEntityTypeConfiguration<LoyaltyOfferDevice>
{
    public void Configure(EntityTypeBuilder<LoyaltyOfferDevice> builder)
    {
        builder.ToTable("loyalty_offer_devices");
        builder.HasKey(x => new { x.OfferId, x.DeviceId });
        builder.HasOne(x => x.Offer).WithMany(x => x.Devices).HasForeignKey(x => x.OfferId).OnDelete(DeleteBehavior.Cascade);
        builder.HasOne(x => x.Device).WithMany().HasForeignKey(x => x.DeviceId).OnDelete(DeleteBehavior.Cascade);
    }
}

public class LoyaltyCreditConfiguration : IEntityTypeConfiguration<LoyaltyCredit>
{
    public void Configure(EntityTypeBuilder<LoyaltyCredit> builder)
    {
        builder.ToTable("loyalty_credits");
        builder.HasKey(x => x.Id);
        builder.Property(x => x.QuantityOriginal).HasPrecision(18, 2);
        builder.Property(x => x.QuantityRemaining).HasPrecision(18, 2);
        builder.HasIndex(x => new { x.CustomerId, x.Status });
        builder.HasIndex(x => new { x.SourceSessionId, x.OfferId });
        builder.HasOne(x => x.Tenant).WithMany().HasForeignKey(x => x.TenantId);
        builder.HasOne(x => x.Customer).WithMany(x => x.LoyaltyCredits).HasForeignKey(x => x.CustomerId);
        builder.HasOne(x => x.Offer).WithMany().HasForeignKey(x => x.OfferId).OnDelete(DeleteBehavior.NoAction);
        builder.HasOne(x => x.SourceSession).WithMany().HasForeignKey(x => x.SourceSessionId).OnDelete(DeleteBehavior.NoAction);
        builder.HasOne(x => x.CafeteriaItem).WithMany().HasForeignKey(x => x.CafeteriaItemId).OnDelete(DeleteBehavior.NoAction);
        builder.HasOne(x => x.Variant).WithMany().HasForeignKey(x => x.VariantId).OnDelete(DeleteBehavior.NoAction);
    }
}

public class BranchConfiguration : IEntityTypeConfiguration<Branch>
{
    public void Configure(EntityTypeBuilder<Branch> builder)
    {
        builder.ToTable("branches");
        builder.HasKey(x => x.Id);
        builder.Property(x => x.Name).HasMaxLength(200).IsRequired();
        builder.Property(x => x.InvoicePrefix).HasMaxLength(20);
        builder.HasIndex(x => new { x.TenantId, x.IsActive });
        builder.HasOne(x => x.Tenant).WithMany(x => x.Branches).HasForeignKey(x => x.TenantId);
        builder.HasOne(x => x.OwnerUser).WithMany().HasForeignKey(x => x.OwnerUserId).OnDelete(DeleteBehavior.NoAction);
    }
}

public class BranchPaymentAccountConfiguration : IEntityTypeConfiguration<BranchPaymentAccount>
{
    public void Configure(EntityTypeBuilder<BranchPaymentAccount> builder)
    {
        builder.ToTable("branch_payment_accounts");
        builder.HasKey(x => x.Id);
        builder.Property(x => x.Label).HasMaxLength(100);
        builder.Property(x => x.AccountNumber).HasMaxLength(100).IsRequired();
        builder.HasIndex(x => new { x.BranchId, x.AccountType, x.SortOrder });
        builder.HasOne(x => x.Branch).WithMany(x => x.PaymentAccounts).HasForeignKey(x => x.BranchId);
    }
}

public class StockVoucherConfiguration : IEntityTypeConfiguration<StockVoucher>
{
    public void Configure(EntityTypeBuilder<StockVoucher> builder)
    {
        builder.ToTable("stock_vouchers");
        builder.HasKey(x => x.Id);
        builder.Property(x => x.VoucherNumber).HasMaxLength(30).IsRequired();
        builder.Property(x => x.Notes).HasMaxLength(500);
        builder.HasIndex(x => new { x.BranchId, x.VoucherNumber }).IsUnique();
        builder.HasIndex(x => new { x.TenantId, x.BranchId, x.VoucherType, x.Status });
        builder.HasOne(x => x.RelatedCountVoucher).WithMany().HasForeignKey(x => x.RelatedCountVoucherId);
        builder.HasOne(x => x.CreatedByUser).WithMany().HasForeignKey(x => x.CreatedByUserId);
        builder.HasOne(x => x.PostedByUser).WithMany().HasForeignKey(x => x.PostedByUserId);
        builder.HasOne(x => x.Branch).WithMany().HasForeignKey(x => x.BranchId);
    }
}

public class StockVoucherLineConfiguration : IEntityTypeConfiguration<StockVoucherLine>
{
    public void Configure(EntityTypeBuilder<StockVoucherLine> builder)
    {
        builder.ToTable("stock_voucher_lines");
        builder.HasKey(x => x.Id);
        builder.Property(x => x.Notes).HasMaxLength(300);
        builder.HasOne(x => x.StockVoucher).WithMany(x => x.Lines).HasForeignKey(x => x.StockVoucherId);
        builder.HasOne(x => x.CafeteriaItem).WithMany().HasForeignKey(x => x.CafeteriaItemId);
    }
}

public class SessionCafeteriaReturnConfiguration : IEntityTypeConfiguration<SessionCafeteriaReturn>
{
    public void Configure(EntityTypeBuilder<SessionCafeteriaReturn> builder)
    {
        builder.ToTable("session_cafeteria_returns");
        builder.HasKey(x => x.Id);
        builder.Property(x => x.Reason).HasMaxLength(500).IsRequired();
        builder.HasOne(x => x.Session).WithMany().HasForeignKey(x => x.SessionId);
        builder.HasOne(x => x.SessionCafeteriaLine).WithMany(x => x.Returns).HasForeignKey(x => x.SessionCafeteriaLineId);
        builder.HasOne(x => x.ReturnedByUser).WithMany().HasForeignKey(x => x.ReturnedByUserId);
    }
}

public class UserConfiguration : IEntityTypeConfiguration<User>
{
    public void Configure(EntityTypeBuilder<User> builder)
    {
        builder.ToTable("users");
        builder.HasKey(x => x.Id);
        builder.Property(x => x.Email).HasMaxLength(256).IsRequired();
        builder.Property(x => x.PasswordHash).HasMaxLength(500).IsRequired();
        builder.Property(x => x.FirstName).HasMaxLength(100).IsRequired();
        builder.Property(x => x.LastName).HasMaxLength(100).IsRequired();
        builder.HasIndex(x => new { x.TenantId, x.Email }).IsUnique();
        builder.HasIndex(x => x.ParentUserId);
        builder.HasOne(x => x.Tenant).WithMany(x => x.Users).HasForeignKey(x => x.TenantId);
        builder.HasOne(x => x.ParentUser).WithMany(x => x.ChildUsers).HasForeignKey(x => x.ParentUserId);
    }
}

public class PermissionConfiguration : IEntityTypeConfiguration<Permission>
{
    public void Configure(EntityTypeBuilder<Permission> builder)
    {
        builder.ToTable("permissions");
        builder.HasKey(x => x.Id);
        builder.Property(x => x.Code).HasMaxLength(100).IsRequired();
        builder.HasIndex(x => x.Code).IsUnique();
        builder.Property(x => x.Module).HasMaxLength(50).IsRequired();
        builder.Property(x => x.Action).HasMaxLength(50).IsRequired();
    }
}

public class UserPermissionConfiguration : IEntityTypeConfiguration<UserPermission>
{
    public void Configure(EntityTypeBuilder<UserPermission> builder)
    {
        builder.ToTable("user_permissions");
        builder.HasKey(x => new { x.UserId, x.PermissionId });
        builder.HasOne(x => x.User).WithMany(x => x.UserPermissions).HasForeignKey(x => x.UserId);
        builder.HasOne(x => x.Permission).WithMany(x => x.UserPermissions).HasForeignKey(x => x.PermissionId);
    }
}

public class UserBranchConfiguration : IEntityTypeConfiguration<UserBranch>
{
    public void Configure(EntityTypeBuilder<UserBranch> builder)
    {
        builder.ToTable("user_branches");
        builder.HasKey(x => new { x.UserId, x.BranchId });
        builder.HasOne(x => x.User).WithMany(x => x.UserBranches).HasForeignKey(x => x.UserId);
        builder.HasOne(x => x.Branch).WithMany(x => x.UserBranches).HasForeignKey(x => x.BranchId);
    }
}

public class RefreshTokenConfiguration : IEntityTypeConfiguration<RefreshToken>
{
    public void Configure(EntityTypeBuilder<RefreshToken> builder)
    {
        builder.ToTable("refresh_tokens");
        builder.HasKey(x => x.Id);
        builder.Property(x => x.TokenHash).HasMaxLength(500).IsRequired();
        builder.HasIndex(x => x.UserId);
        builder.HasOne(x => x.User).WithMany(x => x.RefreshTokens).HasForeignKey(x => x.UserId);
    }
}

public class SessionConfiguration : IEntityTypeConfiguration<Session>
{
    public void Configure(EntityTypeBuilder<Session> builder)
    {
        builder.ToTable("sessions");
        builder.HasKey(x => x.Id);
        builder.Property(x => x.RateSnapshot).HasColumnType("nvarchar(max)");
        builder.Property(x => x.BillingSegmentsJson).HasColumnType("nvarchar(max)");
        builder.Property(x => x.TimeCost).HasPrecision(18, 2);
        builder.Property(x => x.AccruedTimeCost).HasPrecision(18, 2);
        builder.Property(x => x.RoomSurchargePerHour).HasPrecision(18, 2);
        builder.Property(x => x.RoomSurchargeCost).HasPrecision(18, 2);
        builder.Property(x => x.CafeteriaCost).HasPrecision(18, 2);
        builder.Property(x => x.DiscountAmount).HasPrecision(18, 2);
        builder.Property(x => x.DiscountReason).HasMaxLength(300);
        builder.Property(x => x.TotalCost).HasPrecision(18, 2);
        builder.Property(x => x.QuickGuestName).HasMaxLength(200);
        builder.HasIndex(x => new { x.TenantId, x.BranchId, x.Status });
        builder.HasIndex(x => new { x.DeviceId, x.Status });
        builder.HasIndex(x => x.CustomerId);
        builder.HasOne(x => x.Invoice).WithOne(x => x.Session).HasForeignKey<Invoice>(x => x.SessionId);
        builder.HasOne(x => x.Customer).WithMany(x => x.Sessions).HasForeignKey(x => x.CustomerId);
    }
}

public class InvoiceConfiguration : IEntityTypeConfiguration<Invoice>
{
    public void Configure(EntityTypeBuilder<Invoice> builder)
    {
        builder.ToTable("invoices");
        builder.HasKey(x => x.Id);
        builder.Property(x => x.InvoiceNumber).HasMaxLength(30).IsRequired();
        builder.Property(x => x.Subtotal).HasPrecision(18, 2);
        builder.Property(x => x.DiscountAmount).HasPrecision(18, 2);
        builder.Property(x => x.DiscountReason).HasMaxLength(300);
        builder.Property(x => x.Total).HasPrecision(18, 2);
        builder.HasIndex(x => new { x.TenantId, x.BranchId, x.ClosedAt });
        builder.HasIndex(x => new { x.BranchId, x.InvoiceNumber }).IsUnique();
        builder.HasOne(x => x.RevenueEntry).WithOne(x => x.Invoice).HasForeignKey<RevenueEntry>(x => x.InvoiceId);
        builder.HasOne(x => x.CafeteriaSale).WithOne(x => x.Invoice).HasForeignKey<Invoice>(x => x.CafeteriaSaleId);
    }
}

public class AuditLogConfiguration : IEntityTypeConfiguration<AuditLog>
{
    public void Configure(EntityTypeBuilder<AuditLog> builder)
    {
        builder.ToTable("audit_logs");
        builder.HasKey(x => x.Id);
        builder.Property(x => x.ActionType).HasMaxLength(100).IsRequired();
        builder.Property(x => x.EntityType).HasMaxLength(100).IsRequired();
        builder.Property(x => x.UserName).HasMaxLength(200).IsRequired();
        builder.Property(x => x.Details).HasColumnType("nvarchar(max)");
        builder.HasIndex(x => new { x.TenantId, x.BranchId, x.Timestamp });
    }
}

public class DevicePricingPlanConfiguration : IEntityTypeConfiguration<DevicePricingPlan>
{
    public void Configure(EntityTypeBuilder<DevicePricingPlan> builder)
    {
        builder.ToTable("device_pricing_plans");
        builder.HasKey(x => new { x.DeviceId, x.PricingPlanId, x.SessionMode });
    }
}

public class GamingRateConfiguration : IEntityTypeConfiguration<GamingRate>
{
    public void Configure(EntityTypeBuilder<GamingRate> builder)
    {
        builder.Property(x => x.Rate).HasPrecision(18, 2);
        builder.HasIndex(x => new { x.PricingPlanId, x.ControllerCount }).IsUnique();
    }
}

public class WatchingRateConfiguration : IEntityTypeConfiguration<WatchingRate>
{
    public void Configure(EntityTypeBuilder<WatchingRate> builder)
    {
        builder.Property(x => x.RatePerPerson).HasPrecision(18, 2);
    }
}

public class CafeteriaItemConfiguration : IEntityTypeConfiguration<CafeteriaItem>
{
    public void Configure(EntityTypeBuilder<CafeteriaItem> builder)
    {
        builder.ToTable("cafeteria_items");
        builder.Property(x => x.SellPrice).HasPrecision(18, 2);
        builder.Property(x => x.BaseUnitName).HasMaxLength(100).IsRequired();
        builder.Property(x => x.LargeUnitName).HasMaxLength(100);
        builder.HasIndex(x => new { x.TenantId, x.BranchId, x.IsActive });
        builder.HasIndex(x => new { x.BranchId, x.Kind, x.IsActive });
    }
}

public class CafeteriaItemVariantConfiguration : IEntityTypeConfiguration<CafeteriaItemVariant>
{
    public void Configure(EntityTypeBuilder<CafeteriaItemVariant> builder)
    {
        builder.Property(x => x.Name).HasMaxLength(200).IsRequired();
        builder.Property(x => x.SellPrice).HasPrecision(18, 2);
        builder.HasMany(x => x.RecipeLines).WithOne(x => x.Variant).HasForeignKey(x => x.VariantId)
            .OnDelete(DeleteBehavior.Cascade);
    }
}

public class CafeteriaVariantRecipeLineConfiguration : IEntityTypeConfiguration<CafeteriaVariantRecipeLine>
{
    public void Configure(EntityTypeBuilder<CafeteriaVariantRecipeLine> builder)
    {
        builder.ToTable("cafeteria_variant_recipe_lines");
        builder.HasIndex(x => new { x.VariantId, x.WarehouseItemId });
        builder.HasOne(x => x.WarehouseItem).WithMany().HasForeignKey(x => x.WarehouseItemId)
            .OnDelete(DeleteBehavior.Restrict);
    }
}

public class CafeteriaAddOnConfiguration : IEntityTypeConfiguration<CafeteriaAddOn>
{
    public void Configure(EntityTypeBuilder<CafeteriaAddOn> builder)
    {
        builder.ToTable("cafeteria_add_ons");
        builder.Property(x => x.Name).HasMaxLength(200).IsRequired();
        builder.Property(x => x.SellPrice).HasPrecision(18, 2);
        builder.HasIndex(x => new { x.TenantId, x.BranchId, x.IsActive });
        builder.HasOne(x => x.WarehouseItem).WithMany().HasForeignKey(x => x.WarehouseItemId)
            .OnDelete(DeleteBehavior.Restrict);
    }
}

public class CafeteriaSaleLineAddOnConfiguration : IEntityTypeConfiguration<CafeteriaSaleLineAddOn>
{
    public void Configure(EntityTypeBuilder<CafeteriaSaleLineAddOn> builder)
    {
        builder.ToTable("cafeteria_sale_line_add_ons");
        builder.Property(x => x.Name).HasMaxLength(200).IsRequired();
        builder.Property(x => x.UnitPrice).HasPrecision(18, 2);
        builder.Property(x => x.LineTotal).HasPrecision(18, 2);
        builder.HasOne(x => x.AddOn).WithMany().HasForeignKey(x => x.AddOnId)
            .OnDelete(DeleteBehavior.Restrict);
    }
}

public class CafeteriaSaleLineIngredientDeductConfiguration : IEntityTypeConfiguration<CafeteriaSaleLineIngredientDeduct>
{
    public void Configure(EntityTypeBuilder<CafeteriaSaleLineIngredientDeduct> builder)
    {
        builder.ToTable("cafeteria_sale_line_ingredient_deducts");
        builder.HasOne(x => x.WarehouseItem).WithMany().HasForeignKey(x => x.WarehouseItemId)
            .OnDelete(DeleteBehavior.Restrict);
    }
}

public class SessionCafeteriaLineAddOnConfiguration : IEntityTypeConfiguration<SessionCafeteriaLineAddOn>
{
    public void Configure(EntityTypeBuilder<SessionCafeteriaLineAddOn> builder)
    {
        builder.ToTable("session_cafeteria_line_add_ons");
        builder.Property(x => x.Name).HasMaxLength(200).IsRequired();
        builder.Property(x => x.UnitPrice).HasPrecision(18, 2);
        builder.Property(x => x.LineTotal).HasPrecision(18, 2);
        builder.HasOne(x => x.AddOn).WithMany().HasForeignKey(x => x.AddOnId)
            .OnDelete(DeleteBehavior.Restrict);
    }
}

public class SessionCafeteriaLineIngredientDeductConfiguration : IEntityTypeConfiguration<SessionCafeteriaLineIngredientDeduct>
{
    public void Configure(EntityTypeBuilder<SessionCafeteriaLineIngredientDeduct> builder)
    {
        builder.ToTable("session_cafeteria_line_ingredient_deducts");
        builder.HasOne(x => x.WarehouseItem).WithMany().HasForeignKey(x => x.WarehouseItemId)
            .OnDelete(DeleteBehavior.Restrict);
    }
}

public class InventoryUnitConfiguration : IEntityTypeConfiguration<InventoryUnit>
{
    public void Configure(EntityTypeBuilder<InventoryUnit> builder)
    {
        builder.ToTable("inventory_units");
        builder.Property(x => x.Name).HasMaxLength(100).IsRequired();
        builder.Property(x => x.NameAr).HasMaxLength(100);
        builder.HasIndex(x => new { x.TenantId, x.Name }).IsUnique();
        builder.HasOne(x => x.Tenant).WithMany().HasForeignKey(x => x.TenantId);
    }
}

public class ItemUnitConversionLogConfiguration : IEntityTypeConfiguration<ItemUnitConversionLog>
{
    public void Configure(EntityTypeBuilder<ItemUnitConversionLog> builder)
    {
        builder.ToTable("item_unit_conversion_logs");
        builder.Property(x => x.OldBaseUnitName).HasMaxLength(100).IsRequired();
        builder.Property(x => x.NewBaseUnitName).HasMaxLength(100).IsRequired();
        builder.Property(x => x.OldLargeUnitName).HasMaxLength(100);
        builder.Property(x => x.NewLargeUnitName).HasMaxLength(100);
        builder.HasIndex(x => new { x.BranchId, x.CafeteriaItemId, x.CreatedAt });
        builder.HasOne(x => x.CafeteriaItem).WithMany().HasForeignKey(x => x.CafeteriaItemId);
        builder.HasOne(x => x.ChangedByUser).WithMany().HasForeignKey(x => x.ChangedByUserId);
        builder.HasOne(x => x.Branch).WithMany().HasForeignKey(x => x.BranchId);
    }
}

public class RevenueEntryConfiguration : IEntityTypeConfiguration<RevenueEntry>
{
    public void Configure(EntityTypeBuilder<RevenueEntry> builder)
    {
        builder.ToTable("revenue_entries");
        builder.Property(x => x.Amount).HasPrecision(18, 2);
        builder.HasIndex(x => x.InvoiceId).IsUnique();
    }
}

public class ExpenseConfiguration : IEntityTypeConfiguration<Expense>
{
    public void Configure(EntityTypeBuilder<Expense> builder)
    {
        builder.ToTable("expenses");
        builder.Property(x => x.Amount).HasPrecision(18, 2);
        builder.HasOne(x => x.PurchaseOrder).WithOne(x => x.Expense).HasForeignKey<Expense>(x => x.PurchaseOrderId);
    }
}

public class VenueAssetTypeConfiguration : IEntityTypeConfiguration<VenueAssetType>
{
    public void Configure(EntityTypeBuilder<VenueAssetType> builder)
    {
        builder.ToTable("venue_asset_types");
        builder.HasKey(x => x.Id);
        builder.Property(x => x.Name).HasMaxLength(100).IsRequired();
        builder.Property(x => x.Description).HasMaxLength(300);
        builder.HasIndex(x => new { x.TenantId, x.Name });
        builder.HasOne(x => x.Tenant).WithMany().HasForeignKey(x => x.TenantId);
    }
}

public class RoomAssetConfiguration : IEntityTypeConfiguration<RoomAsset>
{
    public void Configure(EntityTypeBuilder<RoomAsset> builder)
    {
        builder.ToTable("room_assets");
        builder.HasKey(x => x.Id);
        builder.Property(x => x.Notes).HasMaxLength(300);
        builder.HasIndex(x => new { x.RoomId, x.VenueAssetTypeId }).IsUnique();
        builder.HasOne(x => x.Room).WithMany(x => x.RoomAssets).HasForeignKey(x => x.RoomId);
        builder.HasOne(x => x.VenueAssetType).WithMany(x => x.RoomAssets).HasForeignKey(x => x.VenueAssetTypeId);
    }
}

public class MasterAlertSettingsConfiguration : IEntityTypeConfiguration<MasterAlertSettings>
{
    public void Configure(EntityTypeBuilder<MasterAlertSettings> builder)
    {
        builder.ToTable("master_alert_settings");
        builder.HasKey(x => x.Id);
        builder.Property(x => x.SmtpHost).HasMaxLength(200);
        builder.Property(x => x.SmtpUsername).HasMaxLength(256);
        builder.Property(x => x.SmtpPassword).HasMaxLength(500);
        builder.Property(x => x.SenderDisplayName).HasMaxLength(200);
        builder.Property(x => x.AlertRecipientEmail).HasMaxLength(256);
        builder.Property(x => x.OwnerWhatsAppPhone).HasMaxLength(30);
        builder.HasIndex(x => x.UserId).IsUnique();
        builder.HasOne(x => x.User).WithOne(x => x.AlertSettings).HasForeignKey<MasterAlertSettings>(x => x.UserId);
        builder.HasOne(x => x.Tenant).WithMany().HasForeignKey(x => x.TenantId);
    }
}

public class PlatformAlertSettingsConfiguration : IEntityTypeConfiguration<PlatformAlertSettings>
{
    public void Configure(EntityTypeBuilder<PlatformAlertSettings> builder)
    {
        builder.ToTable("platform_alert_settings");
        builder.HasKey(x => x.Id);
        builder.Property(x => x.SmtpUsername).HasMaxLength(256);
        builder.Property(x => x.SmtpPassword).HasMaxLength(500);
        builder.Property(x => x.SenderDisplayName).HasMaxLength(200);
        builder.Property(x => x.WhatsAppIntegrationApiBaseUrl).HasMaxLength(500);
        builder.Property(x => x.WhatsAppIntegrationApiKey).HasMaxLength(500);
        builder.HasIndex(x => x.TenantId).IsUnique();
        builder.HasOne(x => x.Tenant).WithMany().HasForeignKey(x => x.TenantId);
    }
}

public class DeviceMaintenanceConfiguration : IEntityTypeConfiguration<DeviceMaintenance>
{
    public void Configure(EntityTypeBuilder<DeviceMaintenance> builder)
    {
        builder.ToTable("device_maintenances");
        builder.HasKey(x => x.Id);
        builder.Property(x => x.Reason).HasMaxLength(500).IsRequired();
        builder.Property(x => x.Notes).HasMaxLength(1000);
        builder.HasIndex(x => new { x.DeviceId, x.CompletedAt });
        builder.HasIndex(x => new { x.TenantId, x.BranchId, x.StartedAt });
        builder.HasOne(x => x.Device).WithMany().HasForeignKey(x => x.DeviceId);
        builder.HasOne(x => x.Branch).WithMany().HasForeignKey(x => x.BranchId);
        builder.HasOne(x => x.ReportedByUser).WithMany().HasForeignKey(x => x.ReportedByUserId);
    }
}

public class InvoicePaymentConfiguration : IEntityTypeConfiguration<InvoicePayment>
{
    public void Configure(EntityTypeBuilder<InvoicePayment> builder)
    {
        builder.ToTable("invoice_payments");
        builder.HasKey(x => x.Id);
        builder.Property(x => x.Amount).HasPrecision(18, 2);
        builder.Property(x => x.DebtorName).HasMaxLength(200);
        builder.Property(x => x.DebtorPhone).HasMaxLength(30);
        builder.HasIndex(x => x.CustomerId);
        builder.HasOne(x => x.Invoice).WithMany(x => x.Payments).HasForeignKey(x => x.InvoiceId);
        builder.HasOne(x => x.Customer).WithMany().HasForeignKey(x => x.CustomerId);
        builder.HasOne(x => x.CollectedByUser).WithMany().HasForeignKey(x => x.CollectedByUserId);
    }
}

public class CafeteriaHoldConfiguration : IEntityTypeConfiguration<CafeteriaHold>
{
    public void Configure(EntityTypeBuilder<CafeteriaHold> builder)
    {
        builder.ToTable("cafeteria_holds");
        builder.HasKey(x => x.Id);
        builder.Property(x => x.GuestName).HasMaxLength(200);
        builder.Property(x => x.TotalAmount).HasPrecision(18, 2);
        builder.HasIndex(x => new { x.TenantId, x.BranchId, x.Status });
        builder.HasOne(x => x.Branch).WithMany().HasForeignKey(x => x.BranchId);
        builder.HasOne(x => x.Customer).WithMany().HasForeignKey(x => x.CustomerId);
        builder.HasOne(x => x.CreatedByUser).WithMany().HasForeignKey(x => x.CreatedByUserId);
        builder.HasOne(x => x.AttachedSession).WithMany().HasForeignKey(x => x.AttachedSessionId);
        builder.HasOne(x => x.ConvertedSale).WithMany().HasForeignKey(x => x.ConvertedSaleId);
    }
}

public class CafeteriaHoldLineConfiguration : IEntityTypeConfiguration<CafeteriaHoldLine>
{
    public void Configure(EntityTypeBuilder<CafeteriaHoldLine> builder)
    {
        builder.ToTable("cafeteria_hold_lines");
        builder.Property(x => x.VariantName).HasMaxLength(200);
        builder.Property(x => x.UnitPrice).HasPrecision(18, 2);
        builder.Property(x => x.LineTotal).HasPrecision(18, 2);
        builder.HasOne(x => x.Hold).WithMany(x => x.Lines).HasForeignKey(x => x.HoldId);
        builder.HasOne(x => x.CafeteriaItem).WithMany().HasForeignKey(x => x.CafeteriaItemId);
        builder.HasOne(x => x.Variant).WithMany().HasForeignKey(x => x.VariantId);
    }
}

public class CafeteriaHoldLineAddOnConfiguration : IEntityTypeConfiguration<CafeteriaHoldLineAddOn>
{
    public void Configure(EntityTypeBuilder<CafeteriaHoldLineAddOn> builder)
    {
        builder.ToTable("cafeteria_hold_line_add_ons");
        builder.Property(x => x.Name).HasMaxLength(200).IsRequired();
        builder.Property(x => x.UnitPrice).HasPrecision(18, 2);
        builder.Property(x => x.LineTotal).HasPrecision(18, 2);
        builder.HasOne(x => x.HoldLine).WithMany(x => x.AddOns).HasForeignKey(x => x.HoldLineId);
        builder.HasOne(x => x.AddOn).WithMany().HasForeignKey(x => x.AddOnId)
            .OnDelete(DeleteBehavior.Restrict);
    }
}

public class CafeteriaHoldLineIngredientDeductConfiguration : IEntityTypeConfiguration<CafeteriaHoldLineIngredientDeduct>
{
    public void Configure(EntityTypeBuilder<CafeteriaHoldLineIngredientDeduct> builder)
    {
        builder.ToTable("cafeteria_hold_line_ingredient_deducts");
        builder.HasOne(x => x.HoldLine).WithMany(x => x.IngredientDeducts).HasForeignKey(x => x.HoldLineId);
        builder.HasOne(x => x.WarehouseItem).WithMany().HasForeignKey(x => x.WarehouseItemId)
            .OnDelete(DeleteBehavior.Restrict);
    }
}

public class DeviceReservationConfiguration : IEntityTypeConfiguration<DeviceReservation>
{
    public void Configure(EntityTypeBuilder<DeviceReservation> builder)
    {
        builder.ToTable("device_reservations");
        builder.HasKey(x => x.Id);
        builder.Property(x => x.GuestName).HasMaxLength(200);
        builder.Property(x => x.Notes).HasMaxLength(1000);
        builder.HasIndex(x => new { x.DeviceId, x.Status, x.StartsAt });
        builder.HasIndex(x => new { x.TenantId, x.BranchId, x.StartsAt });
        builder.HasOne(x => x.Device).WithMany().HasForeignKey(x => x.DeviceId);
        builder.HasOne(x => x.Branch).WithMany().HasForeignKey(x => x.BranchId);
        builder.HasOne(x => x.Customer).WithMany().HasForeignKey(x => x.CustomerId);
        builder.HasOne(x => x.Session).WithMany().HasForeignKey(x => x.SessionId);
        builder.HasOne(x => x.CreatedByUser).WithMany().HasForeignKey(x => x.CreatedByUserId);
    }
}
