namespace Ecolink.Domain.Enums;

/// <summary>
/// Trạng thái bài viết blog (báo cáo mục 3.6.2): Draft, Pending_Approval, Approved, Rejected.
/// Lưu DB dạng chuỗi đúng như báo cáo (xem BlogPostConfiguration). Khi Admin từ chối, bài quay về Draft kèm
/// rejection_reason (UC A-09) nên giá trị Rejected được giữ trong enum nhưng hiện chưa được gán.
/// </summary>
public enum BlogPostStatus
{
    Draft = 1,
    PendingApproval = 2,
    Approved = 3,
    Rejected = 4
}
