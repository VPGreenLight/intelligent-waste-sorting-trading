using Ecolink.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;

namespace Ecolink.Infrastructure.Data.Interceptors;

/// <summary>
/// Cưỡng chế quy tắc bất biến BR-ADM-05: Bảng audit_logs chỉ cho phép INSERT (Append-Only),
/// cấm tuyệt đối mọi thao tác UPDATE hoặc DELETE kể cả từ tầng mã nguồn nội bộ.
/// </summary>
public sealed class AuditLogImmutabilityInterceptor : SaveChangesInterceptor
{
    public override InterceptionResult<int> SavingChanges(
        DbContextEventData eventData,
        InterceptionResult<int> result)
    {
        EnsureAuditLogsAreImmutable(eventData.Context);
        return base.SavingChanges(eventData, result);
    }

    public override ValueTask<InterceptionResult<int>> SavingChangesAsync(
        DbContextEventData eventData,
        InterceptionResult<int> result,
        CancellationToken cancellationToken = default)
    {
        EnsureAuditLogsAreImmutable(eventData.Context);
        return base.SavingChangesAsync(eventData, result, cancellationToken);
    }

    private static void EnsureAuditLogsAreImmutable(DbContext? context)
    {
        if (context == null) return;

        var hasIllegalMutation = context.ChangeTracker.Entries<AuditLog>()
            .Any(e => e.State is EntityState.Modified or EntityState.Deleted);

        if (hasIllegalMutation)
        {
            throw new InvalidOperationException(
                "Vi phạm quy tắc bảo mật BR-ADM-05: Nhật ký kiểm toán (AuditLog) là bất biến, cấm chỉnh sửa hoặc xóa.");
        }
    }
}
