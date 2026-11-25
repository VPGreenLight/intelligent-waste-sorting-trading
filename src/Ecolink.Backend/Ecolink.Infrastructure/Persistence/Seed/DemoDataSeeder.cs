using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using Ecolink.Application.Interfaces;
using Ecolink.Domain.Constants;
using Ecolink.Domain.Entities;
using Ecolink.Domain.Enums;
using Ecolink.Infrastructure.Data;
using Ecolink.Infrastructure.Services;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace Ecolink.Infrastructure.Persistence.Seed;

public sealed record DemoSeedReport(
    bool ResetExecuted,
    int WasteCategories,
    int SortingRules,
    int Users,
    int RecyclerProfiles,
    int CollectionPartners,
    int RecyclerPrices,
    int HandoverRequests,
    int HandoverReceipts,
    int TradeOrders,
    int ChatMessages,
    int TransactionReviews,
    int TransactionComplaints,
    int BlogPosts,
    int PlatformPolicies,
    int EcoFacts,
    int Gifts,
    int GiftRedemptions,
    int LeaderboardCycles,
    int PointTransactions,
    int AiScanHistories,
    int AiFeedbackLogs,
    int AiDatasetItems,
    int AiDatasetVersions,
    int ServicePackages,
    int ServiceOrders,
    int UserSubscriptions,
    int PaymentWebhookLogs,
    int StaffAttendances,
    int AccountModerationLogs,
    int AuditLogs,
    int Notifications)
{
    public string ToSummaryString() =>
        $"""
        === EcoLink Council Demo Seeder Report (Reset={ResetExecuted}) ===
        WasteCategories={WasteCategories}, SortingRules={SortingRules}, Policies={PlatformPolicies}
        Users={Users}, RecyclerProfiles={RecyclerProfiles}, CollectionPartners={CollectionPartners}, RecyclerPrices={RecyclerPrices}
        HandoverRequests={HandoverRequests}, HandoverReceipts={HandoverReceipts}, TradeOrders={TradeOrders}, ChatMessages={ChatMessages}
        TransactionReviews={TransactionReviews}, TransactionComplaints={TransactionComplaints}, BlogPosts={BlogPosts}
        EcoFacts={EcoFacts}, Gifts={Gifts}, GiftRedemptions={GiftRedemptions}, LeaderboardCycles={LeaderboardCycles}, PointTransactions={PointTransactions}
        AiScanHistories={AiScanHistories}, AiFeedbackLogs={AiFeedbackLogs}, AiDatasetItems={AiDatasetItems}, AiDatasetVersions={AiDatasetVersions}
        ServicePackages={ServicePackages}, ServiceOrders={ServiceOrders}, UserSubscriptions={UserSubscriptions}, PaymentWebhookLogs={PaymentWebhookLogs}
        StaffAttendances={StaffAttendances}, AccountModerationLogs={AccountModerationLogs}, AuditLogs={AuditLogs}, Notifications={Notifications}
        """;
}

/// <summary>
/// Seeder dữ liệu chuẩn Hội đồng cho môi trường Demo EcoLink (Hòa Lạc / Thạch Thất / Hà Nội).
/// Không chạy mặc định khi WebApi khởi động; được kích hoạt qua CLI `--seed-demo` hoặc `--reset-and-seed-demo`.
/// </summary>
public sealed class DemoDataSeeder
{
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);

    // Tài khoản demo cố định đồng bộ với AuthService & Frontend quick-login
    private static readonly Guid CitizenId = Guid.Parse("11111111-1111-1111-1111-111111111111");
    private static readonly Guid CitizenVipId = Guid.Parse("11111112-1111-1111-1111-111111111112");
    private static readonly Guid RecyclerId = Guid.Parse("22222222-2222-2222-2222-222222222222");
    private static readonly Guid RecyclerProId = Guid.Parse("22222223-2222-2222-2222-222222222223");
    private static readonly Guid AdminId = Guid.Parse("33333333-3333-3333-3333-333333333333");
    private static readonly Guid StaffId = Guid.Parse("88888888-8888-8888-8888-888888888888");
    private static readonly Guid StaffSupportId = Guid.Parse("88888889-8888-8888-8888-888888888889");

    // Các cơ sở thu gom mẫu từ GeoMatchingService (giữ cố định ID & tọa độ phục vụ test DSC01 / UTC012)
    private static readonly Guid RecyclerHnId = Guid.Parse("77777777-7777-7777-7777-777777777777");
    private static readonly Guid RecyclerHl1Id = Guid.Parse("99999991-9999-9999-9999-999999999991");
    private static readonly Guid RecyclerHl2Id = Guid.Parse("99999992-9999-9999-9999-999999999992");
    private static readonly Guid RecyclerHl3Id = Guid.Parse("99999993-9999-9999-9999-999999999993");
    private static readonly Guid RecyclerHl4Id = Guid.Parse("99999994-9999-9999-9999-999999999994");

    private const string DemoWastePhotoJson = "[\"https://images.unsplash.com/photo-1532996122724-e3c354a0b15b?auto=format&fit=crop&w=900&q=80\"]";
    private const string DemoScalePhotoJson = "[\"https://images.unsplash.com/photo-1604187351574-c75ca79f5807?auto=format&fit=crop&w=900&q=80\"]";

    private readonly ApplicationDbContext _db;
    private readonly IAuthService _authService;
    private readonly IWasteRuleEngineService _wasteRuleEngine;
    private readonly IPlatformPolicyService _platformPolicyService;
    private readonly IGeoMatchingService _geoMatchingService;
    private readonly IRecyclerOnboardingService _onboardingService;
    private readonly IBlogPostService _blogPostService;
    private readonly IGamificationConfigService _gamificationConfigService;
    private readonly ILogger<DemoDataSeeder> _logger;

    public DemoDataSeeder(
        ApplicationDbContext db,
        IAuthService authService,
        IWasteRuleEngineService wasteRuleEngine,
        IPlatformPolicyService platformPolicyService,
        IGeoMatchingService geoMatchingService,
        IRecyclerOnboardingService onboardingService,
        IBlogPostService blogPostService,
        IGamificationConfigService gamificationConfigService,
        ILogger<DemoDataSeeder> logger)
    {
        _db = db;
        _authService = authService;
        _wasteRuleEngine = wasteRuleEngine;
        _platformPolicyService = platformPolicyService;
        _geoMatchingService = geoMatchingService;
        _onboardingService = onboardingService;
        _blogPostService = blogPostService;
        _gamificationConfigService = gamificationConfigService;
        _logger = logger;
    }

    public async Task<DemoSeedReport> SeedAsync(bool resetExisting = false, CancellationToken ct = default)
    {
        var utcNow = DateTime.UtcNow;
        var todayVn = DateOnly.FromDateTime(utcNow.AddHours(7));

        if (_db.Database.IsRelational())
        {
            await _db.Database.MigrateAsync(ct);
        }

        if (resetExisting && _db.Database.IsRelational())
        {
            _logger.LogWarning("Đang dọn sạch toàn bộ bảng dữ liệu trong PostgreSQL để khởi tạo bộ Demo Hội đồng...");
            await TruncateAllTablesAsync(ct);
            _db.ChangeTracker.Clear();
        }

        // 1. Danh mục rác, Quy tắc phân loại, Điều khoản, Gói dịch vụ & Cấu hình Gamification chuẩn
        if (_wasteRuleEngine is WasteRuleEngineService ruleEngine)
        {
            await ruleEngine.EnsureDatabaseDefaultsSeededAsync(ct);
        }

        if (_platformPolicyService is PlatformPolicyService policyService)
        {
            await policyService.EnsureInitializedAsync(ct);
        }

        await ServicePackageHelpers.EnsureCanonicalPackagesExistAsync(_db, ct);
        await _gamificationConfigService.GetSettingsAsync(ct);
        await EnsurePointRateRulesAsync(ct);

        // 2. Người dùng demo cốt lõi, Cơ sở thu gom Hòa Lạc, Ứng viên KYB & Blog
        if (_authService is AuthService authSvc)
        {
            await authSvc.EnsureDemoUsersAsync(ct, includeExtendedAccounts: true);
        }

        if (_geoMatchingService is GeoMatchingService geoSvc)
        {
            await geoSvc.EnsureDemoRecyclerPricesSeededAsync(ct, force: true);
        }

        if (_onboardingService is RecyclerOnboardingService onboardingSvc)
        {
            await onboardingSvc.EnsureSamplePendingApplicationsAsync(ct, force: true);
        }

        if (_blogPostService is BlogPostService blogSvc)
        {
            await blogSvc.SeedDefaultBlogPostsCoreAsync(ct);
        }

        // 3. Mở rộng cộng đồng Citizen Hòa Lạc, Partner & Bảng giá cho recycler_pro
        var extraCitizens = await EnsureExtraCitizensAsync(utcNow, ct);
        await EnsureRecyclerProPartnerAndPricesAsync(utcNow, ct);

        // 4. Gamification: Chu kỳ BXH, Ví điểm, Cây ảo, Điểm danh, Eco Facts, Quà tặng & Đổi quà
        var activeCycle = await EnsureLeaderboardCyclesAsync(utcNow, ct);
        await EnsureEcoFactsAndGiftsAsync(utcNow, ct);
        await EnsureGamificationWalletsTreesAndCheckinsAsync(utcNow, todayVn, activeCycle, extraCitizens, ct);

        // 5. Đơn bàn giao, Biên nhận, Đơn thương lượng Chat, Đánh giá (52 lượt cho recycler_pro) & Khiếu nại
        await EnsureHandoversTradeChatsReviewsAndComplaintsAsync(utcNow, extraCitizens, ct);

        // 6. AI Scan Histories, Feedback Logs & Dataset Curation Version v2.0.0
        await EnsureAiScanFeedbackAndDatasetAsync(utcNow, ct);

        // 7. Gói dịch vụ, Thuê bao, VietQR Orders & Payment Webhook Logs (cẩn trọng DSC01 & UTC012)
        await EnsureServiceOrdersSubscriptionsAndWebhooksAsync(utcNow, ct);

        // 8. Quản trị: Chấm công Staff, Nhật ký kiểm duyệt, Audit Logs & Thông báo
        await EnsureGovernanceAttendancesModerationAuditAndNotificationsAsync(utcNow, todayVn, extraCitizens, ct);

        // 9. Đồng bộ PostgreSQL sequences cho mã đơn & mã biên nhận
        await AdvancePostgresSequencesAsync(ct);

        return await BuildReportAsync(resetExisting, ct);
    }

    private async Task TruncateAllTablesAsync(CancellationToken ct)
    {
        await _db.Database.ExecuteSqlRawAsync(@"
            TRUNCATE TABLE
                ai_dataset_version_items,
                ai_dataset_versions,
                ai_dataset_items,
                ai_feedback_logs,
                ai_scan_histories,
                transaction_complaints,
                transaction_reviews,
                chat_messages,
                transaction_receipts,
                trade_orders,
                handover_receipts,
                handover_request_items,
                handover_requests,
                accepted_materials,
                collection_partners,
                recycler_price_list,
                recycler_profiles,
                staff_attendances,
                staff_permission_assignments,
                staff_profiles,
                payment_webhook_logs,
                user_subscriptions,
                service_orders,
                service_packages,
                gift_redemptions,
                gift_inventory,
                eco_facts,
                daily_checkin_logs,
                point_transactions,
                virtual_tree_progress,
                user_point_wallets,
                user_eco_profiles,
                leaderboard_cycles,
                gamification_config,
                point_rate_rules,
                tree_level_thresholds,
                sorting_guidance_rules,
                waste_categories,
                blog_posts,
                platform_policy_sections,
                notifications,
                account_moderation_logs,
                audit_logs,
                refresh_tokens,
                password_reset_tokens,
                users
            RESTART IDENTITY CASCADE;", ct);
    }

    private async Task EnsurePointRateRulesAsync(CancellationToken ct)
    {
        if (await _db.PointRateRules.AsNoTracking().AnyAsync(ct))
        {
            return;
        }

        var defaultRates = new (string Code, long PointsPerKg)[]
        {
            ("plastic", 12),
            ("paper", 8),
            ("glass", 10),
            ("metal", 15),
            ("cardboard", 10),
            ("trash", 2)
        };

        foreach (var (code, pts) in defaultRates)
        {
            _db.PointRateRules.Add(new PointRateRule
            {
                Id = DeterministicGuid($"point_rate_{code}"),
                WasteCategoryCode = code,
                PointsPerKg = pts
            });
        }

        await _db.SaveChangesAsync(ct);
    }

    private async Task<List<User>> EnsureExtraCitizensAsync(DateTime utcNow, CancellationToken ct)
    {
        var specs = new (string Key, string Username, string FullName, string Phone, int Points, decimal Kg, int Level)[]
        {
            ("hl1", "citizen_hl1", "Nguyễn Minh Anh (KTX Hòa Lạc)", "0911001101", 640, 48.0m, 3),
            ("hl2", "citizen_hl2", "Trần Quốc Bảo (Thạch Hòa)", "0911001102", 510, 39.5m, 3),
            ("hl3", "citizen_hl3", "Lê Thảo Nhi (Tân Xã)", "0911001103", 430, 34.0m, 3),
            ("hl4", "citizen_hl4", "Phạm Gia Huy (Đại học FPT)", "0911001104", 320, 26.0m, 2),
            ("hl5", "citizen_hl5", "Vũ Khánh Linh (Khu CNC Hòa Lạc)", "0911001105", 210, 17.5m, 2),
            ("hl6", "citizen_hl6", "Hoàng Đức Mạnh (Bình Yên)", "0911001106", 120, 11.0m, 2),
            ("hl7", "citizen_hl7", "Đỗ Hải Yến (Phú Cát)", "0911001107", 85, 7.5m, 1),
            ("hl8", "citizen_hl8", "Bùi Tiến Dũng (Thạch Thất)", "0911001108", 60, 5.0m, 1)
        };

        var passwordHash = BCrypt.Net.BCrypt.HashPassword("citizen123");
        var existing = await _db.Users.IgnoreQueryFilters()
            .Where(u => specs.Select(s => s.Username).Contains(u.Username))
            .ToDictionaryAsync(u => u.Username, StringComparer.OrdinalIgnoreCase, ct);

        var result = new List<User>(specs.Length);
        var added = false;

        foreach (var spec in specs)
        {
            if (existing.TryGetValue(spec.Username, out var found))
            {
                result.Add(found);
                continue;
            }

            var userId = DeterministicGuid($"demo_user_{spec.Key}");
            var user = new User
            {
                Id = userId,
                Username = spec.Username,
                Email = $"{spec.Username}@ecolink.vn",
                PasswordHash = passwordHash,
                FullName = spec.FullName,
                PhoneNumber = spec.Phone,
                Role = UserRole.Citizen,
                Status = UserStatus.Active,
                WarningCount = spec.Username == "citizen_hl8" ? 1 : 0,
                ModerationReason = spec.Username == "citizen_hl8" ? "Hẹn lịch thu gom nhưng vắng mặt không báo trước." : null,
                ModeratedAt = spec.Username == "citizen_hl8" ? utcNow.AddDays(-6) : null,
                ModeratedBy = spec.Username == "citizen_hl8" ? StaffId : null,
                EcoProfile = new UserEcoProfile
                {
                    Id = DeterministicGuid($"eco_profile_{spec.Key}"),
                    UserId = userId,
                    CurrentGreenPoints = spec.Points,
                    TotalLifetimePoints = spec.Points + 80,
                    TreeLevel = spec.Level,
                    TreeExperience = spec.Points,
                    TotalWasteScanned = 5,
                    TotalRecycledKg = spec.Kg
                }
            };

            _db.Users.Add(user);
            result.Add(user);
            added = true;
        }

        if (added)
        {
            await _db.SaveChangesAsync(ct);
        }

        return result;
    }

    private async Task EnsureRecyclerProPartnerAndPricesAsync(DateTime utcNow, CancellationToken ct)
    {
        var profile = await _db.RecyclerProfiles.FirstOrDefaultAsync(r => r.UserId == RecyclerProId, ct);
        if (profile != null)
        {
            profile.VerificationStatus = VerificationStatus.Verified;
            profile.ActiveStatus = RecyclerActiveStatus.Online;
            profile.TotalReviews = 52;
            profile.AverageRating = 4.81m;
            profile.UpdatedAt = utcNow;

            var categories = await _db.WasteCategories
                .Where(c => c.Status == "Active" && c.IsRecyclable)
                .ToListAsync(ct);

            var defaultPrices = new Dictionary<string, (decimal Price, decimal MinKg, string Cond)>(StringComparer.OrdinalIgnoreCase)
            {
                ["plastic"] = (9_000m, 2.0m, "Chai nhựa PET, can HDPE sạch ép gọn"),
                ["paper"] = (4_800m, 3.0m, "Giấy văn phòng, sách báo khô ráo"),
                ["glass"] = (2_500m, 5.0m, "Chai thủy tinh nguyên vẹn, đóng thùng cẩn thận"),
                ["metal"] = (28_000m, 1.5m, "Lon nhôm, đồng vụn, sắt phế liệu"),
                ["cardboard"] = (4_500m, 3.0m, "Thùng carton gập phẳng, không ẩm mốc")
            };

            var existingProPrices = await _db.RecyclerPrices
                .Where(p => p.RecyclerId == RecyclerProId)
                .Select(p => p.WasteCategoryId)
                .ToListAsync(ct);

            foreach (var cat in categories)
            {
                if (defaultPrices.TryGetValue(cat.Code, out var spec) && !existingProPrices.Contains(cat.Id))
                {
                    _db.RecyclerPrices.Add(new RecyclerPrice
                    {
                        Id = DeterministicGuid($"price_pro_{cat.Code}"),
                        RecyclerId = RecyclerProId,
                        WasteCategoryId = cat.Id,
                        PricePerKg = spec.Price,
                        MinQuantityKg = spec.MinKg,
                        CollectionConditions = spec.Cond,
                        Status = PriceStatus.Active,
                        EffectiveFrom = utcNow.AddDays(-30),
                        UpdatedAt = utcNow.AddDays(-2)
                    });
                }
            }

            await _db.SaveChangesAsync(ct);
        }

        // Đồng bộ CollectionPartner & AcceptedMaterials cho TẤT CẢ cơ sở Verified (phục vụ TradeOrder FK & Legacy API)
        var verifiedProfiles = await _db.RecyclerProfiles
            .AsNoTracking()
            .Where(r => r.VerificationStatus == VerificationStatus.Verified)
            .ToListAsync(ct);

        var partnerByUserId = await _db.CollectionPartners
            .ToDictionaryAsync(p => p.UserId, ct);

        foreach (var rp in verifiedProfiles)
        {
            if (!partnerByUserId.TryGetValue(rp.UserId, out var partner))
            {
                partner = new CollectionPartner
                {
                    Id = rp.UserId,
                    UserId = rp.UserId,
                    FacilityName = rp.FacilityName,
                    Address = rp.BusinessAddress,
                    Latitude = (double)rp.Latitude,
                    Longitude = (double)rp.Longitude,
                    OperatingHours = rp.OperatingHours,
                    ContactPhone = rp.ContactPhone,
                    IsVerified = true
                };
                _db.CollectionPartners.Add(partner);
                partnerByUserId[rp.UserId] = partner;
            }
            else
            {
                partner.FacilityName = rp.FacilityName;
                partner.Address = rp.BusinessAddress;
                partner.Latitude = (double)rp.Latitude;
                partner.Longitude = (double)rp.Longitude;
                partner.IsVerified = true;
            }
        }

        await _db.SaveChangesAsync(ct);

        var activePrices = await _db.RecyclerPrices
            .AsNoTracking()
            .Where(p => p.Status == PriceStatus.Active)
            .ToListAsync(ct);

        var existingMaterialPairs = (await _db.AcceptedMaterials
                .AsNoTracking()
                .Select(m => new { m.PartnerId, m.WasteCategoryId })
                .ToListAsync(ct))
            .Select(m => (m.PartnerId, m.WasteCategoryId))
            .ToHashSet();

        foreach (var price in activePrices)
        {
            if (!partnerByUserId.TryGetValue(price.RecyclerId, out var partner))
            {
                continue;
            }

            if (existingMaterialPairs.Add((partner.Id, price.WasteCategoryId)))
            {
                _db.AcceptedMaterials.Add(new AcceptedMaterial
                {
                    Id = DeterministicGuid($"mat_{partner.Id}_{price.WasteCategoryId}"),
                    PartnerId = partner.Id,
                    WasteCategoryId = price.WasteCategoryId,
                    PricePerKg = price.PricePerKg,
                    MinimumQuantityKg = price.MinQuantityKg
                });
            }
        }

        await _db.SaveChangesAsync(ct);
    }

    private async Task<LeaderboardCycle> EnsureLeaderboardCyclesAsync(DateTime utcNow, CancellationToken ct)
    {
        var activeCycle = await _db.LeaderboardCycles.FirstOrDefaultAsync(c => c.IsActive, ct);
        if (activeCycle == null)
        {
            var finalizedId = DeterministicGuid("leaderboard_cycle_prev");
            if (!await _db.LeaderboardCycles.AnyAsync(c => c.Id == finalizedId, ct))
            {
                _db.LeaderboardCycles.Add(new LeaderboardCycle
                {
                    Id = finalizedId,
                    Name = "Bảng Vàng Sống Xanh Hòa Lạc - Tháng trước",
                    PeriodType = LeaderboardPeriodType.Monthly,
                    StartsAt = utcNow.AddDays(-45),
                    EndsAt = utcNow.AddDays(-16),
                    IsActive = false,
                    Status = LeaderboardCycleStatus.Finalized,
                    Top1BonusPoints = 500,
                    Top2BonusPoints = 300,
                    Top3BonusPoints = 150,
                    WinnersSnapshotJson = JsonSerializer.Serialize(new[]
                    {
                        new { rank = 1, userId = CitizenVipId, fullName = "Trần Minh Anh (VIP)", points = 850, bonus = 500 }
                    }, JsonOptions),
                    CreatedByAdminId = AdminId,
                    FinalizedByAdminId = AdminId,
                    FinalizedAt = utcNow.AddDays(-15),
                    CreatedAt = utcNow.AddDays(-45),
                    UpdatedAt = utcNow.AddDays(-15)
                });
            }

            activeCycle = new LeaderboardCycle
            {
                Id = DeterministicGuid("leaderboard_cycle_active"),
                Name = "Đua Top Phân Loại Xanh - Khu CNC Hòa Lạc",
                PeriodType = LeaderboardPeriodType.Monthly,
                StartsAt = utcNow.AddDays(-15),
                EndsAt = utcNow.AddDays(15),
                IsActive = true,
                Status = LeaderboardCycleStatus.Active,
                Top1BonusPoints = 500,
                Top2BonusPoints = 300,
                Top3BonusPoints = 150,
                CreatedByAdminId = AdminId,
                CreatedAt = utcNow.AddDays(-15),
                UpdatedAt = utcNow
            };
            _db.LeaderboardCycles.Add(activeCycle);
            await _db.SaveChangesAsync(ct);
        }

        return activeCycle;
    }

    private async Task EnsureEcoFactsAndGiftsAsync(DateTime utcNow, CancellationToken ct)
    {
        if (!await _db.EcoFacts.AsNoTracking().AnyAsync(ct))
        {
            var facts = new (int Level, string Title, string Content)[]
            {
                (1, "Tái chế 1 lon nhôm tiết kiệm 95% năng lượng", "Sản xuất lon nhôm mới từ phế liệu tái chế tiêu tốn ít hơn tới 95% điện năng so với luyện nhôm từ quặng bauxite nguyên khai."),
                (1, "Chai nhựa PET có thể hóa thành sợi áo thể thao", "Khoảng 10 chai nhựa PET sạch sau khi nghiền vụn và kéo sợi đủ dệt một chiếc áo thun thể thao thoáng khí."),
                (1, "Giấy ướt và hộp pizza dính dầu không tái chế được", "Sợi giấy bị thấm dầu mỡ hoặc hóa chất tẩy rửa sẽ làm hỏng cả mẻ bột giấy tái chế, nên hãy bỏ vào thùng rác thông thường."),
                (2, "Thủy tinh có thể tái chế vô hạn lần", "Không giống nhựa bị suy giảm mạch polymer, thủy tinh sạch có thể nấu chảy tái chế 100% vô số lần mà vẫn giữ nguyên độ tinh khiết."),
                (2, "Một viên pin rò rỉ làm ô nhiễm 500 lít nước", "Pin tiểu chứa kim loại nặng như thủy ngân, chì, cadmium. Luôn dán băng keo 2 cực pin cũ và mang đến điểm thu hồi rác nguy hại."),
                (2, "1 tấn giấy tái chế cứu sống 17 cây xanh trưởng thành", "Thu gom báo cũ và thùng carton khô sạch còn giúp tiết kiệm hơn 26.000 lít nước sạch trong quá trình sản xuất giấy."),
                (3, "Hạn chế rửa quá kỹ bằng xà phòng trước khi cân ký", "Chỉ cần tráng sơ cặn nước ngọt hoặc đổ hết thức ăn thừa và phơi ráo để phế liệu không bốc mùi trước khi giao cho vựa."),
                (3, "Phân loại tại nguồn tăng giá trị phế liệu thêm 30%", "Khi tách riêng nhựa PET, giấy carton và kim loại sạch, cơ sở thu gom giảm công phân loại thủ công và trả giá cao hơn."),
                (4, "Phân compost từ rác hữu cơ giảm khí Metan bãi rác", "Rác thực phẩm bị chôn lấp yếm khí tạo khí CH4 gây hiệu ứng nhà kính gấp 28 lần CO2. Ủ phân hữu cơ giúp đất màu mỡ hơn."),
                (4, "Kinh tế tuần hoàn tại khu đô thị đại học Hòa Lạc", "Mỗi lượt bàn giao phế liệu trên EcoLink giúp giảm trung bình 2.4 kg phát thải CO2 tương đương thông qua tái sinh vật liệu.")
            };

            for (var i = 0; i < facts.Length; i++)
            {
                var f = facts[i];
                _db.EcoFacts.Add(new EcoFact
                {
                    Id = DeterministicGuid($"ecofact_{i + 1}"),
                    Title = f.Title,
                    Content = f.Content,
                    ImageUrl = "/assets/rewards/seedling.jpg",
                    SourceUrl = "https://ecolink.vn/blog",
                    UnlockTreeLevel = f.Level,
                    Status = "ACTIVE",
                    CreatedByStaffId = StaffId,
                    CreatedAt = utcNow.AddDays(-(10 - i)),
                    UpdatedAt = utcNow.AddDays(-(10 - i))
                });
            }

            await _db.SaveChangesAsync(ct);
        }

        if (!await _db.Gifts.AsNoTracking().AnyAsync(ct))
        {
            var gifts = new (string Key, string Name, GiftType Type, long Points, int Stock, string ImageUrl, string Desc)[]
            {
                ("kit", "Bộ Kit Ươm Mầm Xanh Để Bàn", GiftType.Physical, 120, 25, "/assets/rewards/kit.jpg", "Chậu gốm mini kèm viên nén xơ dừa và hạt giống hướng dương xanh."),
                ("bag", "Túi Vải Canvas EcoLink Sống Xanh", GiftType.Physical, 180, 40, "/assets/rewards/bag.jpg", "Túi canvas dày dặn thay thế túi ni-lông dùng một lần khi đi siêu thị."),
                ("glasses", "Bộ 2 Ly Thủy Tinh Tái Chế Thủ Công", GiftType.Physical, 320, 18, "/assets/rewards/glasses.jpg", "Ly thủy tinh chế tác từ chai tái chế, dày và an toàn cho đồ uống lạnh."),
                ("compactor", "Dụng Cụ Ép Lon & Chai Nhựa Mini", GiftType.Physical, 480, 12, "/assets/rewards/compactor.jpg", "Giúp nén gọn chai PET và vỏ lon nhôm tiết kiệm 70% không gian lưu trữ."),
                ("seedling", "Gói Gieo Trồng Cây Bóng Mát Hòa Lạc", GiftType.Physical, 250, 30, "/assets/rewards/seedling.jpg", "Đóng góp 1 cây xanh thực tế mang tên bạn tại dự án phủ xanh khu CNC Hòa Lạc."),
                ("voucher10", "Voucher Giảm 10% Mua Sắm Đối Tác Xanh", GiftType.Voucher, 80, 100, "/assets/rewards/coupon10.jpg", "Mã giảm giá điện tử 10% áp dụng tại hệ thống cửa hàng tiêu dùng bền vững.")
            };

            foreach (var g in gifts)
            {
                _db.Gifts.Add(new Gift
                {
                    Id = DeterministicGuid($"gift_{g.Key}"),
                    Name = g.Name,
                    Type = g.Type,
                    Description = g.Desc,
                    ImageUrl = g.ImageUrl,
                    PointsRequired = g.Points,
                    StockQuantity = g.Stock,
                    Status = "ACTIVE",
                    CreatedAt = utcNow.AddDays(-20),
                    UpdatedAt = utcNow.AddDays(-5)
                });
            }

            await _db.SaveChangesAsync(ct);
        }

        if (!await _db.GiftRedemptions.AsNoTracking().AnyAsync(ct))
        {
            var kitId = DeterministicGuid("gift_kit");
            var voucherId = DeterministicGuid("gift_voucher10");

            _db.GiftRedemptions.AddRange(
                new GiftRedemption
                {
                    Id = DeterministicGuid("redemption_citizen_kit"),
                    UserId = CitizenId,
                    GiftId = kitId,
                    PointsSpent = 120,
                    Quantity = 1,
                    RedemptionCode = "RDM-DEMO-0001",
                    Status = RedemptionStatus.Delivered,
                    DeliveryAddress = "Tòa Gamma, Đại học FPT Hòa Lạc, Thạch Thất, Hà Nội",
                    RedeemedAt = utcNow.AddDays(-12),
                    FulfilledAt = utcNow.AddDays(-9)
                },
                new GiftRedemption
                {
                    Id = DeterministicGuid("redemption_citizen_voucher"),
                    UserId = CitizenId,
                    GiftId = voucherId,
                    PointsSpent = 80,
                    Quantity = 1,
                    RedemptionCode = "RDM-DEMO-0002",
                    VoucherCode = "ECOHL-GIFT10-8899",
                    ExpiresAt = utcNow.AddDays(25), // Tuyệt đối > UtcNow để JOB-12 không expire trước giờ demo
                    Status = RedemptionStatus.Redeemed,
                    RedeemedAt = utcNow.AddDays(-5)
                });

            await _db.SaveChangesAsync(ct);
        }
    }

    private async Task EnsureGamificationWalletsTreesAndCheckinsAsync(
        DateTime utcNow,
        DateOnly todayVn,
        LeaderboardCycle activeCycle,
        List<User> extraCitizens,
        CancellationToken ct)
    {
        if (await _db.UserPointWallets.AsNoTracking().AnyAsync(ct))
        {
            return;
        }

        // Đồng bộ UserEcoProfile + UserPointWallet + VirtualTreeProgress
        var citizenEco = await _db.UserEcoProfiles.FirstOrDefaultAsync(e => e.UserId == CitizenId, ct);
        if (citizenEco != null)
        {
            citizenEco.CurrentGreenPoints = 250;
            citizenEco.TotalLifetimePoints = 400;
            citizenEco.TreeLevel = 2;
            citizenEco.TreeExperience = 100;
            citizenEco.TotalWasteScanned = 8;
            citizenEco.TotalRecycledKg = 28.5m;
        }

        var vipEco = await _db.UserEcoProfiles.FirstOrDefaultAsync(e => e.UserId == CitizenVipId, ct);
        if (vipEco != null)
        {
            vipEco.CurrentGreenPoints = 920;
            vipEco.TotalLifetimePoints = 1120;
            vipEco.TreeLevel = 4;
            vipEco.TreeExperience = 720;
            vipEco.TotalWasteScanned = 16;
            vipEco.TotalRecycledKg = 64.0m;
        }

        var citizenWallet = new UserPointWallet
        {
            Id = DeterministicGuid("wallet_citizen"),
            UserId = CitizenId,
            CurrentBalance = 250,
            TotalEarned = 400,
            TotalSpent = 150,
            UpdatedAt = utcNow.AddDays(-1)
        };

        var vipWallet = new UserPointWallet
        {
            Id = DeterministicGuid("wallet_citizen_vip"),
            UserId = CitizenVipId,
            CurrentBalance = 920,
            TotalEarned = 1120,
            TotalSpent = 200,
            UpdatedAt = utcNow.AddDays(-1)
        };

        var recyclerWallet = new UserPointWallet
        {
            Id = DeterministicGuid("wallet_recycler"),
            UserId = RecyclerId,
            CurrentBalance = 0,
            TotalEarned = 0,
            TotalSpent = 0,
            UpdatedAt = utcNow.AddDays(-2)
        };

        var recyclerProWallet = new UserPointWallet
        {
            Id = DeterministicGuid("wallet_recycler_pro"),
            UserId = RecyclerProId,
            CurrentBalance = 0,
            TotalEarned = 0,
            TotalSpent = 0,
            UpdatedAt = utcNow.AddDays(-1)
        };

        _db.UserPointWallets.AddRange(citizenWallet, vipWallet, recyclerWallet, recyclerProWallet);

        // Cây ảo của citizen: sẵn sàng để trình diễn tưới nước & bón phân ngay khi demo
        _db.VirtualTreeProgresses.AddRange(
            new VirtualTreeProgress
            {
                Id = DeterministicGuid("tree_citizen"),
                UserId = CitizenId,
                TreeLevel = 2,
                GrowthPoints = 100,
                TotalWatered = 6,
                DailyWaterCount = 0,
                LastWaterDate = todayVn.AddDays(-1),
                LastWateredAt = utcNow.AddDays(-1),
                HydrationPercent = 72,
                LastHydrationAt = utcNow.AddHours(-6),
                CareExp = 60,
                HarvestReady = false,
                TotalFertilized = 2,
                DailyFertilizeCount = 0,
                LastFertilizeDate = todayVn.AddDays(-2),
                FertilizerPacks = 3,
                LastScanFertilizerDate = todayVn.AddDays(-1),
                TotalHarvested = 0,
                UpdatedAt = utcNow.AddDays(-1)
            },
            new VirtualTreeProgress
            {
                Id = DeterministicGuid("tree_citizen_vip"),
                UserId = CitizenVipId,
                TreeLevel = 4,
                GrowthPoints = 720,
                TotalWatered = 24,
                DailyWaterCount = 1,
                LastWaterDate = todayVn,
                LastWateredAt = utcNow.AddHours(-3),
                HydrationPercent = 94,
                LastHydrationAt = utcNow.AddHours(-3),
                CareExp = 180,
                HarvestReady = true,
                TotalFertilized = 8,
                DailyFertilizeCount = 0,
                LastFertilizeDate = todayVn.AddDays(-1),
                FertilizerPacks = 4,
                LastScanFertilizerDate = todayVn.AddDays(-1),
                TotalHarvested = 1,
                UpdatedAt = utcNow.AddHours(-3)
            });

        // Chuỗi điểm danh 3 ngày liên tiếp kết thúc HÔM QUA (giờ ICT) -> hôm nay (todayVn) chưa điểm danh để demo
        for (var streak = 1; streak <= 3; streak++)
        {
            var dayOffset = 4 - streak;
            var pts = 5 + (streak - 1);
            _db.DailyCheckinLogs.Add(new DailyCheckinLog
            {
                Id = DeterministicGuid($"checkin_citizen_d{streak}"),
                UserId = CitizenId,
                CheckinDate = todayVn.AddDays(-dayOffset),
                StreakCount = streak,
                PointsAwarded = pts,
                CreatedAt = utcNow.AddDays(-dayOffset)
            });
        }

        // Giao dịch điểm bên trong cửa sổ Leaderboard Cycle đang mở để BXH tự động xếp hạng Top 10
        var windowMid = activeCycle.StartsAt.AddDays(3);
        var txList = new List<PointTransaction>
        {
            new()
            {
                Id = DeterministicGuid("ptx_vip_1"),
                WalletId = vipWallet.Id,
                UserId = CitizenVipId,
                Points = 480,
                ActionType = PointActionType.TreeCare,
                SourceType = "VirtualTree",
                SourceId = DeterministicGuid("tree_citizen_vip").ToString(),
                IdempotencyKey = "seed-ptx-vip-1",
                Description = "Thu hoạch Cây Ảo Sống Xanh cấp 4 (chu kỳ tháng)",
                CreatedAt = windowMid
            },
            new()
            {
                Id = DeterministicGuid("ptx_vip_2"),
                WalletId = vipWallet.Id,
                UserId = CitizenVipId,
                Points = 300,
                ActionType = PointActionType.DailyCheckin,
                SourceType = "DailyCheckin",
                IdempotencyKey = "seed-ptx-vip-2",
                Description = "Chuỗi điểm danh Sống Xanh tích lũy trong tháng",
                CreatedAt = windowMid.AddDays(2)
            },
            new()
            {
                Id = DeterministicGuid("ptx_citizen_earn_1"),
                WalletId = citizenWallet.Id,
                UserId = CitizenId,
                Points = 220,
                ActionType = PointActionType.TreeCare,
                SourceType = "VirtualTree",
                SourceId = DeterministicGuid("tree_citizen").ToString(),
                IdempotencyKey = "seed-ptx-cit-1",
                Description = "Thu hoạch Cây Ảo Sống Xanh (đợt 1)",
                CreatedAt = windowMid.AddDays(1)
            },
            new()
            {
                Id = DeterministicGuid("ptx_citizen_earn_2"),
                WalletId = citizenWallet.Id,
                UserId = CitizenId,
                Points = 160,
                ActionType = PointActionType.TreeCare,
                SourceType = "VirtualTree",
                SourceId = DeterministicGuid("tree_citizen").ToString(),
                IdempotencyKey = "seed-ptx-cit-2",
                Description = "Thu hoạch Cây Ảo Sống Xanh (đợt 2)",
                CreatedAt = windowMid.AddDays(4)
            },
            new()
            {
                Id = DeterministicGuid("ptx_citizen_checkin"),
                WalletId = citizenWallet.Id,
                UserId = CitizenId,
                Points = 20,
                ActionType = PointActionType.DailyCheckin,
                SourceType = "DailyCheckin",
                IdempotencyKey = "seed-ptx-cit-checkin",
                Description = "Chuỗi điểm danh Sống Xanh 3 ngày liên tiếp",
                CreatedAt = utcNow.AddDays(-1)
            },
            new()
            {
                Id = DeterministicGuid("ptx_citizen_redeem_1"),
                WalletId = citizenWallet.Id,
                UserId = CitizenId,
                Points = -120,
                ActionType = PointActionType.RedeemGift,
                SourceType = "GiftRedemption",
                IdempotencyKey = "seed-ptx-cit-redeem-1",
                Description = "Đổi quà: Bộ Kit Ươm Mầm Xanh Để Bàn",
                CreatedAt = utcNow.AddDays(-12)
            },
            new()
            {
                Id = DeterministicGuid("ptx_citizen_redeem_2"),
                WalletId = citizenWallet.Id,
                UserId = CitizenId,
                Points = -30,
                ActionType = PointActionType.TreeCare,
                SourceType = "VirtualTree",
                IdempotencyKey = "seed-ptx-cit-tree",
                Description = "Quy đổi điểm đầu tư dưỡng chất cho Cây Ảo",
                CreatedAt = utcNow.AddDays(-6)
            }
        };

        var ecoMap = await _db.UserEcoProfiles.AsNoTracking()
            .Where(e => extraCitizens.Select(u => u.Id).Contains(e.UserId))
            .ToDictionaryAsync(e => e.UserId, ct);

        for (var i = 0; i < extraCitizens.Count; i++)
        {
            var u = extraCitizens[i];
            var pts = ecoMap.TryGetValue(u.Id, out var eco) ? eco.CurrentGreenPoints : 100;
            var wId = DeterministicGuid($"wallet_extra_{u.Username}");
            _db.UserPointWallets.Add(new UserPointWallet
            {
                Id = wId,
                UserId = u.Id,
                CurrentBalance = pts,
                TotalEarned = pts,
                TotalSpent = 0,
                UpdatedAt = windowMid.AddHours(i * 3)
            });

            _db.VirtualTreeProgresses.Add(new VirtualTreeProgress
            {
                Id = DeterministicGuid($"tree_extra_{u.Username}"),
                UserId = u.Id,
                TreeLevel = eco?.TreeLevel ?? 1,
                GrowthPoints = pts / 2,
                TotalWatered = 4,
                HydrationPercent = 80,
                LastHydrationAt = utcNow.AddHours(-4),
                FertilizerPacks = 2,
                UpdatedAt = utcNow.AddHours(-4)
            });

            txList.Add(new PointTransaction
            {
                Id = DeterministicGuid($"ptx_extra_{u.Username}"),
                WalletId = wId,
                UserId = u.Id,
                Points = pts,
                ActionType = PointActionType.TreeCare,
                SourceType = "VirtualTree",
                IdempotencyKey = $"seed-ptx-extra-{u.Username}",
                Description = $"Thưởng chăm sóc & thu hoạch Cây Ảo Sống Xanh ({u.Username})",
                CreatedAt = windowMid.AddHours(i + 1)
            });
        }

        _db.PointTransactions.AddRange(txList);
        await _db.SaveChangesAsync(ct);
    }

    private async Task EnsureHandoversTradeChatsReviewsAndComplaintsAsync(
        DateTime utcNow,
        List<User> extraCitizens,
        CancellationToken ct)
    {
        if (await _db.HandoverRequests.AsNoTracking().AnyAsync(ct))
        {
            return;
        }

        var cats = await _db.WasteCategories.AsNoTracking().ToDictionaryAsync(c => c.Code.ToLowerInvariant(), ct);
        var plastic = cats["plastic"];
        var paper = cats["paper"];
        var glass = cats["glass"];
        var metal = cats["metal"];
        var cardboard = cats["cardboard"];

        var requests = new List<HandoverRequest>();
        var receipts = new List<HandoverReceipt>();
        var tradeOrders = new List<TradeOrder>();
        var tradeReceipts = new List<TransactionReceipt>();
        var chats = new List<ChatMessage>();
        var reviews = new List<TransactionReview>();
        var complaints = new List<TransactionComplaint>();

        var tomorrowSlotUtc = new DateTime(utcNow.Year, utcNow.Month, utcNow.Day, 2, 0, 0, DateTimeKind.Utc).AddDays(1); // 09:00 ICT

        void AddSyncedHandover(
            Guid id,
            string code,
            Guid citizenId,
            Guid recyclerId,
            HandoverStatus status,
            OrderStatus orderStatus,
            string address,
            decimal lat,
            decimal lng,
            DateTime createdAt,
            DateTime proposedAt,
            DateTime? confirmedAt,
            DateTime? completedAt,
            WasteCategory cat1,
            decimal estKg1,
            decimal? actualKg1,
            decimal agreedPrice1,
            string? note = null,
            string? cancelReason = null,
            Guid? cancelledBy = null)
        {
            requests.Add(new HandoverRequest
            {
                Id = id,
                RequestCode = code,
                UserId = citizenId,
                RecyclerId = recyclerId,
                PickupAddress = address,
                Latitude = lat,
                Longitude = lng,
                WastePhotoUrl = DemoWastePhotoJson,
                ProposedTimeSlot = proposedAt,
                ConfirmedTimeSlot = confirmedAt,
                ActualHandoverAt = completedAt,
                Status = status,
                UserNote = note,
                CancelReason = cancelReason,
                CancelledBy = cancelledBy,
                CreatedAt = createdAt,
                UpdatedAt = completedAt ?? confirmedAt ?? createdAt,
                Items =
                [
                    new HandoverRequestItem
                    {
                        Id = DeterministicGuid($"item_{id}_{cat1.Code}"),
                        RequestId = id,
                        WasteCategoryId = cat1.Id,
                        EstimatedWeightKg = estKg1,
                        ActualWeightKg = actualKg1,
                        AgreedPricePerKg = agreedPrice1,
                        CreatedAt = createdAt
                    }
                ]
            });

            tradeOrders.Add(new TradeOrder
            {
                Id = id,
                UserId = citizenId,
                PartnerId = recyclerId,
                RecyclerUserId = recyclerId,
                Status = orderStatus,
                ScheduledAt = confirmedAt ?? proposedAt,
                EstimatedWeightKg = estKg1,
                Note = note,
                CategoryCode = cat1.Code,
                InitialPricePerKg = agreedPrice1,
                AgreedPricePerKg = agreedPrice1,
                ProposedTimeSlot = proposedAt.AddHours(7).ToString("dd/MM/yyyy HH:mm"),
                ConfirmedTimeSlot = confirmedAt?.AddHours(7).ToString("dd/MM/yyyy HH:mm"),
                PickupAddress = address,
                AcceptedAt = confirmedAt ?? (status is HandoverStatus.Accepted or HandoverStatus.Completed or HandoverStatus.AwaitingConfirmation ? createdAt.AddMinutes(30) : null),
                CreatedAt = createdAt,
                UpdatedAt = completedAt ?? createdAt
            });
        }

        // A. Citizen đang có ĐÚNG 2 đơn active (Pending + AwaitingConfirmation) -> còn slot tạo mới ở U-06 (giới hạn 3)!
        var hndPendingId = DeterministicGuid("hnd_citizen_pending");
        AddSyncedHandover(
            hndPendingId,
            "HND-DEMO-0001",
            CitizenId,
            RecyclerId,
            HandoverStatus.Pending,
            OrderStatus.Pending,
            "Cổng chính Đại học FPT, Khu CNC Hòa Lạc, Thạch Thất, Hà Nội",
            21.0124m,
            105.5253m,
            utcNow.AddHours(-1), // An toàn trước JOB-03 (TTL 24h)
            tomorrowSlotUtc,
            null,
            null,
            plastic,
            8.5m,
            null,
            8_500m,
            "Chai nhựa PET sạch đã bóp bẹp, nhờ cơ sở ghé sáng mai lúc 9h.");

        chats.Add(new ChatMessage
        {
            Id = DeterministicGuid("chat_pending_1"),
            TradeOrderId = hndPendingId,
            SenderUserId = CitizenId,
            SenderRole = "Citizen",
            MessageType = ChatMessageType.Text,
            MessageContent = "Chào cơ sở EcoGreen Hòa Lạc, mình có khoảng 8.5kg chai nhựa sạch gửi sáng mai lúc 9h nhé!",
            IsRead = false,
            SentAt = utcNow.AddMinutes(-50)
        });

        var hndAwaitId = DeterministicGuid("hnd_citizen_awaiting");
        AddSyncedHandover(
            hndAwaitId,
            "HND-DEMO-0002",
            CitizenId,
            RecyclerId,
            HandoverStatus.AwaitingConfirmation,
            OrderStatus.AwaitingConfirmation,
            "Tòa Beta, Đại học FPT Hòa Lạc, Thạch Thất, Hà Nội",
            21.0131m,
            105.5261m,
            utcNow.AddHours(-8),
            utcNow.AddHours(-3),
            utcNow.AddHours(-3),
            null,
            metal,
            6.0m,
            6.5m,
            27_000m,
            "Vỏ lon nhôm đã cân xong tại sân KTX, chờ người dân xác nhận biên nhận.");

        var rcpAwaitId = DeterministicGuid("rcp_citizen_awaiting");
        receipts.Add(new HandoverReceipt
        {
            Id = rcpAwaitId,
            RequestId = hndAwaitId,
            ReceiptCode = "RCP-DEMO-0002",
            UserId = CitizenId,
            RecyclerId = RecyclerId,
            TotalWeightKg = 6.5m,
            TotalAmountCash = 175_500m,
            PointsAwarded = 0,
            ScaleProofPhotoUrl = DemoScalePhotoJson,
            ReceiptImageUrl = "https://images.unsplash.com/photo-1604187351574-c75ca79f5807?auto=format&fit=crop&w=900&q=80",
            CreatedAt = utcNow.AddHours(-2), // An toàn trước JOB-01 (48h) để demo U-09!
            ConfirmedByUserAt = null
        });

        chats.AddRange(
            new ChatMessage
            {
                Id = DeterministicGuid("chat_await_1"),
                TradeOrderId = hndAwaitId,
                SenderUserId = RecyclerId,
                SenderRole = "Recycler",
                MessageType = ChatMessageType.Proposal,
                MessageContent = "Đề xuất giá vỏ lon nhôm ép gọn: 27.000đ/kg.",
                ProposalStatus = ProposalStatus.Accepted,
                ProposedPricePerKg = 27_000m,
                ProposedWeightKg = 6.5m,
                ProposedTimeSlot = utcNow.AddHours(4).ToString("dd/MM/yyyy HH:mm"),
                ProposalDataJson = "{\"pricePerKg\":27000,\"weightKg\":6.5}",
                IsRead = true,
                SentAt = utcNow.AddHours(-5),
                ReadAt = utcNow.AddHours(-4)
            },
            new ChatMessage
            {
                Id = DeterministicGuid("chat_await_2"),
                TradeOrderId = hndAwaitId,
                SenderUserId = RecyclerId,
                SenderRole = "Recycler",
                MessageType = ChatMessageType.System,
                MessageContent = "Cơ sở thu gom đã cân thực tế 6.5 kg và phát hành biên nhận RCP-DEMO-0002. Vui lòng kiểm tra và xác nhận.",
                IsRead = false,
                SentAt = utcNow.AddHours(-2)
            });

        // B. Citizen VIP có 1 đơn Accepted đang thương lượng lịch hẹn
        var hndVipAcceptedId = DeterministicGuid("hnd_vip_accepted");
        AddSyncedHandover(
            hndVipAcceptedId,
            "HND-DEMO-0003",
            CitizenVipId,
            RecyclerId,
            HandoverStatus.Accepted,
            OrderStatus.Accepted,
            "Khu nhà ở CNC Hòa Lạc, Xã Tân Xã, Thạch Thất, Hà Nội",
            21.0155m,
            105.5295m,
            utcNow.AddHours(-6),
            tomorrowSlotUtc.AddHours(5),
            tomorrowSlotUtc.AddHours(5),
            null,
            cardboard,
            15.0m,
            null,
            4_500m,
            "Thùng carton đã tháo băng keo và xếp gọn.");

        chats.Add(new ChatMessage
        {
            Id = DeterministicGuid("chat_vip_acc_1"),
            TradeOrderId = hndVipAcceptedId,
            SenderUserId = RecyclerId,
            SenderRole = "Recycler",
            MessageType = ChatMessageType.Proposal,
            MessageContent = "Đã chốt giá thu mua bìa carton 4.500đ/kg, xe tải nhỏ ghé lúc 14h30.",
            ProposalStatus = ProposalStatus.Accepted,
            ProposedPricePerKg = 4_500m,
            ProposedWeightKg = 15.0m,
            ProposedTimeSlot = "14:30 ngày mai",
            ProposalDataJson = "{\"pricePerKg\":4500,\"weightKg\":15.0}",
            IsRead = true,
            SentAt = utcNow.AddHours(-4),
            ReadAt = utcNow.AddHours(-3)
        });

        // C. Lịch sử Completed / Rejected / Cancelled / Expired cho Citizen, Citizen VIP & Các vựa xung quanh
        var completedSpecs = new (string Key, string Code, Guid CitId, Guid RecId, WasteCategory Cat, decimal Kg, decimal Price, long Pts, int DaysAgo, string Addr, int? Stars, string? Comment)[]
        {
            ("hnd_citizen_completed_1", "HND-DEMO-0004", CitizenId, RecyclerId, plastic, 18.5m, 8_500m, 0, 10, "KTX Dom B, Đại học FPT Hòa Lạc", 5, "Cân điện tử minh bạch, nhân viên thân thiện và đúng giờ!"),
            ("hnd_citizen_completed_2", "HND-DEMO-0005", CitizenId, RecyclerId, metal, 10.0m, 26_000m, 0, 7, "Tòa Alpha, Đại học FPT Hòa Lạc", 5, "Giá thu mua vỏ lon nhôm rất tốt, chuyển khoản ngay."),
            ("hnd_citizen_completed_3", "HND-DEMO-0006", CitizenId, RecyclerHl1Id, paper, 12.0m, 4_500m, 0, 14, "Thôn 1, Thạch Hòa, Thạch Thất", 4, "Đến thu gom nhanh gọn dù đường hơi hẹp."),
            ("hnd_vip_completed_1", "HND-DEMO-0007", CitizenVipId, RecyclerId, metal, 40.0m, 27_000m, 0, 9, "Khu CNC Hòa Lạc, Thạch Thất", 5, "Hợp tác thu gom định kỳ hàng tuần rất chuyên nghiệp."),
            ("hnd_vip_completed_2", "HND-DEMO-0008", CitizenVipId, RecyclerHl2Id, cardboard, 24.0m, 4_200m, 0, 6, "Xã Tân Xã, Thạch Thất", 5, "Hỗ trợ bốc xếp nhiệt tình."),
            ("hnd_vip_completed_3", "HND-DEMO-0009", CitizenVipId, RecyclerHl3Id, plastic, 15.0m, 8_200m, 0, 4, "Bình Yên, Thạch Thất", 4, "Cân chuẩn, minh bạch.")
        };

        var reviewTimestamps = new Dictionary<Guid, DateTime>();

        foreach (var sp in completedSpecs)
        {
            var id = DeterministicGuid(sp.Key);
            var createdAt = utcNow.AddDays(-sp.DaysAgo - 1);
            var completedAt = utcNow.AddDays(-sp.DaysAgo);
            AddSyncedHandover(
                id, sp.Code, sp.CitId, sp.RecId,
                HandoverStatus.Completed, OrderStatus.Completed,
                sp.Addr, 21.0135m, 105.5260m,
                createdAt, completedAt.AddHours(-2), completedAt.AddHours(-1), completedAt,
                sp.Cat, sp.Kg, sp.Kg, sp.Price);

            var receiptId = DeterministicGuid($"rcp_{sp.Key}");
            receipts.Add(new HandoverReceipt
            {
                Id = receiptId,
                RequestId = id,
                ReceiptCode = $"RCP-{sp.Code[4..]}",
                UserId = sp.CitId,
                RecyclerId = sp.RecId,
                TotalWeightKg = sp.Kg,
                TotalAmountCash = sp.Kg * sp.Price,
                PointsAwarded = sp.Pts,
                ScaleProofPhotoUrl = DemoScalePhotoJson,
                ReceiptImageUrl = "https://images.unsplash.com/photo-1604187351574-c75ca79f5807?auto=format&fit=crop&w=900&q=80",
                CreatedAt = completedAt.AddMinutes(-15),
                ConfirmedByUserAt = completedAt
            });

            tradeReceipts.Add(new TransactionReceipt
            {
                Id = DeterministicGuid($"trcp_{sp.Key}"),
                TradeOrderId = id,
                ActualWeightKg = sp.Kg,
                UnitPrice = sp.Price,
                TotalAmountCash = sp.Kg * sp.Price,
                PointsAwarded = (int)sp.Pts,
                QrSecurityCode = $"QR-{sp.Code}",
                CompletedAt = completedAt
            });

            if (sp.Stars.HasValue)
            {
                var rvwId = DeterministicGuid($"rvw_{sp.Key}");
                reviews.Add(new TransactionReview
                {
                    Id = rvwId,
                    RequestId = id,
                    ReviewerId = sp.CitId,
                    RecyclerId = sp.RecId,
                    RatingStars = sp.Stars.Value,
                    Comment = sp.Comment,
                    Status = ReviewStatus.Visible,
                    CreatedAt = completedAt.AddHours(1),
                    CreatedBy = sp.CitId.ToString()
                });
                reviewTimestamps[rvwId] = completedAt.AddHours(1);
            }
        }

        // Thêm 3 đơn kết thúc (Rejected, Cancelled, Expired) để hiển thị đa dạng trạng thái
        AddSyncedHandover(
            DeterministicGuid("hnd_rejected"), "HND-DEMO-0010", CitizenId, RecyclerHl4Id,
            HandoverStatus.Rejected, OrderStatus.Rejected,
            "Đại học Quốc Gia Hòa Lạc", 21.0055m, 105.5125m,
            utcNow.AddDays(-8), utcNow.AddDays(-7), null, null,
            paper, 2.0m, null, 4_000m,
            note: "Giấy báo cũ 2kg", cancelReason: "Khối lượng thấp hơn mức tối thiểu thu gom tận nơi (5kg).", cancelledBy: RecyclerHl4Id);

        AddSyncedHandover(
            DeterministicGuid("hnd_cancelled"), "HND-DEMO-0011", CitizenId, RecyclerHl2Id,
            HandoverStatus.Cancelled, OrderStatus.Cancelled,
            "Khu CNC Hòa Lạc", 21.0140m, 105.5270m,
            utcNow.AddDays(-11), utcNow.AddDays(-10), null, null,
            plastic, 5.0m, null, 8_000m,
            note: "Hẹn lịch cuối tuần", cancelReason: "Người dân thay đổi lịch công tác đột xuất.", cancelledBy: CitizenId);

        AddSyncedHandover(
            DeterministicGuid("hnd_expired"), "HND-DEMO-0012", CitizenVipId, RecyclerHl3Id,
            HandoverStatus.Expired, OrderStatus.Expired,
            "Xã Tân Xã, Thạch Thất", 21.0211m, 105.5389m,
            utcNow.AddDays(-13), utcNow.AddDays(-12), null, null,
            glass, 6.0m, null, 2_000m,
            note: "Chai thủy tinh sạch");

        // D. 52 giao dịch Completed + 52 đánh giá hợp lệ (4.81 sao) cho recycler_pro (đủ chuẩn mua gói PRO live!)
        var proReviewers = new[] { CitizenVipId }
            .Concat(extraCitizens.Select(u => u.Id))
            .ToArray();
        var proCats = new[] { plastic, metal, paper, cardboard };
        var proComments5 = new[]
        {
            "Cân rất chuẩn, đội thu gom mặc đồng phục gọn gàng và nhiệt tình.",
            "Thanh toán chuyển khoản ngay sau khi cân xong, rất uy tín tại Hòa Lạc!",
            "Đặt lịch buổi sáng là buổi chiều có nhân viên tới lấy đúng giờ.",
            "Giá thu mua cao hơn các vựa tự phát bên ngoài, hỗ trợ phân loại rất kỹ.",
            "Rất hài lòng với sự chuyên nghiệp của EcoPro Thạch Thất."
        };
        var proComments4 = new[]
        {
            "Thu gom nhanh, trừ 1 điểm nhỏ vì đường vào hơi kẹt giờ tan tầm.",
            "Giá tốt, nhân viên lịch sự nhưng nhắn tin xác nhận hơi chậm 15 phút."
        };

        for (var idx = 1; idx <= 52; idx++)
        {
            var reqId = DeterministicGuid($"hnd_rec_pro_{idx}");
            var rcpId = DeterministicGuid($"rcp_rec_pro_{idx}");
            var rvwId = DeterministicGuid($"rvw_rec_pro_{idx}");
            var reviewerId = proReviewers[(idx - 1) % proReviewers.Length];
            var cat = proCats[(idx - 1) % proCats.Length];
            var daysAgo = 15 + ((idx - 1) % 25);
            var createdAt = utcNow.AddDays(-daysAgo - 1);
            var completedAt = utcNow.AddDays(-daysAgo).AddMinutes(idx);
            var kg = 5.0m + (idx % 12);
            var price = cat.Code switch
            {
                "metal" => 28_000m,
                "plastic" => 9_000m,
                "paper" => 4_800m,
                _ => 4_500m
            };

            AddSyncedHandover(
                reqId,
                $"HND-PRO-{idx:D4}",
                reviewerId,
                RecyclerProId,
                HandoverStatus.Completed,
                OrderStatus.Completed,
                "Khu dân cư CNC Hòa Lạc, Thạch Thất, Hà Nội",
                21.0165m,
                105.5282m,
                createdAt,
                completedAt.AddHours(-2),
                completedAt.AddHours(-1),
                completedAt,
                cat,
                kg,
                kg,
                price);

            receipts.Add(new HandoverReceipt
            {
                Id = rcpId,
                RequestId = reqId,
                ReceiptCode = $"RCP-PRO-{idx:D4}",
                UserId = reviewerId,
                RecyclerId = RecyclerProId,
                TotalWeightKg = kg,
                TotalAmountCash = kg * price,
                PointsAwarded = 0,
                ScaleProofPhotoUrl = DemoScalePhotoJson,
                CreatedAt = completedAt.AddMinutes(-10),
                ConfirmedByUserAt = completedAt
            });

            var stars = idx <= 42 ? 5 : 4; // 42x5 + 10x4 = 250/52 = 4.8077 -> 4.81 sao
            var comment = stars == 5
                ? proComments5[(idx - 1) % proComments5.Length]
                : proComments4[(idx - 1) % proComments4.Length];

            reviews.Add(new TransactionReview
            {
                Id = rvwId,
                RequestId = reqId,
                ReviewerId = reviewerId,
                RecyclerId = RecyclerProId,
                RatingStars = stars,
                Comment = comment,
                Status = ReviewStatus.Visible,
                CreatedAt = completedAt.AddHours(1),
                CreatedBy = reviewerId.ToString()
            });
            reviewTimestamps[rvwId] = completedAt.AddHours(1);
        }

        // Thêm 3 đánh giá mẫu khác cho recycler (22222222) + 1 đánh giá Reported để Staff kiểm duyệt
        for (var i = 1; i <= 3; i++)
        {
            var reqId = DeterministicGuid($"hnd_rec_std_{i}");
            var rcpId = DeterministicGuid($"rcp_rec_std_{i}");
            var rvwId = DeterministicGuid($"rvw_rec_std_{i}");
            var reviewer = extraCitizens[i].Id;
            var createdAt = utcNow.AddDays(-5 - i);
            var completedAt = utcNow.AddDays(-4 - i);
            AddSyncedHandover(
                reqId, $"HND-STD-{i:D4}", reviewer, RecyclerId,
                HandoverStatus.Completed, OrderStatus.Completed,
                "Đại học FPT Hòa Lạc, Thạch Thất", 21.0138m, 105.5256m,
                createdAt, completedAt.AddHours(-2), completedAt.AddHours(-1), completedAt,
                plastic, 8.0m, 8.0m, 8_500m);

            receipts.Add(new HandoverReceipt
            {
                Id = rcpId,
                RequestId = reqId,
                ReceiptCode = $"RCP-STD-{i:D4}",
                UserId = reviewer,
                RecyclerId = RecyclerId,
                TotalWeightKg = 8.0m,
                TotalAmountCash = 68_000m,
                PointsAwarded = 0,
                ScaleProofPhotoUrl = DemoScalePhotoJson,
                CreatedAt = completedAt.AddMinutes(-10),
                ConfirmedByUserAt = completedAt
            });

            reviews.Add(new TransactionReview
            {
                Id = rvwId,
                RequestId = reqId,
                ReviewerId = reviewer,
                RecyclerId = RecyclerId,
                RatingStars = i == 3 ? 4 : 5,
                Comment = i == 3
                    ? "Thu gom ổn, cần bổ sung thêm túi phân loại."
                    : "Cơ sở EcoGreen Hòa Lạc phục vụ sinh viên FPT cực nhanh!",
                Status = i == 3 ? ReviewStatus.Reported : ReviewStatus.Visible,
                ReportCount = i == 3 ? 1 : 0,
                LastReportReason = i == 3 ? "Nội dung bình luận nghi ngờ spam quảng cáo đối thủ." : null,
                ReportedByUserId = i == 3 ? RecyclerId : null,
                CreatedAt = completedAt.AddHours(1),
                CreatedBy = reviewer.ToString()
            });
            reviewTimestamps[rvwId] = completedAt.AddHours(1);
        }

        // Cập nhật bộ đếm đánh giá của recycler (5 visible: 5+5+5+5+4 = 24/5? Wait 2 completed + 2 visible = 4 visible: 5,5,5,5 + 1 reported still counts until hidden = 4.80)
        var recProfile = await _db.RecyclerProfiles.FirstOrDefaultAsync(r => r.UserId == RecyclerId, ct);
        if (recProfile != null)
        {
            recProfile.TotalReviews = 5;
            recProfile.AverageRating = 4.80m;
        }

        // E. 3 Phiếu Khiếu nại (Open, Investigating, Resolved) gắn với 3 RequestId riêng biệt (tuân thủ partial unique index)
        var complaintSpecs = new (string Key, string Code, ComplaintStatus Status, ComplaintCategory Cat, Guid Complainant, string Role, Guid Respondent, string Desc, string? Note, ComplaintResolutionOutcome? Outcome, int DaysAgo)[]
        {
            ("cmp_open", "CMP-DEMO-0001", ComplaintStatus.Open, ComplaintCategory.ScaleDiscrepancy,
                extraCitizens[1].Id, "Citizen", RecyclerHl2Id,
                "Khối lượng bìa carton thực tế cân tại nhà là 14kg nhưng biên nhận ghi nhận 11.5kg.", null, null, 2),
            ("cmp_inv", "CMP-DEMO-0002", ComplaintStatus.Investigating, ComplaintCategory.PaymentIssue,
                extraCitizens[2].Id, "Citizen", RecyclerHl3Id,
                "Đã xác nhận bàn giao 10kg kim loại nhưng chưa nhận đủ tiền chuyển khoản đối soát.", "Staff đang đối chiếu sao kê hai bên.", null, 4),
            ("cmp_res", "CMP-DEMO-0003", ComplaintStatus.Resolved, ComplaintCategory.NoShow,
                extraCitizens[3].Id, "Citizen", RecyclerHl4Id,
                "Cơ sở trễ hẹn 3 tiếng không thông báo trước trong khung giờ sáng.",
                "Đã nhắc nhở đối tác và cộng 30 điểm thiện chí hỗ trợ người dân.", ComplaintResolutionOutcome.CompensateUser, 7)
        };

        foreach (var cp in complaintSpecs)
        {
            var reqId = DeterministicGuid($"hnd_{cp.Key}");
            var rcpId = DeterministicGuid($"rcp_{cp.Key}");
            var createdAt = utcNow.AddDays(-cp.DaysAgo - 2);
            var completedAt = utcNow.AddDays(-cp.DaysAgo - 1);
            var hStatus = cp.Status == ComplaintStatus.Resolved ? HandoverStatus.Completed : HandoverStatus.Disputed;
            var oStatus = cp.Status == ComplaintStatus.Resolved ? OrderStatus.Completed : OrderStatus.Disputed;

            AddSyncedHandover(
                reqId, $"HND-CMP-{cp.Code[9..]}", cp.Complainant, cp.Respondent,
                hStatus, oStatus,
                "Khu vực Hòa Lạc, Thạch Thất, Hà Nội", 21.0150m, 105.5300m,
                createdAt, completedAt.AddHours(-2), completedAt.AddHours(-1), completedAt,
                cardboard, 14.0m, 11.5m, 4_500m);

            receipts.Add(new HandoverReceipt
            {
                Id = rcpId,
                RequestId = reqId,
                ReceiptCode = $"RCP-CMP-{cp.Code[9..]}",
                UserId = cp.Complainant,
                RecyclerId = cp.Respondent,
                TotalWeightKg = 11.5m,
                TotalAmountCash = 51_750m,
                PointsAwarded = 0,
                ScaleProofPhotoUrl = DemoScalePhotoJson,
                CreatedAt = completedAt.AddMinutes(-15),
                ConfirmedByUserAt = cp.Status == ComplaintStatus.Resolved ? completedAt : null
            });

            complaints.Add(new TransactionComplaint
            {
                Id = DeterministicGuid(cp.Key),
                ComplaintCode = cp.Code,
                RequestId = reqId,
                ReceiptId = rcpId,
                ComplainantId = cp.Complainant,
                ComplainantRole = cp.Role,
                RespondentId = cp.Respondent,
                Category = cp.Cat,
                Description = cp.Desc,
                EvidenceUrlsJson = DemoWastePhotoJson,
                Status = cp.Status,
                AssignedStaffId = cp.Status != ComplaintStatus.Open ? StaffId : null,
                TakenAt = cp.Status != ComplaintStatus.Open ? utcNow.AddDays(-cp.DaysAgo) : null,
                ResolutionOutcome = cp.Outcome,
                ResolutionNote = cp.Note,
                ResolvedByStaffId = cp.Status == ComplaintStatus.Resolved ? StaffId : null,
                ResolvedAt = cp.Status == ComplaintStatus.Resolved ? utcNow.AddDays(-cp.DaysAgo + 1) : null,
                WarningLogged = cp.Status == ComplaintStatus.Resolved,
                CreatedAt = utcNow.AddDays(-cp.DaysAgo),
                CreatedBy = cp.Complainant.ToString()
            });
        }

        _db.HandoverRequests.AddRange(requests);
        _db.HandoverReceipts.AddRange(receipts);
        _db.TradeOrders.AddRange(tradeOrders);
        _db.TransactionReceipts.AddRange(tradeReceipts);
        _db.ChatMessages.AddRange(chats);
        _db.TransactionReviews.AddRange(reviews);
        _db.TransactionComplaints.AddRange(complaints);

        await _db.SaveChangesAsync(ct);

        // Khôi phục CreatedAt lịch sử cho TransactionReviews (bị AuditableEntityInterceptor ghi đè UtcNow)
        if (_db.Database.IsRelational())
        {
            foreach (var kv in reviewTimestamps)
            {
                await _db.TransactionReviews
                    .Where(r => r.Id == kv.Key)
                    .ExecuteUpdateAsync(s => s.SetProperty(r => r.CreatedAt, kv.Value), ct);
            }
        }
    }

    private async Task EnsureAiScanFeedbackAndDatasetAsync(DateTime utcNow, CancellationToken ct)
    {
        if (await _db.AiScanHistories.AsNoTracking().AnyAsync(ct))
        {
            return;
        }

        var cats = await _db.WasteCategories.AsNoTracking().ToDictionaryAsync(c => c.Code.ToLowerInvariant(), ct);

        var scanSpecs = new (string Key, Guid? UserId, string Label, double Conf, bool Ood, int DaysAgo, string ImageUrl)[]
        {
            ("scan_1", CitizenId, "plastic", 0.94, false, 9, "https://images.unsplash.com/photo-1532996122724-e3c354a0b15b?auto=format&fit=crop&w=800&q=80"),
            ("scan_2", CitizenId, "metal", 0.91, false, 7, "https://images.unsplash.com/photo-1604187351574-c75ca79f5807?auto=format&fit=crop&w=800&q=80"),
            ("scan_3", CitizenId, "battery", 0.88, false, 5, "https://images.unsplash.com/photo-1619642751034-765dfdf7c58e?auto=format&fit=crop&w=800&q=80"),
            ("scan_4", CitizenId, "unknown", 0.38, true, 3, "https://images.unsplash.com/photo-1558618666-fcd25c85cd64?auto=format&fit=crop&w=800&q=80"),
            ("scan_5", CitizenVipId, "paper", 0.89, false, 6, "https://images.unsplash.com/photo-1589634749000-1e72ec00a13f?auto=format&fit=crop&w=800&q=80"),
            ("scan_6", CitizenVipId, "glass", 0.86, false, 4, "https://images.unsplash.com/photo-1558640476-437a2b9438a2?auto=format&fit=crop&w=800&q=80"),
            ("scan_7", CitizenVipId, "cardboard", 0.92, false, 2, "https://images.unsplash.com/photo-1595278069401-29ff344f6376?auto=format&fit=crop&w=800&q=80"),
            ("scan_8", CitizenId, "plastic", 0.51, true, 1, "https://images.unsplash.com/photo-1611284446314-60a58ac0deb9?auto=format&fit=crop&w=800&q=80")
        };

        var scans = new List<AiScanHistory>();
        foreach (var s in scanSpecs)
        {
            scans.Add(new AiScanHistory
            {
                Id = DeterministicGuid(s.Key),
                UserId = s.UserId,
                ImageUrl = s.ImageUrl,
                ImageSha256 = Sha256Hex($"scan_seed_{s.Key}"),
                ImageWidth = 800,
                ImageHeight = 600,
                ModelUsed = "yolov8_detect",
                PredictedLabel = s.Label,
                ConfidenceScore = s.Conf,
                IsOutOfDistribution = s.Ood,
                DetectedObjectsJson = JsonSerializer.Serialize(new[]
                {
                    new { label = s.Label, confidence = s.Conf, bbox = new[] { 120, 80, 540, 480 } }
                }, JsonOptions),
                ScanStatus = "Completed",
                CreatedAt = utcNow.AddDays(-s.DaysAgo)
            });
        }
        _db.AiScanHistories.AddRange(scans);

        // Dataset items (5 VerifiedLabeled đã tách split 70/20/10 + 3 Unlabeled chờ Staff S-02 gán nhãn)
        var verifiedSpecs = new (string Key, string ScanKey, string Label, DatasetSplitType Split, DatasetItemSourceType Source)[]
        {
            ("ds_v1", "scan_1", "plastic", DatasetSplitType.Train, DatasetItemSourceType.UserFeedback),
            ("ds_v2", "scan_2", "metal", DatasetSplitType.Train, DatasetItemSourceType.StaffUpload),
            ("ds_v3", "scan_5", "paper", DatasetSplitType.Train, DatasetItemSourceType.StaffUpload),
            ("ds_v4", "scan_6", "glass", DatasetSplitType.Validation, DatasetItemSourceType.BenchmarkSeed),
            ("ds_v5", "scan_3", "battery", DatasetSplitType.Test, DatasetItemSourceType.LowConfidenceAuto)
        };

        var versionItems = new List<AiDatasetVersionItem>();
        var versionId = DeterministicGuid("dataset_version_v2");

        foreach (var vs in verifiedSpecs)
        {
            var cat = cats[vs.Label];
            var dsId = DeterministicGuid(vs.Key);
            var scanId = DeterministicGuid(vs.ScanKey);
            var itemUrl = scans.First(s => s.Id == scanId).ImageUrl;
            var yoloJson = JsonSerializer.Serialize(new[]
            {
                new { class_name = vs.Label, x_center = 0.5, y_center = 0.5, width = 0.65, height = 0.72 }
            }, JsonOptions);

            _db.AiDatasetItems.Add(new AiDatasetItem
            {
                Id = dsId,
                SourceType = vs.Source,
                SourceScanId = scanId,
                ImageUrl = itemUrl,
                ImageSha256 = Sha256Hex($"ds_seed_{vs.Key}"),
                ImageWidth = 800,
                ImageHeight = 600,
                PredictedLabel = vs.Label,
                PredictedConfidence = 0.90,
                GroundTruthCategoryId = cat.Id,
                GroundTruthLabel = vs.Label,
                YoloAnnotationsJson = yoloJson,
                Status = DatasetItemStatus.VerifiedLabeled,
                StickySplit = vs.Split,
                LabeledByStaffId = StaffId,
                LabeledAt = utcNow.AddDays(-3),
                QualityNote = "Ảnh rõ nét, đúng chuẩn phân loại EcoLink v2."
            });

            versionItems.Add(new AiDatasetVersionItem
            {
                Id = DeterministicGuid($"dvi_{vs.Key}"),
                VersionId = versionId,
                DatasetItemId = dsId,
                AssignedSplit = vs.Split,
                CategoryCodeSnapshot = vs.Label,
                YoloAnnotationsSnapshot = yoloJson
            });
        }

        // 3 Unlabeled items sẵn sàng cho Staff gán nhãn ở S-02
        var unlabeledSpecs = new (string Key, string ScanKey, string PredLabel, double Conf)[]
        {
            ("ds_u1", "scan_4", "unknown", 0.38),
            ("ds_u2", "scan_8", "plastic", 0.51),
            ("ds_u3", "scan_7", "cardboard", 0.62)
        };

        foreach (var us in unlabeledSpecs)
        {
            var scanId = DeterministicGuid(us.ScanKey);
            var itemUrl = scans.First(s => s.Id == scanId).ImageUrl;
            _db.AiDatasetItems.Add(new AiDatasetItem
            {
                Id = DeterministicGuid(us.Key),
                SourceType = DatasetItemSourceType.LowConfidenceAuto,
                SourceScanId = scanId,
                ImageUrl = itemUrl,
                ImageSha256 = Sha256Hex($"ds_seed_{us.Key}"),
                ImageWidth = 800,
                ImageHeight = 600,
                PredictedLabel = us.PredLabel,
                PredictedConfidence = us.Conf,
                Status = DatasetItemStatus.Unlabeled
            });
        }

        _db.AiDatasetVersions.Add(new AiDatasetVersion
        {
            Id = versionId,
            VersionCode = "v2.0.0",
            VersionName = "EcoLink Hoa Lac Curated Benchmark v2.0",
            Description = "Tập dữ liệu cân bằng chuẩn hóa bao gồm Plastic, Metal, Paper, Glass và Battery.",
            TrainRatio = 70,
            ValRatio = 20,
            TestRatio = 10,
            TotalSamples = 5,
            TrainCount = 3,
            ValCount = 1,
            TestCount = 1,
            ClassDistributionJson = JsonSerializer.Serialize(new { plastic = 1, metal = 1, paper = 1, glass = 1, battery = 1 }, JsonOptions),
            ImbalanceRatio = 1.00m,
            IsBalanced = true,
            Status = DatasetVersionStatus.Published,
            CreatedByStaffId = StaffId
        });

        _db.AiDatasetVersionItems.AddRange(versionItems);

        // 4 AI Feedback Logs (2 Pending, 1 Verified ApproveAndLabel, 1 Rejected)
        _db.AIFeedbackLogs.AddRange(
            new AIFeedbackLog
            {
                Id = DeterministicGuid("aifb_pending_1"),
                UserId = CitizenId,
                ScanId = DeterministicGuid("scan_4"),
                ImageUrl = scans[3].ImageUrl,
                ImageSha256 = scans[3].ImageSha256,
                ModelUsed = "yolov8_detect",
                DetectedObjectsSnapshot = scans[3].DetectedObjectsJson,
                PredictedLabel = "unknown",
                ConfidenceScore = 0.38,
                UserReportedLabel = "battery",
                Status = FeedbackStatus.Pending,
                Note = "Pin laptop phồng, mong hệ thống nhận diện là battery để cảnh báo nguy hại."
            },
            new AIFeedbackLog
            {
                Id = DeterministicGuid("aifb_pending_2"),
                UserId = CitizenId,
                ScanId = DeterministicGuid("scan_8"),
                ImageUrl = scans[7].ImageUrl,
                ImageSha256 = scans[7].ImageSha256,
                ModelUsed = "yolov8_detect",
                DetectedObjectsSnapshot = scans[7].DetectedObjectsJson,
                PredictedLabel = "plastic",
                ConfidenceScore = 0.51,
                UserReportedLabel = "cardboard",
                Status = FeedbackStatus.Pending,
                Note = "Hộp giấy cán màng mỏng bị nhận nhầm sang nhựa."
            },
            new AIFeedbackLog
            {
                Id = DeterministicGuid("aifb_verified_1"),
                UserId = CitizenId,
                ScanId = DeterministicGuid("scan_1"),
                ImageUrl = scans[0].ImageUrl,
                ImageSha256 = scans[0].ImageSha256,
                ModelUsed = "yolov8_detect",
                DetectedObjectsSnapshot = scans[0].DetectedObjectsJson,
                PredictedLabel = "plastic",
                ConfidenceScore = 0.94,
                UserReportedLabel = "plastic",
                AdminVerifiedLabel = "plastic",
                Status = FeedbackStatus.Verified,
                ReviewDecision = AiFeedbackReviewDecision.ApproveAndLabel,
                ReviewedByStaffId = StaffId,
                ReviewedAt = utcNow.AddDays(-4),
                ReviewNote = "Đã chốt nhãn plastic đưa vào tập Train v2.0.0.",
                DatasetItemId = DeterministicGuid("ds_v1")
            },
            new AIFeedbackLog
            {
                Id = DeterministicGuid("aifb_rejected_1"),
                UserId = CitizenVipId,
                ScanId = DeterministicGuid("scan_7"),
                ImageUrl = scans[6].ImageUrl,
                ImageSha256 = scans[6].ImageSha256,
                ModelUsed = "yolov8_detect",
                DetectedObjectsSnapshot = scans[6].DetectedObjectsJson,
                PredictedLabel = "cardboard",
                ConfidenceScore = 0.92,
                UserReportedLabel = "paper",
                Status = FeedbackStatus.Rejected,
                ReviewDecision = AiFeedbackReviewDecision.Reject,
                ReviewedByStaffId = StaffId,
                ReviewedAt = utcNow.AddDays(-2),
                ReviewNote = "Vật thể là bìa carton sóng (cardboard), phân loại của AI đã chính xác."
            });

        await _db.SaveChangesAsync(ct);
    }

    private async Task EnsureServiceOrdersSubscriptionsAndWebhooksAsync(DateTime utcNow, CancellationToken ct)
    {
        if (await _db.ServiceOrders.AsNoTracking().AnyAsync(ct))
        {
            return;
        }

        // BẢO ĐẢM INVARIANT DSC01 & UTC012:
        // - KHÔNG kích hoạt IsFeatured (gói PRO Active) cho bất kỳ recycler nào trong bán kính 15km quanh Hòa Lạc (21.0124, 105.5253).
        // - recycler_hn (77777777, Cầu Giấy ~26.8km) có gói PRO Active để showcase Tích xanh trên bản đồ bán kính rộng.
        // - recycler_hl1 (99999991) có gói PRO đã Hết hạn (Expired) -> IsFeatured=false nên UTC012 pass 100%.
        // - recycler_pro (22222223) CHƯA có PRO Active (để trình diễn mua PRO trực tiếp) nhưng có 1 đơn Underpaid anomaly để demo A-11.

        var orders = new List<ServiceOrder>();
        var subscriptions = new List<UserSubscription>();
        var webhooks = new List<PaymentWebhookLog>();

        const string bankCode = "TPB";
        const string bankName = "TPBank";
        const string accountNo = "05773216801";
        const string accountName = "ECOLINK PLATFORM SERVICE";

        // 1. recycler_hn: PRO Active (300 days remaining)
        var hnOrderId = DeterministicGuid("srv_order_pro_hn");
        orders.Add(new ServiceOrder
        {
            Id = hnOrderId,
            OrderCode = "ORD-SEED-PRO-HN01",
            UserId = RecyclerHnId,
            PackageId = CanonicalServicePackages.ProId,
            PackageCodeSnapshot = CanonicalServicePackages.ProCode,
            PackageNameSnapshot = "Pro (Tích xanh & Ưu tiên bản đồ)",
            DurationDaysSnapshot = 365,
            FeaturesSnapshot = CanonicalServicePackages.ProFeaturesJson,
            ExpectedAmountVnd = 1_000_000m,
            PaidAmountVnd = 1_000_000m,
            PaymentMethod = "VIETQR",
            Description = "ORD-SEED-PRO-HN01",
            QrCodeUrl = "https://img.vietqr.io/image/TPB-05773216801-compact2.png?amount=1000000&addInfo=ORD-SEED-PRO-HN01",
            TransferContent = "ORD-SEED-PRO-HN01",
            BankCode = bankCode,
            BankName = bankName,
            AccountNo = accountNo,
            AccountName = accountName,
            PaymentReference = "FT26300112233",
            GatewayTransactionNo = "TPB-99001122",
            OrderStatus = ServiceOrderStatus.Paid,
            PaymentStatus = ServicePaymentStatus.Success,
            ReconciliationStatus = ReconciliationStatus.Matched,
            AnomalyType = PaymentAnomalyType.None,
            QrExpiresAt = utcNow.AddDays(-64),
            PaidAt = utcNow.AddDays(-65)
        });
        subscriptions.Add(new UserSubscription
        {
            Id = DeterministicGuid("sub_pro_hn"),
            UserId = RecyclerHnId,
            PackageId = CanonicalServicePackages.ProId,
            SourceOrderId = hnOrderId,
            PackageCode = CanonicalServicePackages.ProCode,
            FeaturesSnapshot = CanonicalServicePackages.ProFeaturesJson,
            StartsAt = utcNow.AddDays(-65),
            ExpiresAt = utcNow.AddDays(300),
            Status = SubscriptionStatus.Active
        });
        webhooks.Add(new PaymentWebhookLog
        {
            Id = DeterministicGuid("wh_pro_hn"),
            WebhookCode = "WH-SEED-0001",
            IdempotencyKey = "seed-wh-pro-hn01",
            GatewaySource = "PAYOS",
            MatchedOrderId = hnOrderId,
            ParsedOrderCode = "ORD-SEED-PRO-HN01",
            ReceivedAmountVnd = 1_000_000m,
            BankReference = "FT26300112233",
            TransferDescription = "ORD-SEED-PRO-HN01",
            SignatureValid = true,
            RawPayload = "{\"orderCode\":\"ORD-SEED-PRO-HN01\",\"amount\":1000000,\"status\":\"PAID\"}",
            ProcessingResult = "MATCHED_PAID",
            AnomalyType = PaymentAnomalyType.None,
            ReceivedAt = utcNow.AddDays(-65)
        });

        // 2. recycler_hl1: PRO Expired (đảm bảo UTC012 DistanceKm == 0.0 không bị đẩy xuống bởi IsFeatured)
        var hl1OrderId = DeterministicGuid("srv_order_pro_hl1_exp");
        orders.Add(new ServiceOrder
        {
            Id = hl1OrderId,
            OrderCode = "ORD-SEED-PRO-HL01",
            UserId = RecyclerHl1Id,
            PackageId = CanonicalServicePackages.ProId,
            PackageCodeSnapshot = CanonicalServicePackages.ProCode,
            PackageNameSnapshot = "Pro (Tích xanh & Ưu tiên bản đồ)",
            DurationDaysSnapshot = 365,
            FeaturesSnapshot = CanonicalServicePackages.ProFeaturesJson,
            ExpectedAmountVnd = 1_000_000m,
            PaidAmountVnd = 1_000_000m,
            PaymentMethod = "VIETQR",
            Description = "ORD-SEED-PRO-HL01",
            QrCodeUrl = "https://img.vietqr.io/image/TPB-05773216801-compact2.png?amount=1000000&addInfo=ORD-SEED-PRO-HL01",
            TransferContent = "ORD-SEED-PRO-HL01",
            BankCode = bankCode,
            BankName = bankName,
            AccountNo = accountNo,
            AccountName = accountName,
            PaymentReference = "FT25300001122",
            OrderStatus = ServiceOrderStatus.Paid,
            PaymentStatus = ServicePaymentStatus.Success,
            ReconciliationStatus = ReconciliationStatus.Matched,
            AnomalyType = PaymentAnomalyType.None,
            QrExpiresAt = utcNow.AddDays(-369),
            PaidAt = utcNow.AddDays(-370)
        });
        subscriptions.Add(new UserSubscription
        {
            Id = DeterministicGuid("sub_pro_hl1_exp"),
            UserId = RecyclerHl1Id,
            PackageId = CanonicalServicePackages.ProId,
            SourceOrderId = hl1OrderId,
            PackageCode = CanonicalServicePackages.ProCode,
            FeaturesSnapshot = CanonicalServicePackages.ProFeaturesJson,
            StartsAt = utcNow.AddDays(-370),
            ExpiresAt = utcNow.AddDays(-5),
            Status = SubscriptionStatus.Expired
        });

        // 3. citizen_vip: PREMIUM Active (22 ngày còn lại)
        var vipOrderId = DeterministicGuid("srv_order_prem_vip");
        orders.Add(new ServiceOrder
        {
            Id = vipOrderId,
            OrderCode = "ORD-SEED-PREM-VIP1",
            UserId = CitizenVipId,
            PackageId = CanonicalServicePackages.PremiumId,
            PackageCodeSnapshot = CanonicalServicePackages.PremiumCode,
            PackageNameSnapshot = "Premium (Tắt quảng cáo)",
            DurationDaysSnapshot = 30,
            FeaturesSnapshot = CanonicalServicePackages.PremiumFeaturesJson,
            ExpectedAmountVnd = 59_000m,
            PaidAmountVnd = 59_000m,
            PaymentMethod = "VIETQR",
            Description = "ORD-SEED-PREM-VIP1",
            QrCodeUrl = "https://img.vietqr.io/image/TPB-05773216801-compact2.png?amount=59000&addInfo=ORD-SEED-PREM-VIP1",
            TransferContent = "ORD-SEED-PREM-VIP1",
            BankCode = bankCode,
            BankName = bankName,
            AccountNo = accountNo,
            AccountName = accountName,
            PaymentReference = "FT26320005900",
            OrderStatus = ServiceOrderStatus.Paid,
            PaymentStatus = ServicePaymentStatus.Success,
            ReconciliationStatus = ReconciliationStatus.Matched,
            AnomalyType = PaymentAnomalyType.None,
            QrExpiresAt = utcNow.AddDays(-7),
            PaidAt = utcNow.AddDays(-8)
        });
        subscriptions.Add(new UserSubscription
        {
            Id = DeterministicGuid("sub_prem_vip"),
            UserId = CitizenVipId,
            PackageId = CanonicalServicePackages.PremiumId,
            SourceOrderId = vipOrderId,
            PackageCode = CanonicalServicePackages.PremiumCode,
            FeaturesSnapshot = CanonicalServicePackages.PremiumFeaturesJson,
            StartsAt = utcNow.AddDays(-8),
            ExpiresAt = utcNow.AddDays(22),
            Status = SubscriptionStatus.Active
        });

        // 4. recycler_pro: 1 đơn Underpaid anomaly để Admin A-11 đối soát (KHÔNG kích hoạt PRO trước demo!)
        var anomOrderId = DeterministicGuid("srv_order_pro_anom");
        orders.Add(new ServiceOrder
        {
            Id = anomOrderId,
            OrderCode = "ORD-SEED-PRO-ANOM",
            UserId = RecyclerProId,
            PackageId = CanonicalServicePackages.ProId,
            PackageCodeSnapshot = CanonicalServicePackages.ProCode,
            PackageNameSnapshot = "Pro (Tích xanh & Ưu tiên bản đồ)",
            DurationDaysSnapshot = 365,
            FeaturesSnapshot = CanonicalServicePackages.ProFeaturesJson,
            ExpectedAmountVnd = 1_000_000m,
            PaidAmountVnd = 500_000m,
            PaymentMethod = "VIETQR",
            Description = "ORD-SEED-PRO-ANOM",
            QrCodeUrl = "https://img.vietqr.io/image/TPB-05773216801-compact2.png?amount=1000000&addInfo=ORD-SEED-PRO-ANOM",
            TransferContent = "ORD-SEED-PRO-ANOM",
            BankCode = bankCode,
            BankName = bankName,
            AccountNo = accountNo,
            AccountName = accountName,
            PaymentReference = "FT26324000500",
            OrderStatus = ServiceOrderStatus.Pending,
            PaymentStatus = ServicePaymentStatus.Anomaly,
            ReconciliationStatus = ReconciliationStatus.Underpaid,
            AnomalyType = PaymentAnomalyType.Underpaid,
            AnomalyNote = "Khách chuyển thiếu 500.000đ (nhận 500.000đ / 1.000.000đ).",
            QrExpiresAt = utcNow.AddHours(12)
        });
        webhooks.Add(new PaymentWebhookLog
        {
            Id = DeterministicGuid("wh_pro_anom"),
            WebhookCode = "WH-SEED-0002",
            IdempotencyKey = "seed-wh-pro-anom",
            GatewaySource = "VIETQR_MOCK",
            MatchedOrderId = anomOrderId,
            ParsedOrderCode = "ORD-SEED-PRO-ANOM",
            ReceivedAmountVnd = 500_000m,
            BankReference = "FT26324000500",
            TransferDescription = "ORD-SEED-PRO-ANOM",
            SignatureValid = true,
            RawPayload = "{\"orderCode\":\"ORD-SEED-PRO-ANOM\",\"expectedAmount\":1000000,\"receivedAmount\":500000,\"anomaly\":\"Underpaid\"}",
            ProcessingResult = "ANOMALY_UNDERPAID",
            AnomalyType = PaymentAnomalyType.Underpaid,
            ReceivedAt = utcNow.AddHours(-2)
        });

        // 5. Webhook mồ côi UnmatchedReference để Admin đối soát thủ công
        webhooks.Add(new PaymentWebhookLog
        {
            Id = DeterministicGuid("wh_unmatched_orphan"),
            WebhookCode = "WH-SEED-0003",
            IdempotencyKey = "seed-wh-unmatched-999",
            GatewaySource = "PAYOS",
            MatchedOrderId = null,
            ParsedOrderCode = "ECOLINK_UNKNOWN_999",
            ReceivedAmountVnd = 59_000m,
            BankReference = "FT26324009999",
            TransferDescription = "Chuyen khoan nang cap goi ECOLINK_UNKNOWN_999",
            SignatureValid = true,
            RawPayload = "{\"description\":\"ECOLINK_UNKNOWN_999\",\"amount\":59000}",
            ProcessingResult = "UNMATCHED_REFERENCE",
            AnomalyType = PaymentAnomalyType.UnmatchedReference,
            ReceivedAt = utcNow.AddHours(-1)
        });

        // 6. Đơn Pending VietQR mẫu cho recycler (22222222)
        orders.Add(new ServiceOrder
        {
            Id = DeterministicGuid("srv_order_pending_rec"),
            OrderCode = "ORD-SEED-PRO-PEND",
            UserId = RecyclerId,
            PackageId = CanonicalServicePackages.ProId,
            PackageCodeSnapshot = CanonicalServicePackages.ProCode,
            PackageNameSnapshot = "Pro (Tích xanh & Ưu tiên bản đồ)",
            DurationDaysSnapshot = 365,
            FeaturesSnapshot = CanonicalServicePackages.ProFeaturesJson,
            ExpectedAmountVnd = 1_000_000m,
            PaymentMethod = "VIETQR",
            Description = "ORD-SEED-PRO-PEND",
            QrCodeUrl = "https://img.vietqr.io/image/TPB-05773216801-compact2.png?amount=1000000&addInfo=ORD-SEED-PRO-PEND",
            TransferContent = "ORD-SEED-PRO-PEND",
            BankCode = bankCode,
            BankName = bankName,
            AccountNo = accountNo,
            AccountName = accountName,
            OrderStatus = ServiceOrderStatus.Pending,
            PaymentStatus = ServicePaymentStatus.Pending,
            ReconciliationStatus = ReconciliationStatus.Pending,
            AnomalyType = PaymentAnomalyType.None,
            QrExpiresAt = utcNow.AddHours(6)
        });

        _db.ServiceOrders.AddRange(orders);
        _db.UserSubscriptions.AddRange(subscriptions);
        _db.PaymentWebhookLogs.AddRange(webhooks);
        await _db.SaveChangesAsync(ct);
    }

    private async Task EnsureGovernanceAttendancesModerationAuditAndNotificationsAsync(
        DateTime utcNow,
        DateOnly todayVn,
        List<User> extraCitizens,
        CancellationToken ct)
    {
        if (!await _db.StaffAttendances.AsNoTracking().AnyAsync(ct))
        {
            // Seed 5 ngày công quá khứ cho staff (88888888), bỏ trống hôm nay (todayVn) để trình diễn chấm công live!
            for (var d = 5; d >= 1; d--)
            {
                var workDate = todayVn.AddDays(-d);
                var checkInUtc = new DateTime(workDate.Year, workDate.Month, workDate.Day, 1, 30, 0, DateTimeKind.Utc); // 08:30 ICT
                var checkOutUtc = checkInUtc.AddHours(8).AddMinutes(15);

                _db.StaffAttendances.Add(new StaffAttendance
                {
                    Id = DeterministicGuid($"att_staff_d{d}"),
                    StaffId = StaffId,
                    WorkDate = workDate,
                    CheckInAt = checkInUtc,
                    CheckOutAt = checkOutUtc,
                    WorkedMinutes = 495,
                    StreakDay = 6 - d,
                    WorkPointsEarned = 10 + (5 - d),
                    TasksCompletedSnapshot = 6 + d,
                    ShiftNote = $"Ca vận hành Hòa Lạc ngày {workDate:dd/MM/yyyy}"
                });
            }

            for (var d = 3; d >= 1; d--)
            {
                var workDate = todayVn.AddDays(-d);
                var checkInUtc = new DateTime(workDate.Year, workDate.Month, workDate.Day, 2, 0, 0, DateTimeKind.Utc);
                _db.StaffAttendances.Add(new StaffAttendance
                {
                    Id = DeterministicGuid($"att_staff_sup_d{d}"),
                    StaffId = StaffSupportId,
                    WorkDate = workDate,
                    CheckInAt = checkInUtc,
                    CheckOutAt = checkInUtc.AddHours(8),
                    WorkedMinutes = 480,
                    StreakDay = 4 - d,
                    WorkPointsEarned = 10,
                    TasksCompletedSnapshot = 4,
                    ShiftNote = $"Ca hỗ trợ khiếu nại & kiểm duyệt ngày {workDate:dd/MM/yyyy}"
                });
            }

            await _db.SaveChangesAsync(ct);
        }

        if (!await _db.AccountModerationLogs.AsNoTracking().AnyAsync(ct))
        {
            var hl8 = extraCitizens.First(u => u.Username == "citizen_hl8");
            _db.AccountModerationLogs.Add(new AccountModerationLog
            {
                Id = DeterministicGuid("mod_log_1"),
                TargetUserId = hl8.Id,
                PerformedBy = StaffId,
                ActionType = "WARNING",
                PreviousStatus = "Active",
                NewStatus = "Active",
                Reason = "Cảnh cáo lần 1: Đặt lịch bàn giao phế liệu nhưng không phản hồi khi cơ sở tới điểm hẹn.",
                RevokedTokensCount = 0,
                CreatedAt = utcNow.AddDays(-6)
            });
            await _db.SaveChangesAsync(ct);
        }

        if (!await _db.AuditLogs.AsNoTracking().AnyAsync(ct))
        {
            _db.AuditLogs.AddRange(
                new AuditLog
                {
                    Id = DeterministicGuid("audit_1"),
                    ActorUserId = AdminId,
                    ActorEmail = "admin@ecolink.vn",
                    ActorRole = "Admin",
                    Action = "ROLE_PERMISSION_ASSIGNED",
                    EntityType = "StaffProfile",
                    EntityId = StaffId.ToString(),
                    OldValues = "{}",
                    NewValues = "{\"permissions\":[\"REVIEW_AI\",\"CURATE_DATASET\",\"VERIFY_RECYCLER\",\"HANDLE_COMPLAINT\",\"MANAGE_ACCOUNTS\",\"MANAGE_BLOG\",\"MANAGE_INVENTORY\"]}",
                    Reason = "Khởi tạo toàn quyền vận hành chuẩn cho Nhân viên Lê Thu Hà.",
                    CreatedAt = utcNow.AddDays(-15)
                },
                new AuditLog
                {
                    Id = DeterministicGuid("audit_2"),
                    ActorUserId = AdminId,
                    ActorEmail = "admin@ecolink.vn",
                    ActorRole = "Admin",
                    Action = "GAMIFICATION_CONFIG_UPDATE",
                    EntityType = "GamificationConfig",
                    EntityId = "1",
                    OldValues = "{\"defaultPointsPerKg\":8}",
                    NewValues = "{\"defaultPointsPerKg\":10}",
                    Reason = "Cập nhật hệ số điểm chuẩn cho chiến dịch phân loại rác Hòa Lạc.",
                    CreatedAt = utcNow.AddDays(-12)
                },
                new AuditLog
                {
                    Id = DeterministicGuid("audit_3"),
                    ActorUserId = StaffId,
                    ActorEmail = "staff@ecolink.vn",
                    ActorRole = "Staff",
                    Action = "PUBLISH_DATASET_VERSION",
                    EntityType = "AiDatasetVersion",
                    EntityId = DeterministicGuid("dataset_version_v2").ToString(),
                    OldValues = "{}",
                    NewValues = "{\"versionCode\":\"v2.0.0\",\"totalSamples\":5}",
                    Reason = "Phát hành bộ dữ liệu kiểm chứng đa lớp AI v2.0.0.",
                    CreatedAt = utcNow.AddDays(-3)
                },
                new AuditLog
                {
                    Id = DeterministicGuid("audit_4"),
                    ActorUserId = StaffId,
                    ActorEmail = "staff@ecolink.vn",
                    ActorRole = "Staff",
                    Action = "RECYCLER_KYB_VERIFIED",
                    EntityType = "RecyclerProfile",
                    EntityId = RecyclerProId.ToString(),
                    OldValues = "{\"verificationStatus\":\"Pending\"}",
                    NewValues = "{\"verificationStatus\":\"Verified\"}",
                    Reason = "Đã thẩm định giấy phép môi trường & kho bãi EcoPro Thạch Thất.",
                    CreatedAt = utcNow.AddDays(-20)
                });
            await _db.SaveChangesAsync(ct);
        }

        if (!await _db.Notifications.AsNoTracking().AnyAsync(ct))
        {
            _db.Notifications.AddRange(
                new Notification
                {
                    Id = DeterministicGuid("notif_cit_1"),
                    UserId = CitizenId,
                    Title = "Biên nhận thu gom mới cần xác nhận",
                    Message = "Cơ sở Vựa phế liệu EcoGreen Hòa Lạc đã cân thực tế 6.5 kg kim loại và lập biên nhận RCP-DEMO-0002.",
                    NotificationType = "TRANSACTION",
                    ReferenceType = "HandoverRequest",
                    ReferenceId = DeterministicGuid("hnd_citizen_awaiting"),
                    IsRead = false,
                    CreatedAt = utcNow.AddHours(-2)
                },
                new Notification
                {
                    Id = DeterministicGuid("notif_cit_2"),
                    UserId = CitizenId,
                    Title = "Báo lỗi AI đã được Staff kiểm duyệt",
                    Message = "Hình ảnh chai nhựa PET của bạn đã được xác nhận và bổ sung vào bộ dữ liệu huấn luyện v2.0.0.",
                    NotificationType = "AI_SCAN",
                    IsRead = true,
                    ReadAt = utcNow.AddDays(-3),
                    CreatedAt = utcNow.AddDays(-4)
                },
                new Notification
                {
                    Id = DeterministicGuid("notif_rec_1"),
                    UserId = RecyclerId,
                    Title = "Yêu cầu bàn giao mới HND-DEMO-0001",
                    Message = "Người dân tại Cổng chính ĐH FPT Hòa Lạc vừa gửi yêu cầu thu gom 8.5kg chai nhựa PET.",
                    NotificationType = "TRANSACTION",
                    ReferenceType = "HandoverRequest",
                    ReferenceId = DeterministicGuid("hnd_citizen_pending"),
                    IsRead = false,
                    CreatedAt = utcNow.AddHours(-1)
                },
                new Notification
                {
                    Id = DeterministicGuid("notif_staff_1"),
                    UserId = StaffId,
                    Title = "Có 3 hồ sơ KYB cơ sở thu gom đang chờ duyệt",
                    Message = "Vựa Phế Liệu Hòa Lạc Xanh và 2 đối tác khác đang chờ thẩm định hồ sơ pháp lý.",
                    NotificationType = "SYSTEM",
                    IsRead = false,
                    CreatedAt = utcNow.AddHours(-5)
                },
                new Notification
                {
                    Id = DeterministicGuid("notif_admin_1"),
                    UserId = AdminId,
                    Title = "Cảnh báo đối soát thanh toán Underpaid",
                    Message = "Đơn ORD-SEED-PRO-ANOM chuyển thiếu 500.000đ cần Admin kiểm tra và xử lý.",
                    NotificationType = "SYSTEM",
                    ReferenceType = "ServiceOrder",
                    ReferenceId = DeterministicGuid("srv_order_pro_anom"),
                    IsRead = false,
                    CreatedAt = utcNow.AddHours(-2)
                });
            await _db.SaveChangesAsync(ct);
        }
    }

    private async Task AdvancePostgresSequencesAsync(CancellationToken ct)
    {
        if (!_db.Database.IsRelational())
        {
            return;
        }

        try
        {
            await _db.Database.ExecuteSqlRawAsync(@"
                DO $$
                BEGIN
                    IF EXISTS (SELECT 1 FROM pg_class WHERE relkind = 'S' AND relname = 'seq_handover_request_code') THEN
                        PERFORM setval('seq_handover_request_code', 200, false);
                    END IF;
                    IF EXISTS (SELECT 1 FROM pg_class WHERE relkind = 'S' AND relname = 'seq_handover_receipt_code') THEN
                        PERFORM setval('seq_handover_receipt_code', 200, false);
                    END IF;
                END $$;", ct);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Không thể cập nhật sequences handover (có thể chưa tồn tại trong schema hiện tại).");
        }
    }

    private async Task<DemoSeedReport> BuildReportAsync(bool resetExecuted, CancellationToken ct) => new(
        ResetExecuted: resetExecuted,
        WasteCategories: await _db.WasteCategories.AsNoTracking().CountAsync(ct),
        SortingRules: await _db.SortingRules.AsNoTracking().CountAsync(ct),
        Users: await _db.Users.IgnoreQueryFilters().AsNoTracking().CountAsync(ct),
        RecyclerProfiles: await _db.RecyclerProfiles.IgnoreQueryFilters().AsNoTracking().CountAsync(ct),
        CollectionPartners: await _db.CollectionPartners.AsNoTracking().CountAsync(ct),
        RecyclerPrices: await _db.RecyclerPrices.AsNoTracking().CountAsync(ct),
        HandoverRequests: await _db.HandoverRequests.AsNoTracking().CountAsync(ct),
        HandoverReceipts: await _db.HandoverReceipts.AsNoTracking().CountAsync(ct),
        TradeOrders: await _db.TradeOrders.AsNoTracking().CountAsync(ct),
        ChatMessages: await _db.ChatMessages.AsNoTracking().CountAsync(ct),
        TransactionReviews: await _db.TransactionReviews.IgnoreQueryFilters().AsNoTracking().CountAsync(ct),
        TransactionComplaints: await _db.TransactionComplaints.IgnoreQueryFilters().AsNoTracking().CountAsync(ct),
        BlogPosts: await _db.BlogPosts.AsNoTracking().CountAsync(ct),
        PlatformPolicies: await _db.PlatformPolicySections.AsNoTracking().CountAsync(ct),
        EcoFacts: await _db.EcoFacts.AsNoTracking().CountAsync(ct),
        Gifts: await _db.Gifts.AsNoTracking().CountAsync(ct),
        GiftRedemptions: await _db.GiftRedemptions.AsNoTracking().CountAsync(ct),
        LeaderboardCycles: await _db.LeaderboardCycles.AsNoTracking().CountAsync(ct),
        PointTransactions: await _db.PointTransactions.AsNoTracking().CountAsync(ct),
        AiScanHistories: await _db.AiScanHistories.IgnoreQueryFilters().AsNoTracking().CountAsync(ct),
        AiFeedbackLogs: await _db.AIFeedbackLogs.AsNoTracking().CountAsync(ct),
        AiDatasetItems: await _db.AiDatasetItems.IgnoreQueryFilters().AsNoTracking().CountAsync(ct),
        AiDatasetVersions: await _db.AiDatasetVersions.IgnoreQueryFilters().AsNoTracking().CountAsync(ct),
        ServicePackages: await _db.ServicePackages.IgnoreQueryFilters().AsNoTracking().CountAsync(ct),
        ServiceOrders: await _db.ServiceOrders.IgnoreQueryFilters().AsNoTracking().CountAsync(ct),
        UserSubscriptions: await _db.UserSubscriptions.IgnoreQueryFilters().AsNoTracking().CountAsync(ct),
        PaymentWebhookLogs: await _db.PaymentWebhookLogs.AsNoTracking().CountAsync(ct),
        StaffAttendances: await _db.StaffAttendances.IgnoreQueryFilters().AsNoTracking().CountAsync(ct),
        AccountModerationLogs: await _db.AccountModerationLogs.AsNoTracking().CountAsync(ct),
        AuditLogs: await _db.AuditLogs.AsNoTracking().CountAsync(ct),
        Notifications: await _db.Notifications.AsNoTracking().CountAsync(ct));

    private static Guid DeterministicGuid(string seedKey)
    {
        var hash = MD5.HashData(Encoding.UTF8.GetBytes($"ecolink-demo-seed:{seedKey}"));
        return new Guid(hash);
    }

    private static string Sha256Hex(string input)
    {
        var hash = SHA256.HashData(Encoding.UTF8.GetBytes(input));
        return Convert.ToHexString(hash).ToLowerInvariant();
    }
}
