using Ecolink.Application.Dtos;
using Ecolink.Application.Interfaces;
using Ecolink.Domain.Entities;
using Ecolink.Domain.Exceptions;
using Ecolink.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;

namespace Ecolink.Infrastructure.Services;

/// <summary>
/// Quản lý Eco Fact (UC S-09, BR-ADM-02): Staff xem / thêm / sửa / bật tắt hiển thị, lọc theo cấp cây.
/// Không có xóa cứng vì Eco Fact đã mở khóa phải được giữ lại (BR-GAM-06); muốn gỡ thì đặt INACTIVE.
/// Thiếu trường -> MSG-AUTH-02, vượt độ dài -> MSG-NTF-03, ảnh sai định dạng -> MSG-CLS-01.
/// </summary>
public class EcoFactService : IEcoFactService
{
    private const string Rule = "BR-GAM-06";
    private const int MaxTitle = 200;
    private const int MaxContent = 300;

    private readonly ApplicationDbContext _db;
    private readonly IGamificationConfigService _config;

    public EcoFactService(ApplicationDbContext db, IGamificationConfigService config)
    {
        _db = db;
        _config = config;
    }

    public async Task<GamPagedResult<EcoFactDto>> ListAsync(int? treeLevel, string? status, string? search, int page, int pageSize, CancellationToken ct = default)
    {
        var q = _db.EcoFacts.AsNoTracking().AsQueryable();
        if (treeLevel.HasValue) q = q.Where(f => f.UnlockTreeLevel == treeLevel.Value);
        if (!string.IsNullOrWhiteSpace(status)) { var st = ParseStatus(status); q = q.Where(f => f.Status == st); }
        if (!string.IsNullOrWhiteSpace(search)) { var p = GamErrors.Like(search); q = q.Where(f => EF.Functions.ILike(f.Title, p)); }

        page = Math.Max(1, page);
        pageSize = Math.Clamp(pageSize, 1, 50);
        var total = await q.CountAsync(ct);
        var rows = await q.OrderBy(f => f.UnlockTreeLevel).ThenByDescending(f => f.CreatedAt).ThenBy(f => f.Id)
            .Skip((page - 1) * pageSize).Take(pageSize).ToListAsync(ct);
        return new GamPagedResult<EcoFactDto> { Items = rows.Select(ToDto).ToList(), Page = page, PageSize = pageSize, TotalCount = total };
    }

    public async Task<EcoFactDto> GetAsync(Guid id, CancellationToken ct = default) =>
        ToDto(await _db.EcoFacts.AsNoTracking().FirstOrDefaultAsync(f => f.Id == id, ct)
              ?? throw new NotFoundException("Không tìm thấy Eco Fact."));

    public async Task<EcoFactDto> CreateAsync(Guid staffId, SaveEcoFactRequest r, CancellationToken ct = default)
    {
        var v = await ValidateAsync(r, ct);
        var fact = new EcoFact
        {
            Title = v.Title, Content = v.Content, SourceUrl = v.Source, ImageUrl = v.Image,
            UnlockTreeLevel = v.Level, CreatedByStaffId = staffId, Status = "ACTIVE"
        };
        _db.EcoFacts.Add(fact);
        await _db.SaveChangesAsync(ct);
        return ToDto(fact);
    }

    public async Task<EcoFactDto> UpdateAsync(Guid id, SaveEcoFactRequest r, CancellationToken ct = default)
    {
        var v = await ValidateAsync(r, ct);
        var fact = await _db.EcoFacts.FirstOrDefaultAsync(f => f.Id == id, ct) ?? throw new NotFoundException("Không tìm thấy Eco Fact.");
        fact.Title = v.Title; fact.Content = v.Content; fact.SourceUrl = v.Source; fact.ImageUrl = v.Image;
        fact.UnlockTreeLevel = v.Level;
        fact.UpdatedAt = DateTime.UtcNow;
        await _db.SaveChangesAsync(ct);
        return ToDto(fact);
    }

    public async Task<EcoFactDto> SetStatusAsync(Guid id, string status, CancellationToken ct = default)
    {
        var st = ParseStatus(status);
        var now = DateTime.UtcNow;
        var n = await _db.EcoFacts.Where(f => f.Id == id).ExecuteUpdateAsync(s => s
            .SetProperty(f => f.Status, st).SetProperty(f => f.UpdatedAt, now), ct);
        if (n == 0) throw new NotFoundException("Không tìm thấy Eco Fact.");
        return await GetAsync(id, ct);
    }

    // ------------------------------------------------------------------
    private async Task<(string Title, string Content, string? Source, string? Image, int Level)> ValidateAsync(SaveEcoFactRequest r, CancellationToken ct)
    {
        var title = (r.Title ?? string.Empty).Trim();
        var content = (r.Content ?? string.Empty).Trim();
        if (title.Length == 0 || content.Length == 0)
            throw GamErrors.Rule(Rule, "MSG-AUTH-02", "Vui lòng nhập tiêu đề và nội dung.");
        if (title.Length > MaxTitle)
            throw GamErrors.Rule(Rule, "MSG-NTF-03", $"Tiêu đề tối đa {MaxTitle} ký tự.");
        if (content.Length > MaxContent)
            throw GamErrors.Rule(Rule, "MSG-NTF-03", $"Nội dung tối đa {MaxContent} ký tự.");

        var maxLevel = (await _config.GetSettingsAsync(ct)).MaxLevel;
        if (r.UnlockTreeLevel < 1 || r.UnlockTreeLevel > maxLevel)
            throw GamErrors.Rule(Rule, "MSG-AUTH-02", $"Cấp cây phải từ 1 đến {maxLevel}.");

        return (title, content, GamErrors.CleanHttpUrl(r.SourceUrl, Rule, "URL nguồn"),
            GamErrors.CleanImageUrl(r.ImageUrl, Rule), r.UnlockTreeLevel);
    }

    private static string ParseStatus(string? s)
    {
        var v = (s ?? string.Empty).Trim().ToUpperInvariant();
        return v is "ACTIVE" or "INACTIVE" ? v : throw GamErrors.Rule(Rule, "MSG-AUTH-02", "Trạng thái phải là ACTIVE hoặc INACTIVE.");
    }

    internal static EcoFactDto ToDto(EcoFact f) => new()
    {
        Id = f.Id, Code = "EF-" + f.Id.ToString("N")[..6].ToUpperInvariant(), Title = f.Title, Content = f.Content,
        SourceUrl = f.SourceUrl, ImageUrl = f.ImageUrl, UnlockTreeLevel = f.UnlockTreeLevel, Status = f.Status,
        CreatedAt = f.CreatedAt, UpdatedAt = f.UpdatedAt
    };
}
