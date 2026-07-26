namespace PlayHub.Domain.Common;

public static class SoftDeleteExtensions
{
    public static void MarkAsDeleted(this ISoftDelete entity, Guid? deletedByUserId = null)
    {
        entity.IsDeleted = true;
        entity.DeletedAt = DateTime.UtcNow;
        entity.DeletedByUserId = deletedByUserId;
    }

    public static void RestoreFromDeleted(this ISoftDelete entity)
    {
        entity.IsDeleted = false;
        entity.DeletedAt = null;
        entity.DeletedByUserId = null;
    }
}
