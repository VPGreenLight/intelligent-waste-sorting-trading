using System.Globalization;
using System.Text.Json;
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
/// Hiện thực nghiệp vụ Recycler Profile, Pricing &amp; Availability (UC R-01, R-02).
/// Quy tắc áp dụng: BR-MTH-01 (phải được Staff duyệt), BR-MTH-03 (cần ≥ 1 loại phế liệu + vị trí hợp lệ),
/// BR-MTH-06 (Offline chỉ ẩn khỏi bản đồ), BR-MTH-07 (ngưng mua thay vì xóa cứng).
/// </summary>
public class RecyclerManagementService : IRecyclerManagementService
{
    private const int PriceStaleDays = 30;
    private const string NonRecyclableCode = "trash";
    private const int OperatingHoursMaxLength = 100; // recycler_profile.operating_hours VARCHAR(100)

    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);
    private static readonly string[] DayLabels = { "CN", "T2", "T3", "T4", "T5", "T6", "T7" };

    // Danh mục rác chuẩn (5 nhóm phế liệu thương mại IsRecyclable=true + 5 nhóm chỉ áp dụng Policy xử lý)
    private static readonly (string Code, string Name, string BinColor, bool IsRecyclable)[] DefaultCategories =
    {
        ("plastic", "Nhựa tái chế (Plastic)", "Cam", true),
        ("paper", "Giấy báo & Tài liệu (Paper)", "Xanh Dương", true),
        ("glass", "Thủy tinh (Glass)", "Trắng", true),
        ("metal", "Kim loại & Vỏ lon (Metal)", "Xám Kim Loại", true),
        ("cardboard", "Bìa Carton (Cardboard)", "Nâu Vàng", true),
        ("battery", "Pin & Rác nguy hại (Battery)", "Đỏ Nguy Hại", false),
        ("biological", "Rác hữu cơ & Thực phẩm (Biological)", "Xanh Lá", false),
        ("clothes", "Vải vóc & Quần áo cũ (Clothes)", "Tím Đồ Cũ", false),
        ("shoes", "Giày dép cũ (Shoes)", "Tím Đồ Cũ", false),
        ("trash", "Rác thải thông thường / Không thể tái chế", "Đen", false)
    };

    private readonly ApplicationDbContext _db;
    private readonly ILogger<RecyclerManagementService> _logger;
    private readonly IAuditLogService? _auditLogService;
    private readonly IWasteRuleEngineService? _ruleEngine;

    public RecyclerManagementService(
        ApplicationDbContext db,
        ILogger<RecyclerManagementService> logger,
        IAuditLogService? auditLogService = null,
        IWasteRuleEngineService? ruleEngine = null)
    {
        _db = db;
        _logger = logger;
        _auditLogService = auditLogService;
        _ruleEngine = ruleEngine;
    }

    // ============================================================
    // Danh mục phế liệu chuẩn
    // ============================================================
    public async Task<IReadOnlyList<WasteCategoryDto>> GetWasteCategoriesAsync(CancellationToken ct = default)
    {
        await EnsureDefaultCategoriesSeededAsync(ct);

        return await _db.WasteCategories
            .AsNoTracking()
            .Where(c => c.IsRecyclable && c.Code != NonRecyclableCode && c.Status == "Active")
            .OrderBy(c => c.Name)
            .Select(c => new WasteCategoryDto
            {
                Id = c.Id,
                Code = c.Code,
                Name = c.Name,
                Description = c.Description,
                StandardBinColor = c.StandardBinColor,
                IconUrl = c.IconUrl,
                IsRecyclable = c.IsRecyclable,
                Status = c.Status,
                HasSortingRule = c.SortingRule != null,
                RuleStatus = c.SortingRule != null ? c.SortingRule.Status : null,
                CreatedAt = c.CreatedAt,
                UpdatedAt = c.UpdatedAt
            })
            .ToListAsync(ct);
    }

    public async Task<IReadOnlyList<WasteCategoryDto>> GetAllWasteCategoriesAsync(
        bool includeInactive,
        bool includeNonRecyclable,
        CancellationToken ct = default)
    {
        await EnsureDefaultCategoriesSeededAsync(ct);

        var query = _db.WasteCategories
            .AsNoTracking()
            .Include(c => c.SortingRule)
            .AsQueryable();

        if (!includeInactive)
        {
            query = query.Where(c => c.Status == "Active");
        }

        if (!includeNonRecyclable)
        {
            query = query.Where(c => c.IsRecyclable && c.Code != NonRecyclableCode);
        }

        return await query
            .OrderBy(c => c.Name)
            .Select(c => new WasteCategoryDto
            {
                Id = c.Id,
                Code = c.Code,
                Name = c.Name,
                Description = c.Description,
                StandardBinColor = c.StandardBinColor,
                IconUrl = c.IconUrl,
                IsRecyclable = c.IsRecyclable,
                Status = c.Status,
                HasSortingRule = c.SortingRule != null,
                RuleStatus = c.SortingRule != null ? c.SortingRule.Status : null,
                CreatedAt = c.CreatedAt,
                UpdatedAt = c.UpdatedAt
            })
            .ToListAsync(ct);
    }

    public async Task<WasteCategoryDto> CreateWasteCategoryAsync(
        Guid adminUserId,
        CreateWasteCategoryRequest request,
        CancellationToken ct = default)
    {
        var code = request.Code.Trim().ToLowerInvariant();
        if (await _db.WasteCategories.AsNoTracking().AnyAsync(c => c.Code == code, ct))
        {
            throw new ConflictException(
                "BR-SRT-02",
                "MSG-SRT-02",
                $"Mã danh mục rác '{code}' đã tồn tại trong hệ thống.");
        }

        var now = DateTime.UtcNow;
        var entity = new WasteCategory
        {
            Code = code,
            Name = request.Name.Trim(),
            Description = string.IsNullOrWhiteSpace(request.Description) ? null : request.Description.Trim(),
            StandardBinColor = request.StandardBinColor.Trim(),
            IconUrl = string.IsNullOrWhiteSpace(request.IconUrl) ? null : request.IconUrl.Trim(),
            IsRecyclable = request.IsRecyclable,
            Status = "Active",
            CreatedAt = now,
            UpdatedAt = now
        };

        var defaultRule = new SortingRule
        {
            WasteCategoryId = entity.Id,
            WasteCategory = entity,
            Title = $"Hướng dẫn phân loại {entity.Name}",
            SortingInstruction = !string.IsNullOrWhiteSpace(entity.Description)
                ? entity.Description
                : $"Tách riêng {entity.Name} khỏi các nhóm rác khác để tránh lẫn tạp chất.",
            PreparationSteps = entity.IsRecyclable
                ? "1. Làm sạch sơ bộ và để khô ráo trước khi phân loại.\n2. Xếp gọn để tối ưu thể tích lưu trữ."
                : "1. Buộc kín miệng túi rác.\n2. Không trộn lẫn vào nhóm phế liệu tái chế.",
            HandlingAction = entity.IsRecyclable
                ? $"Bỏ vào thùng rác màu {entity.StandardBinColor} hoặc đặt lịch thu gom trên EcoLink."
                : $"Bỏ vào thùng rác màu {entity.StandardBinColor} theo quy định thu gom rác sinh hoạt.",
            HazardWarning = null,
            ApplicableRegion = "VN_NATIONAL",
            Status = "DRAFT",
            CreatedAt = now,
            UpdatedAt = now
        };

        entity.SortingRule = defaultRule;
        _db.WasteCategories.Add(entity);
        _db.SortingRules.Add(defaultRule);
        await _db.SaveChangesAsync(ct);

        if (_auditLogService != null)
        {
            await _auditLogService.LogAsync(
                action: "CREATE_WASTE_CATEGORY",
                entityType: nameof(WasteCategory),
                entityId: entity.Id.ToString(),
                oldValues: null,
                newValues: new { entity.Code, entity.Name, entity.StandardBinColor, entity.IsRecyclable, entity.Status },
                reason: $"Tạo mới danh mục rác '{entity.Code}' ({entity.Name}).",
                explicitActorId: adminUserId,
                ct: ct);
        }

        return MapCategory(entity);
    }

    public async Task<WasteCategoryDto> UpdateWasteCategoryAsync(
        Guid adminUserId,
        Guid categoryId,
        UpdateWasteCategoryRequest request,
        CancellationToken ct = default)
    {
        var entity = await _db.WasteCategories
            .Include(c => c.SortingRule)
            .FirstOrDefaultAsync(c => c.Id == categoryId, ct)
            ?? throw new NotFoundException($"Không tìm thấy danh mục rác với ID '{categoryId}'.");

        var oldSnapshot = new
        {
            entity.Name,
            entity.Description,
            entity.StandardBinColor,
            entity.IconUrl,
            entity.IsRecyclable
        };

        entity.Name = request.Name.Trim();
        entity.Description = string.IsNullOrWhiteSpace(request.Description) ? null : request.Description.Trim();
        entity.StandardBinColor = request.StandardBinColor.Trim();
        entity.IconUrl = string.IsNullOrWhiteSpace(request.IconUrl) ? null : request.IconUrl.Trim();
        entity.IsRecyclable = request.IsRecyclable;
        entity.UpdatedAt = DateTime.UtcNow;

        await _db.SaveChangesAsync(ct);

        if (_ruleEngine != null)
        {
            await _ruleEngine.InvalidateRuleCacheAsync(entity.Code, ct);
        }

        if (_auditLogService != null)
        {
            await _auditLogService.LogAsync(
                action: "UPDATE_WASTE_CATEGORY",
                entityType: nameof(WasteCategory),
                entityId: entity.Id.ToString(),
                oldValues: oldSnapshot,
                newValues: new
                {
                    entity.Name,
                    entity.Description,
                    entity.StandardBinColor,
                    entity.IconUrl,
                    entity.IsRecyclable
                },
                reason: $"Cập nhật danh mục rác '{entity.Code}'.",
                explicitActorId: adminUserId,
                ct: ct);
        }

        return MapCategory(entity);
    }

    public async Task<WasteCategoryDto> ToggleWasteCategoryStatusAsync(
        Guid adminUserId,
        Guid categoryId,
        ToggleWasteCategoryStatusRequest request,
        CancellationToken ct = default)
    {
        var entity = await _db.WasteCategories
            .Include(c => c.SortingRule)
            .FirstOrDefaultAsync(c => c.Id == categoryId, ct)
            ?? throw new NotFoundException($"Không tìm thấy danh mục rác với ID '{categoryId}'.");

        var normalizedStatus = request.Status.Trim().ToLowerInvariant() switch
        {
            "active" => "Active",
            "inactive" => "Inactive",
            _ => throw new BusinessRuleException(
                "BR-SRT-04",
                "MSG-SRT-04",
                "Trạng thái danh mục chỉ chấp nhận 'Active' hoặc 'Inactive'.")
        };

        var oldStatus = entity.Status;
        if (!string.Equals(oldStatus, normalizedStatus, StringComparison.Ordinal))
        {
            entity.Status = normalizedStatus;
            entity.UpdatedAt = DateTime.UtcNow;
            await _db.SaveChangesAsync(ct);

            if (_ruleEngine != null)
            {
                await _ruleEngine.InvalidateRuleCacheAsync(entity.Code, ct);
            }

            if (_auditLogService != null)
            {
                await _auditLogService.LogAsync(
                    action: "TOGGLE_WASTE_CATEGORY_STATUS",
                    entityType: nameof(WasteCategory),
                    entityId: entity.Id.ToString(),
                    oldValues: new { Status = oldStatus },
                    newValues: new { Status = entity.Status },
                    reason: $"Chuyển trạng thái danh mục '{entity.Code}' từ {oldStatus} sang {entity.Status} (xóa mềm theo BR-SRT-04).",
                    explicitActorId: adminUserId,
                    ct: ct);
            }
        }

        return MapCategory(entity);
    }

    private async Task EnsureDefaultCategoriesSeededAsync(CancellationToken ct)
    {
        var existingCodes = await _db.WasteCategories.AsNoTracking().Select(c => c.Code).ToListAsync(ct);
        var missing = DefaultCategories.Where(d => !existingCodes.Contains(d.Code)).ToList();
        if (missing.Count > 0)
        {
            foreach (var (code, name, bin, isRecyclable) in missing)
            {
                _db.WasteCategories.Add(new WasteCategory
                {
                    Code = code,
                    Name = name,
                    StandardBinColor = bin,
                    IsRecyclable = isRecyclable,
                    Status = "Active"
                });
            }
            await _db.SaveChangesAsync(ct);
            _logger.LogInformation("Đã khởi tạo {Count} danh mục rác chuẩn.", missing.Count);
        }
    }

    private static WasteCategoryDto MapCategory(WasteCategory c) => new()
    {
        Id = c.Id,
        Code = c.Code,
        Name = c.Name,
        Description = c.Description,
        StandardBinColor = c.StandardBinColor,
        IconUrl = c.IconUrl,
        IsRecyclable = c.IsRecyclable,
        Status = c.Status,
        HasSortingRule = c.SortingRule != null,
        RuleStatus = c.SortingRule?.Status,
        CreatedAt = c.CreatedAt,
        UpdatedAt = c.UpdatedAt
    };

    // ============================================================
    // Recycler Profile
    // ============================================================
    public async Task<RecyclerProfileDto> GetProfileAsync(Guid recyclerId, CancellationToken ct = default)
    {
        var profile = await LoadProfileAsync(recyclerId, ct);
        return MapProfile(profile, await CountActivePricesAsync(recyclerId, ct));
    }

    public async Task<RecyclerProfileDto> UpdateProfileAsync(Guid recyclerId, UpdateRecyclerProfileRequest request, CancellationToken ct = default)
    {
        var profile = await LoadProfileAsync(recyclerId, ct);
        EnsureNotSuspended(profile);

        if (request.Latitude == 0 && request.Longitude == 0)
        {
            throw new BusinessRuleException("BR-MTH-03", "MSG-MTH-06",
                "Vị trí cơ sở không hợp lệ. Vui lòng chọn vị trí trên bản đồ hoặc nhập tọa độ chính xác.");
        }

        profile.FacilityName = request.FacilityName.Trim();
        profile.RepresentativeName = Normalize(request.RepresentativeName);
        profile.BusinessAddress = request.BusinessAddress.Trim();
        profile.ContactPhone = request.ContactPhone.Trim();
        profile.Description = Normalize(request.Description);
        profile.BusinessPhotoUrl = Normalize(request.BusinessPhotoUrl);
        profile.Latitude = (decimal)request.Latitude;
        profile.Longitude = (decimal)request.Longitude;
        profile.Location = new Point(request.Longitude, request.Latitude) { SRID = 4326 };

        // BR-MTH-02: hồ sơ bị từ chối được phép chỉnh sửa và gửi duyệt lại
        if (profile.VerificationStatus == VerificationStatus.Rejected)
        {
            profile.VerificationStatus = VerificationStatus.Pending;
            profile.RejectionReason = null;
            profile.ActiveStatus = RecyclerActiveStatus.Offline;
        }

        await _db.SaveChangesAsync(ct);
        _logger.LogInformation("Recycler {RecyclerId} đã cập nhật hồ sơ cơ sở.", recyclerId);

        return MapProfile(profile, await CountActivePricesAsync(recyclerId, ct));
    }

    // ============================================================
    // Pricing (R-01)
    // ============================================================
    public async Task<IReadOnlyList<RecyclerPriceDto>> GetPricesAsync(Guid recyclerId, string? keyword, bool? isActive, CancellationToken ct = default)
    {
        IQueryable<RecyclerPrice> query = _db.RecyclerPrices
            .AsNoTracking()
            .Include(p => p.WasteCategory)
            .Where(p => p.RecyclerId == recyclerId);

        if (isActive.HasValue)
        {
            var status = isActive.Value ? PriceStatus.Active : PriceStatus.Inactive;
            query = query.Where(p => p.Status == status);
        }

        if (!string.IsNullOrWhiteSpace(keyword))
        {
            if (_db.Database.IsRelational())
            {
                var pattern = $"%{keyword.Trim()}%";
                query = query.Where(p => EF.Functions.ILike(p.WasteCategory!.Name, pattern)
                                         || EF.Functions.ILike(p.WasteCategory!.Code, pattern));
            }
            else
            {
                var kw = keyword.Trim().ToLowerInvariant();
                query = query.Where(p => p.WasteCategory != null &&
                                         (p.WasteCategory.Name.ToLower().Contains(kw) ||
                                          p.WasteCategory.Code.ToLower().Contains(kw)));
            }
        }

        var items = await query.OrderBy(p => p.WasteCategory!.Name).ToListAsync(ct);
        return items.Select(MapPrice).ToList();
    }

    public async Task<RecyclerPriceDto> AddPriceAsync(Guid recyclerId, CreateRecyclerPriceRequest request, CancellationToken ct = default)
    {
        var profile = await LoadProfileAsync(recyclerId, ct);
        EnsureVerifiedAndOperable(profile);

        var categoryId = request.WasteCategoryId!.Value;
        var price = request.PricePerKg!.Value;
        ValidatePriceValues(price, request.MinQuantityKg, request.CollectionConditions);

        var category = await _db.WasteCategories.FirstOrDefaultAsync(c => c.Id == categoryId, ct)
                       ?? throw new NotFoundException("Loại phế liệu không tồn tại.");
        EnsurePurchasable(category);

        var exists = await _db.RecyclerPrices.AnyAsync(p => p.RecyclerId == recyclerId && p.WasteCategoryId == categoryId, ct);
        if (exists)
        {
            throw new BusinessRuleException("BR-MTH-03", "MSG-GEN-01",
                "Loại phế liệu này đã có trong bảng giá. Vui lòng chỉnh sửa dòng hiện có.");
        }

        var entity = new RecyclerPrice
        {
            RecyclerId = recyclerId,
            WasteCategoryId = categoryId,
            WasteCategory = category,
            PricePerKg = price,
            MinQuantityKg = request.MinQuantityKg,
            CollectionConditions = Normalize(request.CollectionConditions),
            Status = PriceStatus.Active
        };

        _db.RecyclerPrices.Add(entity);
        await _db.SaveChangesAsync(ct);

        return MapPrice(entity);
    }

    public async Task<RecyclerPriceDto> UpdatePriceAsync(Guid recyclerId, Guid priceId, UpdateRecyclerPriceRequest request, CancellationToken ct = default)
    {
        var profile = await LoadProfileAsync(recyclerId, ct);
        EnsureVerifiedAndOperable(profile);

        var entity = await _db.RecyclerPrices
            .Include(p => p.WasteCategory)
            .FirstOrDefaultAsync(p => p.Id == priceId && p.RecyclerId == recyclerId, ct)
            ?? throw new NotFoundException("Không tìm thấy dòng giá cần cập nhật.");

        if (request.PricePerKg.HasValue) entity.PricePerKg = request.PricePerKg.Value;
        if (request.MinQuantityKg.HasValue) entity.MinQuantityKg = request.MinQuantityKg.Value;
        if (request.CollectionConditions != null) entity.CollectionConditions = Normalize(request.CollectionConditions);
        if (request.IsActive.HasValue) entity.Status = request.IsActive.Value ? PriceStatus.Active : PriceStatus.Inactive;
        entity.UpdatedAt = DateTime.UtcNow;

        ValidatePriceValues(entity.PricePerKg, entity.MinQuantityKg, entity.CollectionConditions);

        // EF-03: không để hồ sơ đang Online mà không còn loại phế liệu nào được nhận mua
        if (entity.Status != PriceStatus.Active && profile.ActiveStatus == RecyclerActiveStatus.Online)
        {
            var otherActive = await _db.RecyclerPrices.CountAsync(
                p => p.RecyclerId == recyclerId && p.Id != priceId && p.Status == PriceStatus.Active, ct);
            if (otherActive == 0)
            {
                throw ProfileIncomplete();
            }
        }

        await _db.SaveChangesAsync(ct);
        return MapPrice(entity);
    }

    public async Task<IReadOnlyList<RecyclerPriceDto>> BulkSavePricesAsync(Guid recyclerId, BulkSavePricesRequest request, CancellationToken ct = default)
    {
        var profile = await LoadProfileAsync(recyclerId, ct);
        EnsureVerifiedAndOperable(profile);

        var categoryIds = request.Items.Select(i => i.WasteCategoryId!.Value).ToList();
        if (categoryIds.Distinct().Count() != categoryIds.Count)
        {
            throw new BusinessRuleException("BR-MTH-03", "MSG-GEN-01", "Danh sách có loại phế liệu bị trùng lặp.");
        }

        var categories = await _db.WasteCategories
            .Where(c => categoryIds.Contains(c.Id))
            .ToDictionaryAsync(c => c.Id, ct);
        if (categories.Count != categoryIds.Count)
        {
            throw new NotFoundException("Có loại phế liệu không tồn tại trong danh mục hệ thống.");
        }

        var rows = await _db.RecyclerPrices
            .Include(p => p.WasteCategory)
            .Where(p => p.RecyclerId == recyclerId)
            .ToListAsync(ct);

        foreach (var item in request.Items)
        {
            var categoryId = item.WasteCategoryId!.Value;
            var category = categories[categoryId];
            EnsurePurchasable(category);
            ValidatePriceValues(item.PricePerKg!.Value, item.MinQuantityKg, item.CollectionConditions);

            var row = rows.FirstOrDefault(p => p.WasteCategoryId == categoryId);
            if (row == null)
            {
                if (!item.IsActive) continue; // Chưa có dòng giá và không nhận mua => không cần tạo

                row = new RecyclerPrice
                {
                    RecyclerId = recyclerId,
                    WasteCategoryId = categoryId,
                    WasteCategory = category
                };
                _db.RecyclerPrices.Add(row);
                rows.Add(row);
            }

            row.PricePerKg = item.PricePerKg!.Value;
            row.MinQuantityKg = item.MinQuantityKg;
            row.CollectionConditions = Normalize(item.CollectionConditions);
            row.Status = item.IsActive ? PriceStatus.Active : PriceStatus.Inactive;
            row.UpdatedAt = DateTime.UtcNow; // làm mới mốc "cập nhật lần cuối" dù giá trị không đổi (SCR-RCY-01, JOB-04)
        }

        if (profile.ActiveStatus == RecyclerActiveStatus.Online && rows.All(p => p.Status != PriceStatus.Active))
        {
            throw ProfileIncomplete();
        }

        await _db.SaveChangesAsync(ct);
        _logger.LogInformation("Recycler {RecyclerId} đã lưu bảng giá ({Count} dòng).", recyclerId, request.Items.Count);

        return rows.OrderBy(p => p.WasteCategory?.Name).Select(MapPrice).ToList();
    }

    // ============================================================
    // Availability (R-02)
    // ============================================================
    public async Task<AvailabilityDto> GetAvailabilityAsync(Guid recyclerId, CancellationToken ct = default)
    {
        var profile = await LoadProfileAsync(recyclerId, ct);
        return MapAvailability(profile, await CountActivePricesAsync(recyclerId, ct));
    }

    public async Task<AvailabilityDto> UpdateAvailabilityAsync(Guid recyclerId, UpdateAvailabilityRequest request, CancellationToken ct = default)
    {
        var profile = await LoadProfileAsync(recyclerId, ct);
        var activeCount = await CountActivePricesAsync(recyclerId, ct);

        if (request.IsOnline!.Value)
        {
            EnsureVerifiedAndOperable(profile);

            var hasLocation = profile.Location != null && !(profile.Latitude == 0 && profile.Longitude == 0);
            if (activeCount == 0 || !hasLocation)
            {
                throw ProfileIncomplete();
            }

            profile.ActiveStatus = RecyclerActiveStatus.Online;
            profile.BusyReason = null;
        }
        else
        {
            // Chuyển sang Offline luôn được phép; chỉ ẩn khỏi bản đồ, đơn đang xử lý không bị ảnh hưởng (BR-MTH-06)
            profile.ActiveStatus = RecyclerActiveStatus.Offline;
            profile.BusyReason = Normalize(request.BusyReason);
        }

        await _db.SaveChangesAsync(ct);
        _logger.LogInformation("Recycler {RecyclerId} chuyển trạng thái hoạt động: {Status}", recyclerId, profile.ActiveStatus);

        return MapAvailability(profile, activeCount);
    }

    public async Task<AvailabilityDto> UpdateScheduleAsync(Guid recyclerId, UpdateScheduleRequest request, CancellationToken ct = default)
    {
        var profile = await LoadProfileAsync(recyclerId, ct);
        EnsureVerifiedAndOperable(profile);

        var days = request.Days;
        if (days.Select(d => d.DayOfWeek).Distinct().Count() != days.Count)
        {
            throw new BusinessRuleException("BR-MTH-03", "MSG-GEN-01", "Mỗi ngày trong tuần chỉ được khai báo một lần.");
        }

        var normalized = new List<ScheduleDayDto>();
        foreach (var d in days.OrderBy(x => (x.DayOfWeek + 6) % 7)) // Thứ hai đứng đầu, Chủ nhật cuối
        {
            if (d.IsClosed)
            {
                normalized.Add(new ScheduleDayDto { DayOfWeek = d.DayOfWeek, IsClosed = true });
                continue;
            }

            if (!TimeOnly.TryParseExact(d.OpenTime, "HH:mm", CultureInfo.InvariantCulture, DateTimeStyles.None, out var open)
                || !TimeOnly.TryParseExact(d.CloseTime, "HH:mm", CultureInfo.InvariantCulture, DateTimeStyles.None, out var close))
            {
                throw new BusinessRuleException("BR-MTH-03", "MSG-GEN-01",
                    $"Giờ mở/đóng cửa của {DayLabels[d.DayOfWeek]} phải có dạng HH:mm (ví dụ 08:00).");
            }

            if (close <= open)
            {
                throw new BusinessRuleException("BR-MTH-03", "MSG-GEN-01",
                    $"Giờ đóng cửa của {DayLabels[d.DayOfWeek]} phải sau giờ mở cửa.");
            }

            normalized.Add(new ScheduleDayDto
            {
                DayOfWeek = d.DayOfWeek,
                IsClosed = false,
                OpenTime = open.ToString("HH:mm", CultureInfo.InvariantCulture),
                CloseTime = close.ToString("HH:mm", CultureInfo.InvariantCulture)
            });
        }

        profile.WeeklyScheduleJson = JsonSerializer.Serialize(normalized, JsonOptions);
        profile.OperatingHours = BuildOperatingHoursSummary(normalized);

        await _db.SaveChangesAsync(ct);
        return MapAvailability(profile, await CountActivePricesAsync(recyclerId, ct));
    }

    // ============================================================
    // Helpers
    // ============================================================
    private async Task<RecyclerProfile> LoadProfileAsync(Guid recyclerId, CancellationToken ct)
    {
        var profile = await _db.RecyclerProfiles
            .Include(r => r.User)
            .FirstOrDefaultAsync(r => r.UserId == recyclerId, ct);

        return profile ?? throw new NotFoundException("Không tìm thấy hồ sơ cơ sở thu gom của tài khoản này.");
    }

    private Task<int> CountActivePricesAsync(Guid recyclerId, CancellationToken ct) =>
        _db.RecyclerPrices.CountAsync(p => p.RecyclerId == recyclerId && p.Status == PriceStatus.Active, ct);

    private static void EnsureNotSuspended(RecyclerProfile profile)
    {
        if (profile.User != null && profile.User.Status == UserStatus.Suspended)
        {
            throw new BusinessRuleException("BR-AUTH-05", "MSG-AUTH-04",
                "Tài khoản đã bị đình chỉ hoặc vô hiệu hóa. Vui lòng liên hệ bộ phận hỗ trợ.");
        }
    }

    /// <summary>BR-MTH-01: chỉ Recycler đã được Staff duyệt mới được thao tác giá / bật hoạt động.</summary>
    private static void EnsureVerifiedAndOperable(RecyclerProfile profile)
    {
        EnsureNotSuspended(profile);

        if (profile.VerificationStatus == VerificationStatus.Rejected)
        {
            throw new BusinessRuleException("BR-MTH-01", "MSG-MTH-05",
                $"Hồ sơ cơ sở chưa được phê duyệt: {profile.RejectionReason}. Vui lòng cập nhật hồ sơ và gửi lại.");
        }

        if (profile.VerificationStatus != VerificationStatus.Verified)
        {
            throw new BusinessRuleException("BR-MTH-01", "MSG-MTH-03",
                "Hồ sơ cơ sở đang chờ Staff phê duyệt. Bạn chỉ có thể thao tác sau khi được duyệt.");
        }
    }

    private static void EnsurePurchasable(WasteCategory category)
    {
        if (!string.Equals(category.Status, "Active", StringComparison.OrdinalIgnoreCase))
        {
            throw new BusinessRuleException("BR-SRT-04", "MSG-SRT-04",
                $"Danh mục phế liệu '{category.Name}' hiện đang tạm ngưng trên hệ thống.");
        }

        if (!category.IsRecyclable || string.Equals(category.Code, NonRecyclableCode, StringComparison.OrdinalIgnoreCase))
        {
            throw new BusinessRuleException("BR-MTH-03", "MSG-GEN-01",
                "Danh mục rác này chỉ hỗ trợ hướng dẫn xử lý, không thuộc nhóm phế liệu nhận thu mua.");
        }
    }

    private static void ValidatePriceValues(decimal price, decimal minQuantityKg, string? conditions)
    {
        if (price <= 0 || price != decimal.Truncate(price))
        {
            throw new BusinessRuleException("BR-MTH-03", "MSG-GEN-01", "Giá thu mua phải là số nguyên dương (VND/kg).");
        }

        if (minQuantityKg <= 0)
        {
            throw new BusinessRuleException("BR-MTH-03", "MSG-GEN-01", "Khối lượng tối thiểu phải lớn hơn 0.");
        }

        if (conditions != null && conditions.Trim().Length > 200)
        {
            throw new BusinessRuleException("BR-MTH-03", "MSG-GEN-01", "Điều kiện thu gom tối đa 200 ký tự.");
        }
    }

    private static BusinessRuleException ProfileIncomplete() =>
        new("BR-MTH-03", "MSG-MTH-06",
            "Vui lòng thêm ít nhất một loại phế liệu nhận mua và một vị trí hợp lệ trước khi hoạt động.");

    private static string? Normalize(string? value) =>
        string.IsNullOrWhiteSpace(value) ? null : value.Trim();

    private static string BuildOperatingHoursSummary(List<ScheduleDayDto> days)
    {
        var openDays = days.Where(d => !d.IsClosed).ToList(); // đã sắp xếp Thứ hai -> Chủ nhật
        if (openDays.Count == 0) return "Đóng cửa cả tuần";

        var parts = openDays
            .GroupBy(d => $"{d.OpenTime} - {d.CloseTime}")
            .Select(g => $"{FormatDays(g.Select(d => d.DayOfWeek).ToList())}: {g.Key}");

        var summary = string.Join("; ", parts);

        // Cột operating_hours chỉ dài 100 ký tự; lịch chi tiết vẫn nằm đầy đủ trong weekly_schedule
        return summary.Length <= OperatingHoursMaxLength
            ? summary
            : summary[..(OperatingHoursMaxLength - 3)] + "...";
    }

    /// <summary>Gộp các ngày liên tiếp (từ 3 ngày trở lên) thành khoảng: [1..5] -> "T2-T6", [1,3] -> "T2, T4".</summary>
    private static string FormatDays(List<int> daysOfWeek)
    {
        var order = daysOfWeek.Select(d => (d + 6) % 7).OrderBy(x => x).ToList(); // 0 = Thứ hai ... 6 = Chủ nhật
        var parts = new List<string>();

        var i = 0;
        while (i < order.Count)
        {
            var j = i;
            while (j + 1 < order.Count && order[j + 1] == order[j] + 1) j++;

            if (j - i >= 2)
            {
                parts.Add($"{DayLabels[(order[i] + 1) % 7]}-{DayLabels[(order[j] + 1) % 7]}");
            }
            else
            {
                for (var k = i; k <= j; k++) parts.Add(DayLabels[(order[k] + 1) % 7]);
            }

            i = j + 1;
        }

        return string.Join(", ", parts);
    }

    private static List<ScheduleDayDto> ParseSchedule(string? json)
    {
        if (string.IsNullOrWhiteSpace(json)) return new List<ScheduleDayDto>();
        try
        {
            return JsonSerializer.Deserialize<List<ScheduleDayDto>>(json, JsonOptions) ?? new List<ScheduleDayDto>();
        }
        catch (JsonException)
        {
            return new List<ScheduleDayDto>();
        }
    }

    private static RecyclerProfileDto MapProfile(RecyclerProfile p, int activePriceCount) => new()
    {
        RecyclerId = p.UserId,
        FacilityName = p.FacilityName,
        RepresentativeName = p.RepresentativeName,
        BusinessAddress = p.BusinessAddress,
        Latitude = (double)p.Latitude,
        Longitude = (double)p.Longitude,
        OperatingHours = p.OperatingHours,
        ContactPhone = p.ContactPhone,
        Description = p.Description,
        LegalPermitUrl = p.LegalPermitUrl,
        BusinessPhotoUrl = p.BusinessPhotoUrl,
        VerificationStatus = p.VerificationStatus.ToString(),
        ActiveStatus = p.ActiveStatus.ToString(),
        RejectionReason = p.RejectionReason,
        BusyReason = p.BusyReason,
        ActivePriceCount = activePriceCount
    };

    private static AvailabilityDto MapAvailability(RecyclerProfile p, int activePriceCount) => new()
    {
        IsOnline = p.ActiveStatus == RecyclerActiveStatus.Online,
        ActiveStatus = p.ActiveStatus.ToString(),
        VerificationStatus = p.VerificationStatus.ToString(),
        BusyReason = p.BusyReason,
        OperatingHours = p.OperatingHours,
        WeeklySchedule = ParseSchedule(p.WeeklyScheduleJson),
        ActivePriceCount = activePriceCount
    };

    private static RecyclerPriceDto MapPrice(RecyclerPrice p)
    {
        var lastUpdated = p.UpdatedAt;
        return new RecyclerPriceDto
        {
            Id = p.Id,
            WasteCategoryId = p.WasteCategoryId,
            WasteCategoryCode = p.WasteCategory?.Code ?? string.Empty,
            WasteCategoryName = p.WasteCategory?.Name ?? string.Empty,
            PricePerKg = p.PricePerKg,
            MinQuantityKg = p.MinQuantityKg,
            CollectionConditions = p.CollectionConditions,
            IsActive = p.Status == PriceStatus.Active,
            EffectiveFrom = p.EffectiveFrom,
            EffectiveTo = p.EffectiveTo,
            LastUpdatedAt = lastUpdated,
            IsOutdated = (DateTime.UtcNow - lastUpdated).TotalDays > PriceStaleDays
        };
    }
}
