namespace PlayHub.Domain.Common;

public abstract class BaseEntity
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
}

public interface ITenantEntity
{
    Guid TenantId { get; set; }
}

public interface IBranchEntity : ITenantEntity
{
    Guid BranchId { get; set; }
}

public interface ISoftDelete
{
    bool IsDeleted { get; set; }
    DateTime? DeletedAt { get; set; }
    Guid? DeletedByUserId { get; set; }
}

public interface IAuditableEntity
{
    DateTime? UpdatedAt { get; set; }
    Guid? CreatedByUserId { get; set; }
    Guid? UpdatedByUserId { get; set; }
}
