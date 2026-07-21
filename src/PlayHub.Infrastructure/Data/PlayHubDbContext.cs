using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Storage.ValueConversion;
using PlayHub.Domain.Common;
using PlayHub.Domain.Entities;

namespace PlayHub.Infrastructure.Data;

/// <summary>
/// SQL Server returns DateTime with Kind=Unspecified, so JSON serialization drops the 'Z' suffix
/// and browsers parse the value as local time (shifting timers by the UTC offset).
/// All timestamps in this app are stored as UTC, so mark them as such when materialized.
/// </summary>
public class UtcDateTimeConverter : ValueConverter<DateTime, DateTime>
{
    public UtcDateTimeConverter() : base(
        v => v,
        v => DateTime.SpecifyKind(v, DateTimeKind.Utc))
    {
    }
}

public class NullableUtcDateTimeConverter : ValueConverter<DateTime?, DateTime?>
{
    public NullableUtcDateTimeConverter() : base(
        v => v,
        v => v.HasValue ? DateTime.SpecifyKind(v.Value, DateTimeKind.Utc) : v)
    {
    }
}

public class PlayHubDbContext : DbContext
{
    private readonly TenantContext _tenantContext;

    public PlayHubDbContext(DbContextOptions<PlayHubDbContext> options, TenantContext tenantContext) : base(options)
    {
        _tenantContext = tenantContext;
    }

    public DbSet<Tenant> Tenants => Set<Tenant>();
    public DbSet<Branch> Branches => Set<Branch>();
    public DbSet<BranchPaymentAccount> BranchPaymentAccounts => Set<BranchPaymentAccount>();
    public DbSet<User> Users => Set<User>();
    public DbSet<Permission> Permissions => Set<Permission>();
    public DbSet<UserPermission> UserPermissions => Set<UserPermission>();
    public DbSet<UserBranch> UserBranches => Set<UserBranch>();
    public DbSet<RefreshToken> RefreshTokens => Set<RefreshToken>();
    public DbSet<Room> Rooms => Set<Room>();
    public DbSet<Device> Devices => Set<Device>();
    public DbSet<ControllerType> ControllerTypes => Set<ControllerType>();
    public DbSet<DeviceController> DeviceControllers => Set<DeviceController>();
    public DbSet<Screen> Screens => Set<Screen>();
    public DbSet<VenueAssetType> VenueAssetTypes => Set<VenueAssetType>();
    public DbSet<RoomAsset> RoomAssets => Set<RoomAsset>();
    public DbSet<MasterAlertSettings> MasterAlertSettings => Set<MasterAlertSettings>();
    public DbSet<DeviceMaintenance> DeviceMaintenances => Set<DeviceMaintenance>();
    public DbSet<DevicePricingPlan> DevicePricingPlans => Set<DevicePricingPlan>();
    public DbSet<PricingPlan> PricingPlans => Set<PricingPlan>();
    public DbSet<GamingRate> GamingRates => Set<GamingRate>();
    public DbSet<WatchingRate> WatchingRates => Set<WatchingRate>();
    public DbSet<Session> Sessions => Set<Session>();
    public DbSet<SessionPause> SessionPauses => Set<SessionPause>();
    public DbSet<SessionCafeteriaLine> SessionCafeteriaLines => Set<SessionCafeteriaLine>();
    public DbSet<SessionCafeteriaReturn> SessionCafeteriaReturns => Set<SessionCafeteriaReturn>();
    public DbSet<Customer> Customers => Set<Customer>();
    public DbSet<WalletTransaction> WalletTransactions => Set<WalletTransaction>();
    public DbSet<CustomerOffer> CustomerOffers => Set<CustomerOffer>();
    public DbSet<CafeteriaItem> CafeteriaItems => Set<CafeteriaItem>();
    public DbSet<CafeteriaItemVariant> CafeteriaItemVariants => Set<CafeteriaItemVariant>();
    public DbSet<CafeteriaVariantRecipeLine> CafeteriaVariantRecipeLines => Set<CafeteriaVariantRecipeLine>();
    public DbSet<CafeteriaAddOn> CafeteriaAddOns => Set<CafeteriaAddOn>();
    public DbSet<CafeteriaSaleLineAddOn> CafeteriaSaleLineAddOns => Set<CafeteriaSaleLineAddOn>();
    public DbSet<CafeteriaSaleLineIngredientDeduct> CafeteriaSaleLineIngredientDeducts => Set<CafeteriaSaleLineIngredientDeduct>();
    public DbSet<SessionCafeteriaLineAddOn> SessionCafeteriaLineAddOns => Set<SessionCafeteriaLineAddOn>();
    public DbSet<SessionCafeteriaLineIngredientDeduct> SessionCafeteriaLineIngredientDeducts => Set<SessionCafeteriaLineIngredientDeduct>();
    public DbSet<InventoryUnit> InventoryUnits => Set<InventoryUnit>();
    public DbSet<ItemUnitConversionLog> ItemUnitConversionLogs => Set<ItemUnitConversionLog>();
    public DbSet<CafeteriaSale> CafeteriaSales => Set<CafeteriaSale>();
    public DbSet<CafeteriaSaleLine> CafeteriaSaleLines => Set<CafeteriaSaleLine>();
    public DbSet<CafeteriaReturn> CafeteriaReturns => Set<CafeteriaReturn>();
    public DbSet<InventoryMovement> InventoryMovements => Set<InventoryMovement>();
    public DbSet<PurchaseOrder> PurchaseOrders => Set<PurchaseOrder>();
    public DbSet<PurchaseOrderLine> PurchaseOrderLines => Set<PurchaseOrderLine>();
    public DbSet<StockVoucher> StockVouchers => Set<StockVoucher>();
    public DbSet<StockVoucherLine> StockVoucherLines => Set<StockVoucherLine>();
    public DbSet<Invoice> Invoices => Set<Invoice>();
    public DbSet<InvoicePayment> InvoicePayments => Set<InvoicePayment>();
    public DbSet<PaymentProof> PaymentProofs => Set<PaymentProof>();
    public DbSet<RevenueEntry> RevenueEntries => Set<RevenueEntry>();
    public DbSet<ExpenseCategory> ExpenseCategories => Set<ExpenseCategory>();
    public DbSet<Expense> Expenses => Set<Expense>();
    public DbSet<AuditLog> AuditLogs => Set<AuditLog>();
    public DbSet<CashCollection> CashCollections => Set<CashCollection>();
    public DbSet<Notification> Notifications => Set<Notification>();

    public override int SaveChanges()
    {
        ApplySoftDeletes();
        return base.SaveChanges();
    }

    public override int SaveChanges(bool acceptAllChangesOnSuccess)
    {
        ApplySoftDeletes();
        return base.SaveChanges(acceptAllChangesOnSuccess);
    }

    public override Task<int> SaveChangesAsync(CancellationToken cancellationToken = default)
    {
        ApplySoftDeletes();
        return base.SaveChangesAsync(cancellationToken);
    }

    public override Task<int> SaveChangesAsync(bool acceptAllChangesOnSuccess, CancellationToken cancellationToken = default)
    {
        ApplySoftDeletes();
        return base.SaveChangesAsync(acceptAllChangesOnSuccess, cancellationToken);
    }

    /// <summary>
    /// System-wide soft delete: any Remove() on ISoftDelete becomes IsDeleted=true (never hard delete).
    /// </summary>
    private void ApplySoftDeletes()
    {
        var deletedBy = _tenantContext.UserId == Guid.Empty ? (Guid?)null : _tenantContext.UserId;
        var now = DateTime.UtcNow;

        foreach (var entry in ChangeTracker.Entries<ISoftDelete>())
        {
            if (entry.State != EntityState.Deleted)
                continue;

            entry.State = EntityState.Modified;
            entry.Entity.IsDeleted = true;
            entry.Entity.DeletedAt = now;
            entry.Entity.DeletedByUserId = deletedBy;

            var isActive = entry.Properties.FirstOrDefault(p => p.Metadata.Name == "IsActive");
            if (isActive is not null && isActive.Metadata.ClrType == typeof(bool))
                isActive.CurrentValue = false;
        }
    }

    protected override void ConfigureConventions(ModelConfigurationBuilder configurationBuilder)
    {
        base.ConfigureConventions(configurationBuilder);
        configurationBuilder.Properties<DateTime>().HaveConversion<UtcDateTimeConverter>();
        configurationBuilder.Properties<DateTime?>().HaveConversion<NullableUtcDateTimeConverter>();
    }

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        base.OnModelCreating(modelBuilder);
        modelBuilder.ApplyConfigurationsFromAssembly(typeof(PlayHubDbContext).Assembly);
        ApplyGlobalQueryFilters(modelBuilder);
        PermissionSeed.Seed(modelBuilder);
        DisableCascadeDeletes(modelBuilder);
        UseClientGeneratedGuidKeys(modelBuilder);
    }

    /// <summary>
    /// BaseEntity always assigns Id = Guid.NewGuid() client-side. Without this, EF treats
    /// Guid keys as store-generated, so new children added to an already-tracked parent
    /// (e.g. session.Pauses.Add) are marked Modified instead of Added and SaveChanges
    /// issues an UPDATE that matches 0 rows (DbUpdateConcurrencyException).
    /// </summary>
    private static void UseClientGeneratedGuidKeys(ModelBuilder modelBuilder)
    {
        foreach (var entityType in modelBuilder.Model.GetEntityTypes())
        {
            var primaryKey = entityType.FindPrimaryKey();
            if (primaryKey is null) continue;

            foreach (var property in primaryKey.Properties)
            {
                if (property.ClrType == typeof(Guid))
                    property.ValueGenerated = Microsoft.EntityFrameworkCore.Metadata.ValueGenerated.Never;
            }
        }
    }

    /// <summary>
    /// SQL Server rejects multiple cascade delete paths (e.g. Tenant→AuditLog and Tenant→User→AuditLog).
    /// </summary>
    private static void DisableCascadeDeletes(ModelBuilder modelBuilder)
    {
        foreach (var foreignKey in modelBuilder.Model.GetEntityTypes()
            .SelectMany(entityType => entityType.GetForeignKeys()))
        {
            foreignKey.DeleteBehavior = DeleteBehavior.NoAction;
        }
    }

    private void ApplyGlobalQueryFilters(ModelBuilder modelBuilder)
    {
        modelBuilder.Entity<Branch>().HasQueryFilter(e =>
            e.TenantId == _tenantContext.TenantId && !e.IsDeleted);

        modelBuilder.Entity<BranchPaymentAccount>().HasQueryFilter(e =>
            e.TenantId == _tenantContext.TenantId && !e.IsDeleted);

        modelBuilder.Entity<User>().HasQueryFilter(e =>
            e.TenantId == _tenantContext.TenantId && !e.IsDeleted);

        modelBuilder.Entity<ControllerType>().HasQueryFilter(e =>
            e.TenantId == _tenantContext.TenantId && !e.IsDeleted);

        modelBuilder.Entity<VenueAssetType>().HasQueryFilter(e =>
            e.TenantId == _tenantContext.TenantId && !e.IsDeleted);

        modelBuilder.Entity<MasterAlertSettings>().HasQueryFilter(e =>
            e.TenantId == _tenantContext.TenantId);

        modelBuilder.Entity<DeviceMaintenance>().HasQueryFilter(e =>
            e.TenantId == _tenantContext.TenantId &&
            !e.IsDeleted &&
            ((_tenantContext.IsSuperAdmin && _tenantContext.BranchId == null)
             || (_tenantContext.BranchId != null && e.BranchId == _tenantContext.BranchId)));

        modelBuilder.Entity<PricingPlan>().HasQueryFilter(e =>
            e.TenantId == _tenantContext.TenantId && !e.IsDeleted);

        modelBuilder.Entity<ExpenseCategory>().HasQueryFilter(e =>
            e.TenantId == _tenantContext.TenantId && !e.IsDeleted);

        modelBuilder.Entity<Customer>().HasQueryFilter(e =>
            e.TenantId == _tenantContext.TenantId && !e.IsDeleted);

        modelBuilder.Entity<WalletTransaction>().HasQueryFilter(e =>
            e.TenantId == _tenantContext.TenantId);

        modelBuilder.Entity<CustomerOffer>().HasQueryFilter(e =>
            e.TenantId == _tenantContext.TenantId && !e.IsDeleted);

        modelBuilder.Entity<Notification>().HasQueryFilter(e => e.TenantId == _tenantContext.TenantId);
        modelBuilder.Entity<AuditLog>().HasQueryFilter(e => e.TenantId == _tenantContext.TenantId);

        modelBuilder.Entity<Room>().HasQueryFilter(e =>
            e.TenantId == _tenantContext.TenantId &&
            !e.IsDeleted &&
            ((_tenantContext.IsSuperAdmin && _tenantContext.BranchId == null)
             || (_tenantContext.BranchId != null && e.BranchId == _tenantContext.BranchId)));

        modelBuilder.Entity<Device>().HasQueryFilter(e =>
            e.TenantId == _tenantContext.TenantId &&
            !e.IsDeleted &&
            ((_tenantContext.IsSuperAdmin && _tenantContext.BranchId == null)
             || (_tenantContext.BranchId != null && e.BranchId == _tenantContext.BranchId)));

        modelBuilder.Entity<Session>().HasQueryFilter(e =>
            e.TenantId == _tenantContext.TenantId &&
            !e.IsDeleted &&
            ((_tenantContext.IsSuperAdmin && _tenantContext.BranchId == null)
             || (_tenantContext.BranchId != null && e.BranchId == _tenantContext.BranchId)));

        modelBuilder.Entity<CafeteriaItem>().HasQueryFilter(e =>
            e.TenantId == _tenantContext.TenantId &&
            !e.IsDeleted &&
            ((_tenantContext.IsSuperAdmin && _tenantContext.BranchId == null)
             || (_tenantContext.BranchId != null && e.BranchId == _tenantContext.BranchId)));

        modelBuilder.Entity<CafeteriaAddOn>().HasQueryFilter(e =>
            e.TenantId == _tenantContext.TenantId &&
            !e.IsDeleted &&
            ((_tenantContext.IsSuperAdmin && _tenantContext.BranchId == null)
             || (_tenantContext.BranchId != null && e.BranchId == _tenantContext.BranchId)));

        modelBuilder.Entity<InventoryUnit>().HasQueryFilter(e =>
            e.TenantId == _tenantContext.TenantId && !e.IsDeleted);

        modelBuilder.Entity<ItemUnitConversionLog>().HasQueryFilter(e =>
            e.TenantId == _tenantContext.TenantId &&
            ((_tenantContext.IsSuperAdmin && _tenantContext.BranchId == null)
             || (_tenantContext.BranchId != null && e.BranchId == _tenantContext.BranchId)));

        modelBuilder.Entity<CafeteriaSale>().HasQueryFilter(e =>
            e.TenantId == _tenantContext.TenantId &&
            !e.IsDeleted &&
            ((_tenantContext.IsSuperAdmin && _tenantContext.BranchId == null)
             || (_tenantContext.BranchId != null && e.BranchId == _tenantContext.BranchId)));

        modelBuilder.Entity<InventoryMovement>().HasQueryFilter(e =>
            e.TenantId == _tenantContext.TenantId &&
            ((_tenantContext.IsSuperAdmin && _tenantContext.BranchId == null)
             || (_tenantContext.BranchId != null && e.BranchId == _tenantContext.BranchId)));

        modelBuilder.Entity<PurchaseOrder>().HasQueryFilter(e =>
            e.TenantId == _tenantContext.TenantId &&
            !e.IsDeleted &&
            ((_tenantContext.IsSuperAdmin && _tenantContext.BranchId == null)
             || (_tenantContext.BranchId != null && e.BranchId == _tenantContext.BranchId)));

        modelBuilder.Entity<StockVoucher>().HasQueryFilter(e =>
            e.TenantId == _tenantContext.TenantId &&
            !e.IsDeleted &&
            ((_tenantContext.IsSuperAdmin && _tenantContext.BranchId == null)
             || (_tenantContext.BranchId != null && e.BranchId == _tenantContext.BranchId)));

        modelBuilder.Entity<Invoice>().HasQueryFilter(e =>
            e.TenantId == _tenantContext.TenantId &&
            !e.IsDeleted &&
            ((_tenantContext.IsSuperAdmin && _tenantContext.BranchId == null)
             || (_tenantContext.BranchId != null && e.BranchId == _tenantContext.BranchId)));

        modelBuilder.Entity<RevenueEntry>().HasQueryFilter(e =>
            e.TenantId == _tenantContext.TenantId &&
            !e.IsDeleted &&
            ((_tenantContext.IsSuperAdmin && _tenantContext.BranchId == null)
             || (_tenantContext.BranchId != null && e.BranchId == _tenantContext.BranchId)));

        modelBuilder.Entity<Expense>().HasQueryFilter(e =>
            e.TenantId == _tenantContext.TenantId &&
            !e.IsDeleted &&
            ((_tenantContext.IsSuperAdmin && _tenantContext.BranchId == null)
             || (_tenantContext.BranchId != null && e.BranchId == _tenantContext.BranchId)));

        modelBuilder.Entity<CashCollection>().HasQueryFilter(e =>
            e.TenantId == _tenantContext.TenantId &&
            ((_tenantContext.IsSuperAdmin && _tenantContext.BranchId == null)
             || (_tenantContext.BranchId != null && e.BranchId == _tenantContext.BranchId)));
    }
}
