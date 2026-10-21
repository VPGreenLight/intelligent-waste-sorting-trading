using Ecolink.Application.Interfaces;
using Ecolink.Domain.Common;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;

namespace Ecolink.Infrastructure.Data.Interceptors;

/// <summary>
/// Interceptor tự động điền các trường audit (created_at, created_by, updated_at, updated_by)
/// và xử lý Xóa mềm (Soft Delete) tự động cho mọi thực thể kế thừa BaseAuditableEntity.
/// Tuân thủ quy chuẩn database_design_and_audit_guidelines.md.
/// </summary>
public class AuditableEntityInterceptor : SaveChangesInterceptor
{
    private readonly ICurrentUserService? _currentUserService;

    public AuditableEntityInterceptor(ICurrentUserService? currentUserService = null)
    {
        _currentUserService = currentUserService;
    }

    public override InterceptionResult<int> SavingChanges(DbContextEventData eventData, InterceptionResult<int> result)
    {
        UpdateEntities(eventData.Context);
        return base.SavingChanges(eventData, result);
    }

    public override ValueTask<InterceptionResult<int>> SavingChangesAsync(
        DbContextEventData eventData,
        InterceptionResult<int> result,
        CancellationToken cancellationToken = default)
    {
        UpdateEntities(eventData.Context);
        return base.SavingChangesAsync(eventData, result, cancellationToken);
    }

    private void UpdateEntities(DbContext? context)
    {
        if (context == null) return;

        var now = DateTime.UtcNow;
        var currentUserId = _currentUserService?.UserId?.ToString() ?? "System";

        foreach (var entry in context.ChangeTracker.Entries<BaseAuditableEntity>())
        {
            if (entry.State == EntityState.Added)
            {
                entry.Entity.CreatedAt = now;
                entry.Entity.CreatedBy = currentUserId;
                entry.Entity.IsDeleted = false;
            }
            else if (entry.State == EntityState.Modified)
            {
                entry.Entity.UpdatedAt = now;
                entry.Entity.UpdatedBy = currentUserId;
            }
            else if (entry.State == EntityState.Deleted)
            {
                // Tự động chuyển đổi xóa cứng thành xóa mềm
                entry.State = EntityState.Modified;
                entry.Entity.IsDeleted = true;
                entry.Entity.DeletedAt = now;
                entry.Entity.UpdatedBy = currentUserId;
            }
        }
    }
}
