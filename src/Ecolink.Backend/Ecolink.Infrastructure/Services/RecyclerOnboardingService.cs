using Ecolink.Application.Dtos;
using Ecolink.Application.Interfaces;
using Ecolink.Domain.Entities;
using Ecolink.Domain.Enums;
using Ecolink.Domain.Exceptions;
using Ecolink.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using NetTopologySuite.Geometries;

namespace Ecolink.Infrastructure.Services;

/// <summary>
/// Hiện thực hóa nghiệp vụ Đăng ký Nâng cấp & Thẩm định Cơ sở thu gom (Module: Recycler Onboarding & Verification)
/// Bao gồm Use Case U-17 (Apply for Recycler Upgrade) và S-04 (Verify Recycler Application).
/// </summary>
public class RecyclerOnboardingService : IRecyclerOnboardingService
{
    private readonly ApplicationDbContext _dbContext;
    private readonly IEmailService _emailService;
    private readonly ILogger<RecyclerOnboardingService> _logger;

    public RecyclerOnboardingService(
        ApplicationDbContext dbContext,
        IEmailService emailService,
        ILogger<RecyclerOnboardingService> logger)
    {
        _dbContext = dbContext;
        _emailService = emailService;
        _logger = logger;
    }

    public async Task<RecyclerApplicationDetailDto> SubmitUpgradeApplicationAsync(
        Guid userId,
        ApplyRecyclerUpgradeRequest request,
        CancellationToken ct = default)
    {
        var user = await _dbContext.Users
            .Include(u => u.RecyclerProfile)
            .FirstOrDefaultAsync(u => u.Id == userId, ct);

        if (user == null)
        {
            throw new NotFoundException("Không tìm thấy tài khoản người dùng.");
        }

        if (user.Status == UserStatus.Suspended || user.Status == UserStatus.Locked)
        {
            throw new BusinessRuleException("BR-AUTH-05", "MSG-AUTH-04",
                "Tài khoản đang bị khóa hoặc đình chỉ, không thể gửi yêu cầu nâng cấp.");
        }

        var existingProfile = user.RecyclerProfile;
        if (existingProfile != null)
        {
            if (existingProfile.VerificationStatus == VerificationStatus.Verified)
            {
                throw new BusinessRuleException("BR-MTH-01", "MSG-MTH-04",
                    "Hồ sơ cơ sở của bạn đã được phê duyệt trước đó.");
            }

            if (existingProfile.VerificationStatus == VerificationStatus.Pending)
            {
                throw new BusinessRuleException("BR-MTH-01", "MSG-MTH-03",
                    "Hồ sơ cơ sở của bạn đang trong hàng đợi thẩm định. Vui lòng chờ kết quả từ nhân viên.");
            }
        }

        var locationPoint = new Point(request.Longitude, request.Latitude) { SRID = 4326 };

        if (existingProfile == null)
        {
            // Nộp mới lần đầu
            existingProfile = new RecyclerProfile
            {
                UserId = user.Id,
                User = user,
                FacilityName = request.FacilityName.Trim(),
                RepresentativeName = request.RepresentativeName.Trim(),
                BusinessAddress = request.BusinessAddress.Trim(),
                Latitude = (decimal)request.Latitude,
                Longitude = (decimal)request.Longitude,
                Location = locationPoint,
                OperatingHours = string.IsNullOrWhiteSpace(request.OperatingHours) ? "08:00 - 18:00" : request.OperatingHours.Trim(),
                ContactPhone = request.ContactPhone.Trim(),
                Description = request.Description?.Trim(),
                LegalPermitUrl = request.LegalPermitUrl.Trim(),
                BusinessPhotoUrl = request.BusinessPhotoUrl.Trim(),
                VerificationStatus = VerificationStatus.Pending,
                ActiveStatus = RecyclerActiveStatus.Offline,
                RejectionReason = null,
                CreatedBy = user.Id.ToString()
            };
            _dbContext.RecyclerProfiles.Add(existingProfile);
        }
        else
        {
            // Nộp lại sau khi bị Rejected (Tuân thủ BR-MTH-02)
            existingProfile.FacilityName = request.FacilityName.Trim();
            existingProfile.RepresentativeName = request.RepresentativeName.Trim();
            existingProfile.BusinessAddress = request.BusinessAddress.Trim();
            existingProfile.Latitude = (decimal)request.Latitude;
            existingProfile.Longitude = (decimal)request.Longitude;
            existingProfile.Location = locationPoint;
            existingProfile.OperatingHours = string.IsNullOrWhiteSpace(request.OperatingHours) ? "08:00 - 18:00" : request.OperatingHours.Trim();
            existingProfile.ContactPhone = request.ContactPhone.Trim();
            existingProfile.Description = request.Description?.Trim();
            existingProfile.LegalPermitUrl = request.LegalPermitUrl.Trim();
            existingProfile.BusinessPhotoUrl = request.BusinessPhotoUrl.Trim();
            existingProfile.VerificationStatus = VerificationStatus.Pending;
            existingProfile.RejectionReason = null;
            existingProfile.UpdatedAt = DateTime.UtcNow;
            existingProfile.UpdatedBy = user.Id.ToString();
        }

        await _dbContext.SaveChangesAsync(ct);

        _logger.LogInformation("Người dùng {Username} ({UserId}) đã gửi hồ sơ nâng cấp Recycler cho cơ sở '{Facility}'",
            user.Username, user.Id, existingProfile.FacilityName);

        return MapToDetailDto(user, existingProfile);
    }

    public async Task<RecyclerApplicationDetailDto> GetMyApplicationAsync(Guid userId, CancellationToken ct = default)
    {
        var user = await _dbContext.Users
            .Include(u => u.RecyclerProfile)
            .FirstOrDefaultAsync(u => u.Id == userId, ct);

        if (user == null || user.RecyclerProfile == null)
        {
            throw new NotFoundException("Bạn chưa gửi hồ sơ đăng ký nâng cấp Cơ sở thu gom nào.");
        }

        return MapToDetailDto(user, user.RecyclerProfile);
    }

    public async Task<PagedResult<RecyclerApplicationDetailDto>> GetApplicationsForStaffAsync(
        RecyclerApplicationFilterRequest filter,
        CancellationToken ct = default)
    {
        var query = _dbContext.RecyclerProfiles
            .Include(r => r.User)
            .AsNoTracking()
            .AsQueryable();

        if (!string.IsNullOrWhiteSpace(filter.Status) &&
            Enum.TryParse<VerificationStatus>(filter.Status.Trim(), true, out var parsedStatus))
        {
            query = query.Where(r => r.VerificationStatus == parsedStatus);
        }

        if (!string.IsNullOrWhiteSpace(filter.Keyword))
        {
            var kw = filter.Keyword.Trim().ToLower();
            query = query.Where(r =>
                r.FacilityName.ToLower().Contains(kw) ||
                r.BusinessAddress.ToLower().Contains(kw) ||
                r.ContactPhone.Contains(kw) ||
                r.User.Email.ToLower().Contains(kw) ||
                r.User.FullName.ToLower().Contains(kw));
        }

        var totalRecords = await query.CountAsync(ct);

        query = filter.SortOrder.Equals("desc", StringComparison.OrdinalIgnoreCase)
            ? query.OrderByDescending(r => r.CreatedAt)
            : query.OrderBy(r => r.CreatedAt);

        var pageNumber = Math.Max(1, filter.PageNumber);
        var pageSize = Math.Clamp(filter.PageSize, 1, 100);

        var items = await query
            .Skip((pageNumber - 1) * pageSize)
            .Take(pageSize)
            .ToListAsync(ct);

        return new PagedResult<RecyclerApplicationDetailDto>
        {
            Items = items.Select(r => MapToDetailDto(r.User, r)).ToList(),
            PageNumber = pageNumber,
            PageSize = pageSize,
            TotalRecords = totalRecords
        };
    }

    public async Task<RecyclerApplicationDetailDto> GetApplicationByUserIdAsync(Guid targetUserId, CancellationToken ct = default)
    {
        var profile = await _dbContext.RecyclerProfiles
            .Include(r => r.User)
            .AsNoTracking()
            .FirstOrDefaultAsync(r => r.UserId == targetUserId, ct);

        if (profile == null)
        {
            throw new NotFoundException($"Không tìm thấy hồ sơ đăng ký Recycler của người dùng '{targetUserId}'.");
        }

        return MapToDetailDto(profile.User, profile);
    }

    public async Task<RecyclerApplicationDetailDto> VerifyApplicationAsync(
        Guid staffUserId,
        Guid targetUserId,
        VerifyRecyclerApplicationRequest request,
        CancellationToken ct = default)
    {
        var profile = await _dbContext.RecyclerProfiles
            .Include(r => r.User)
            .FirstOrDefaultAsync(r => r.UserId == targetUserId, ct);

        if (profile == null)
        {
            throw new NotFoundException("Không tìm thấy hồ sơ đăng ký Recycler cần thẩm định.");
        }

        if (profile.VerificationStatus != VerificationStatus.Pending)
        {
            throw new BusinessRuleException("BR-MTH-01", "MSG-GEN-01",
                $"Chỉ có thể thẩm định hồ sơ đang ở trạng thái 'Pending' (Trạng thái hiện tại: '{profile.VerificationStatus}').");
        }

        if (!request.IsApproved && string.IsNullOrWhiteSpace(request.RejectionReason))
        {
            throw new BusinessRuleException("BR-MTH-02", "MSG-MTH-05",
                "Bắt buộc phải nhập lý do từ chối khi không phê duyệt hồ sơ (BR-MTH-02).");
        }

        if (request.IsApproved)
        {
            // Phê duyệt: Chuyển trạng thái hồ sơ -> Verified và nâng cấp Role -> Recycler
            profile.VerificationStatus = VerificationStatus.Verified;
            profile.RejectionReason = null;
            profile.UpdatedAt = DateTime.UtcNow;
            profile.UpdatedBy = staffUserId.ToString();

            profile.User.Role = UserRole.Recycler;
            if (profile.User.Status == UserStatus.PendingVerification)
            {
                profile.User.Status = UserStatus.Active;
            }
            profile.User.UpdatedAt = DateTime.UtcNow;
            profile.User.UpdatedBy = staffUserId.ToString();
        }
        else
        {
            // Từ chối: Lưu lý do từ chối theo BR-MTH-02
            profile.VerificationStatus = VerificationStatus.Rejected;
            profile.RejectionReason = request.RejectionReason!.Trim();
            profile.ActiveStatus = RecyclerActiveStatus.Offline;
            profile.UpdatedAt = DateTime.UtcNow;
            profile.UpdatedBy = staffUserId.ToString();
        }

        await _dbContext.SaveChangesAsync(ct);

        _logger.LogInformation("Staff {StaffId} đã {Decision} hồ sơ Recycler của User {TargetUserId} ({Facility})",
            staffUserId, request.IsApproved ? "APPROVE" : "REJECT", targetUserId, profile.FacilityName);

        // Gửi email thông báo kết quả (BR-NTF-02, BR-NTF-06)
        await SendVerificationNotificationEmailAsync(profile.User, profile, request.IsApproved, ct);

        return MapToDetailDto(profile.User, profile);
    }

    private async Task SendVerificationNotificationEmailAsync(
        User user,
        RecyclerProfile profile,
        bool isApproved,
        CancellationToken ct)
    {
        var subject = isApproved
            ? "[EcoLink] Hồ sơ Cơ sở thu mua của bạn đã được phê duyệt"
            : "[EcoLink] Thông báo kết quả thẩm định hồ sơ Cơ sở thu mua";

        var htmlBody = isApproved
            ? $@"<div style='font-family: Arial, sans-serif; max-width: 600px; margin: auto; padding: 20px; border: 1px solid #e0e0e0; border-radius: 8px;'>
                    <h2 style='color: #2e7d32;'>Chúc mừng! Hồ sơ Cơ sở thu mua đã được duyệt</h2>
                    <p>Xin chào <strong>{user.FullName}</strong>,</p>
                    <p>Hồ sơ cơ sở <strong>{profile.FacilityName}</strong> của bạn đã được duyệt và hiện hiển thị với người dùng (MSG-MTH-04).</p>
                    <p>Vui lòng truy cập ứng dụng EcoLink để thiết lập danh mục phế liệu nhận thu mua và bảng giá niêm yết (BR-MTH-03).</p>
                 </div>"
            : $@"<div style='font-family: Arial, sans-serif; max-width: 600px; margin: auto; padding: 20px; border: 1px solid #e0e0e0; border-radius: 8px;'>
                    <h2 style='color: #c62828;'>Thông báo kết quả thẩm định hồ sơ Cơ sở thu mua</h2>
                    <p>Xin chào <strong>{user.FullName}</strong>,</p>
                    <p>Hồ sơ cơ sở <strong>{profile.FacilityName}</strong> của bạn chưa được duyệt: <strong>{profile.RejectionReason}</strong>.</p>
                    <p>Vui lòng cập nhật lại thông tin/chứng từ theo hướng dẫn trên và gửi lại hồ sơ (MSG-MTH-05).</p>
                 </div>";

        await _emailService.SendEmailAsync(user.Email, user.FullName, subject, htmlBody, ct);
    }

    private static RecyclerApplicationDetailDto MapToDetailDto(User user, RecyclerProfile profile) =>
        new()
        {
            UserId = user.Id,
            Username = user.Username,
            Email = user.Email,
            ApplicantFullName = user.FullName,
            CurrentRole = user.Role.ToString(),
            FacilityName = profile.FacilityName,
            RepresentativeName = profile.RepresentativeName,
            BusinessAddress = profile.BusinessAddress,
            Latitude = profile.Latitude,
            Longitude = profile.Longitude,
            OperatingHours = profile.OperatingHours,
            ContactPhone = profile.ContactPhone,
            Description = profile.Description,
            LegalPermitUrl = profile.LegalPermitUrl,
            BusinessPhotoUrl = profile.BusinessPhotoUrl,
            VerificationStatus = profile.VerificationStatus.ToString(),
            ActiveStatus = profile.ActiveStatus.ToString(),
            RejectionReason = profile.RejectionReason,
            SubmittedAt = profile.CreatedAt,
            ReviewedAt = profile.UpdatedAt,
            ReviewedBy = profile.UpdatedBy
        };
}

