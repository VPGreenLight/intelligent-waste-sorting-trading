using System.Text.RegularExpressions;
using Ecolink.Application.Dtos;
using Ecolink.Application.Interfaces;
using Ecolink.Domain.Entities;
using Ecolink.Domain.Exceptions;
using Ecolink.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace Ecolink.Infrastructure.Services;

/// <summary>
/// Quản lý kho thông báo hệ thống bền vững &amp; đẩy thời gian thực SignalR (/hubs/notifications).
/// Áp dụng quy tắc BR-NTF-01..06 (ownership check, unread count sync, che PII trong thông báo).
/// </summary>
public partial class NotificationService : INotificationService
{
    private readonly ApplicationDbContext _db;
    private readonly ILogger<NotificationService> _logger;
    private readonly INotificationRealtimeNotifier? _realtimeNotifier;

    [GeneratedRegex(@"(?<!\d)((?:0|\+84)(?:3|5|7|8|9)\d{8})(?!\d)", RegexOptions.Compiled)]
    private static partial Regex VnPhoneRegex();

    [GeneratedRegex(@"([A-Za-z0-9._%+\-]{1,64})@([A-Za-z0-9.\-]+\.[A-Za-z]{2,})", RegexOptions.Compiled)]
    private static partial Regex EmailRegex();

    public NotificationService(
        ApplicationDbContext db,
        ILogger<NotificationService> logger,
        INotificationRealtimeNotifier? realtimeNotifier = null)
    {
        _db = db;
        _logger = logger;
        _realtimeNotifier = realtimeNotifier;
    }

    public async Task<NotificationListResponseDto> GetMyNotificationsAsync(
        Guid userId,
        NotificationFilterRequest filter,
        CancellationToken ct = default)
    {
        var pageNumber = Math.Max(1, filter.PageNumber);
        var pageSize = Math.Clamp(filter.PageSize, 1, 50);

        var baseUserQuery = _db.Notifications
            .AsNoTracking()
            .Where(n => n.UserId == userId);

        var unreadCount = await baseUserQuery.CountAsync(n => !n.IsRead, ct);

        var filteredQuery = baseUserQuery;
        if (filter.IsRead.HasValue)
        {
            filteredQuery = filteredQuery.Where(n => n.IsRead == filter.IsRead.Value);
        }

        if (!string.IsNullOrWhiteSpace(filter.NotificationType))
        {
            var normalizedType = filter.NotificationType.Trim().ToUpperInvariant();
            filteredQuery = filteredQuery.Where(n => n.NotificationType == normalizedType);
        }

        var totalCount = await filteredQuery.CountAsync(ct);

        var items = await filteredQuery
            .OrderByDescending(n => n.CreatedAt)
            .ThenByDescending(n => n.Id)
            .Skip((pageNumber - 1) * pageSize)
            .Take(pageSize)
            .Select(n => new NotificationDto
            {
                Id = n.Id,
                Title = n.Title,
                Message = n.Message,
                NotificationType = n.NotificationType,
                ReferenceType = n.ReferenceType,
                ReferenceId = n.ReferenceId,
                IsRead = n.IsRead,
                ReadAt = n.ReadAt,
                CreatedAt = n.CreatedAt
            })
            .ToListAsync(ct);

        return new NotificationListResponseDto
        {
            Items = items,
            TotalRecords = totalCount,
            PageNumber = pageNumber,
            PageSize = pageSize,
            UnreadCount = unreadCount
        };
    }

    public async Task<MarkNotificationReadResultDto> MarkAsReadAsync(
        Guid userId,
        Guid notificationId,
        CancellationToken ct = default)
    {
        var entity = await _db.Notifications
            .FirstOrDefaultAsync(n => n.Id == notificationId, ct)
            ?? throw new NotFoundException("Không tìm thấy thông báo hệ thống yêu cầu.");

        if (entity.UserId != userId)
        {
            throw new ForbiddenAccessException(
                "BR-NTF-01",
                "MSG-NTF-02",
                "Bạn không có quyền đánh dấu đã đọc thông báo của người dùng khác.");
        }

        if (!entity.IsRead)
        {
            entity.IsRead = true;
            entity.ReadAt = DateTime.UtcNow;
            await _db.SaveChangesAsync(ct);
        }

        var unreadCount = await _db.Notifications
            .AsNoTracking()
            .CountAsync(n => n.UserId == userId && !n.IsRead, ct);

        await SafePushUnreadCountAsync(userId, unreadCount, ct);

        return new MarkNotificationReadResultDto
        {
            NotificationId = entity.Id,
            IsRead = entity.IsRead,
            ReadAt = entity.ReadAt,
            UnreadCount = unreadCount
        };
    }

    public async Task<MarkAllNotificationsReadResultDto> MarkAllAsReadAsync(
        Guid userId,
        CancellationToken ct = default)
    {
        var unreadItems = await _db.Notifications
            .Where(n => n.UserId == userId && !n.IsRead)
            .ToListAsync(ct);

        var now = DateTime.UtcNow;
        if (unreadItems.Count > 0)
        {
            foreach (var item in unreadItems)
            {
                item.IsRead = true;
                item.ReadAt = now;
            }
            await _db.SaveChangesAsync(ct);
        }

        await SafePushUnreadCountAsync(userId, 0, ct);

        return new MarkAllNotificationsReadResultDto
        {
            MarkedCount = unreadItems.Count,
            UnreadCount = 0,
            ReadAt = now
        };
    }

    public async Task<NotificationDto> CreateAndPushAsync(
        Guid recipientUserId,
        string title,
        string message,
        string notificationType,
        string? referenceType = null,
        Guid? referenceId = null,
        CancellationToken ct = default)
    {
        if (recipientUserId == Guid.Empty)
        {
            throw new BusinessRuleException(
                "BR-NTF-01",
                "MSG-GEN-01",
                "RecipientUserId thông báo hệ thống không hợp lệ.");
        }

        var cleanTitle = MaskSensitivePii(string.IsNullOrWhiteSpace(title) ? "Thông báo từ EcoLink" : title.Trim());
        if (cleanTitle.Length > 200) cleanTitle = cleanTitle[..200];

        var cleanMessage = MaskSensitivePii(message?.Trim() ?? string.Empty);
        var normalizedType = string.IsNullOrWhiteSpace(notificationType)
            ? "SYSTEM"
            : notificationType.Trim().ToUpperInvariant();
        if (normalizedType.Length > 50) normalizedType = normalizedType[..50];

        var entity = new Notification
        {
            Id = Guid.NewGuid(),
            UserId = recipientUserId,
            Title = cleanTitle,
            Message = cleanMessage,
            NotificationType = normalizedType,
            ReferenceType = string.IsNullOrWhiteSpace(referenceType) ? null : referenceType.Trim(),
            ReferenceId = referenceId,
            IsRead = false,
            CreatedAt = DateTime.UtcNow
        };

        _db.Notifications.Add(entity);
        await _db.SaveChangesAsync(ct);

        var unreadCount = await _db.Notifications
            .AsNoTracking()
            .CountAsync(n => n.UserId == recipientUserId && !n.IsRead, ct);

        var dto = new NotificationDto
        {
            Id = entity.Id,
            Title = entity.Title,
            Message = entity.Message,
            NotificationType = entity.NotificationType,
            ReferenceType = entity.ReferenceType,
            ReferenceId = entity.ReferenceId,
            IsRead = entity.IsRead,
            ReadAt = entity.ReadAt,
            CreatedAt = entity.CreatedAt
        };

        if (_realtimeNotifier != null)
        {
            try
            {
                await _realtimeNotifier.PushNotificationAsync(recipientUserId, dto, unreadCount, ct);
            }
            catch (Exception ex)
            {
                _logger.LogWarning(
                    ex,
                    "Không thể đẩy SignalR notification {NotificationId} cho user {UserId}, thông báo vẫn được lưu bền vững trong DB.",
                    entity.Id,
                    recipientUserId);
            }
        }

        return dto;
    }

    private async Task SafePushUnreadCountAsync(Guid userId, int unreadCount, CancellationToken ct)
    {
        if (_realtimeNotifier == null) return;
        try
        {
            await _realtimeNotifier.PushUnreadCountAsync(userId, unreadCount, ct);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Không thể đẩy SignalR UnreadCountUpdated cho user {UserId}.", userId);
        }
    }

    /// <summary>
    /// Che số điện thoại / email cá nhân nếu bị truyền nhầm vào thông báo hệ thống (BR-NTF-05).
    /// </summary>
    public static string MaskSensitivePii(string input)
    {
        if (string.IsNullOrWhiteSpace(input)) return string.Empty;

        var maskedPhone = VnPhoneRegex().Replace(input, m =>
        {
            var raw = m.Value;
            return raw.Length >= 5 ? $"{raw[..3]}***{raw[^2..]}" : "***";
        });

        return EmailRegex().Replace(maskedPhone, m =>
        {
            var local = m.Groups[1].Value;
            var domain = m.Groups[2].Value;
            var prefix = local.Length > 1 ? local[..1] : "*";
            return $"{prefix}***@{domain}";
        });
    }
}
