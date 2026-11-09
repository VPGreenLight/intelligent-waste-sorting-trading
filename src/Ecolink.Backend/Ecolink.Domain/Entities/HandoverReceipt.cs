namespace Ecolink.Domain.Entities;

/// <summary>
/// Biên nhận số do Recycler lập sau khi cân thực tế (UC R-06), người dân xác nhận ở U-09.
/// Ánh xạ bảng HandoverReceipt (báo cáo mục 3.3.5). Quan hệ 1-1 với HandoverRequest.
/// Khác báo cáo: confirmed_by_user_at cho phép NULL (biên nhận tồn tại trước khi người dân xác nhận).
/// Hệ thống chỉ ghi nhận số liệu, không xử lý tiền (BR-HND-05). Sau khi xác nhận thì khóa vĩnh viễn (BR-HND-07).
/// </summary>
public class HandoverReceipt
{
    public Guid Id { get; set; } = Guid.NewGuid();               // receipt_id
    public Guid RequestId { get; set; }                          // request_id (UNIQUE)
    public HandoverRequest? Request { get; set; }

    public string ReceiptCode { get; set; } = string.Empty;      // vd RCP-2026-0001

    public Guid UserId { get; set; }                             // người dân nhận tiền
    public Guid RecyclerId { get; set; }                         // cơ sở trả tiền

    public decimal TotalWeightKg { get; set; }
    public decimal TotalAmountCash { get; set; }                 // VND, chỉ để ghi nhận
    public long PointsAwarded { get; set; }                      // 0 cho tới khi hoàn tất

    /// <summary>scale_proof_photo_url (TEXT NOT NULL): mảng JSON 1–5 URL ảnh cân (BR-HND-04).</summary>
    public string ScaleProofPhotoUrl { get; set; } = string.Empty;
    public string? ReceiptImageUrl { get; set; }

    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public DateTime? ConfirmedByUserAt { get; set; }
}
