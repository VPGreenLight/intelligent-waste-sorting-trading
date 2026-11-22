namespace Ecolink.Domain.Constants;

/// <summary>
/// Danh mục chuẩn 7 mã quyền RBAC hạt mịn dành cho Nhân viên vận hành (UC A-04, BR-ADM-02).
/// Hỗ trợ chuẩn hóa hai chiều với các alias hiển thị trên Frontend (ecolink-web).
/// </summary>
public static class StaffPermissions
{
    public const string ReviewAi = "REVIEW_AI";
    public const string CurateDataset = "CURATE_DATASET";
    public const string VerifyRecycler = "VERIFY_RECYCLER";
    public const string HandleComplaint = "HANDLE_COMPLAINT";
    public const string ManageAccounts = "MANAGE_ACCOUNTS";
    public const string ManageBlog = "MANAGE_BLOG";
    public const string ManageInventory = "MANAGE_INVENTORY";

    public static readonly IReadOnlyList<(string Code, string LegacyAlias, string Label, string Description)> Catalog =
    [
        (ReviewAi, "ai.review", "Đánh giá ảnh AI", "Thẩm định báo cáo nhận diện sai và gán nhãn Ground-Truth (S-01, S-02)"),
        (CurateDataset, "ai.dataset", "Cập nhật tập dữ liệu", "Tuyển chọn và đóng gói phiên bản Dataset huấn luyện AI (S-03)"),
        (VerifyRecycler, "partner.verify", "Thẩm định hồ sơ đối tác", "Xét duyệt giấy phép pháp lý và tọa độ cơ sở thu mua (S-04)"),
        (HandleComplaint, "complaint.handle", "Xử lý khiếu nại", "Phân xử tranh chấp cân đo, trễ hẹn và biên nhận giao dịch (S-05)"),
        (ManageAccounts, "account.manage", "Quản lý tài khoản", "Tra cứu hồ sơ, cảnh cáo, khóa hoặc vô hiệu hóa tài khoản vi phạm (S-06, S-07)"),
        (ManageBlog, "blog.write", "Soạn bài Blog", "Biên tập bài viết truyền thông kinh tế tuần hoàn gửi Admin duyệt (S-08)"),
        (ManageInventory, "gift.manage", "Quản lý kho quà", "Quản lý nội dung Eco Fact và tồn kho quà tặng / mã voucher (S-09, S-10)")
    ];

    public static IReadOnlyList<string> AllCodes => Catalog.Select(x => x.Code).ToList();

    public static string Normalize(string? rawCode)
    {
        if (string.IsNullOrWhiteSpace(rawCode))
        {
            return string.Empty;
        }

        var trimmed = rawCode.Trim();
        var hit = Catalog.FirstOrDefault(x =>
            x.Code.Equals(trimmed, StringComparison.OrdinalIgnoreCase) ||
            x.LegacyAlias.Equals(trimmed, StringComparison.OrdinalIgnoreCase));

        return string.IsNullOrEmpty(hit.Code) ? trimmed.ToUpperInvariant() : hit.Code;
    }

    public static bool IsValid(string? rawCode)
    {
        var normalized = Normalize(rawCode);
        return !string.IsNullOrEmpty(normalized) &&
               Catalog.Any(x => x.Code.Equals(normalized, StringComparison.OrdinalIgnoreCase));
    }
}
