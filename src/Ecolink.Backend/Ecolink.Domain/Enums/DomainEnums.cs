namespace Ecolink.Domain.Enums;

public enum UserRole
{
    User = 1,
    Citizen = 1,       // Alias cho Người dân
    Recycler = 2,
    Collector = 2,     // Alias cho Đối tác thu gom
    Staff = 3,         // Nhân viên vận hành Back-office
    Admin = 4          // Quản trị viên hệ thống
}

public enum UserStatus
{
    Active = 1,
    Locked = 2,                 // Khóa tạm thời do sai mật khẩu 5 lần (BR-AUTH-04)
    Suspended = 3,              // Đình chỉ tạm thời do vi phạm quy chuẩn (UC S-07, BR-ADM-03)
    PendingVerification = 4,    // Chờ xác minh email
    Deactivated = 5             // Vô hiệu hóa mềm vĩnh viễn, giữ biên nhận lịch sử (UC S-07, BR-ADM-04)
}

public enum VerificationStatus
{
    Pending = 1,    // Chờ Staff thẩm định hồ sơ (UC G-03, S-04)
    Verified = 2,   // Đã thẩm định thành công
    Rejected = 3    // Bị từ chối kèm lý do (BR-MTH-02, MSG-MTH-05)
}

public enum RecyclerActiveStatus
{
    Offline = 1,    // Tạm nghỉ / Ẩn khỏi bản đồ GPS
    Online = 2      // Đang mở cửa nhận thu gom trên bản đồ GPS (UC R-02)
}

public enum OrderStatus
{
    Pending = 1,                // Mới tạo, chờ Recycler phản hồi hoặc thương lượng
    Accepted = 2,               // Đã chốt điều khoản & khóa khung giờ thu gom (BR-NEG-02, BR-NEG-05)
    Rejected = 3,               // Recycler từ chối yêu cầu
    Completed = 4,              // Đã xác nhận biên nhận / quét mã QR hoàn tất bàn giao
    Cancelled = 5,              // Người dùng hoặc Recycler hủy trước khi hoàn tất
    Negotiating = 6,            // Hai bên đang thương lượng giá / khối lượng / khung giờ (BR-NEG-02, BR-HND-02)
    Scheduled = 7,              // Đã lên lịch chuẩn bị bàn giao thực tế (BR-HND-02)
    Expired = 8,                // Tự động hết hạn sau 24h không có phản hồi (BR-HND-03, JOB-03)
    Disputed = 9,               // Đang có khiếu nại chờ Staff phân xử (BR-DSP-06)
    AwaitingConfirmation = 10   // Recycler đã lập biên nhận số, chờ người dân xác nhận (R-06, U-09, JOB-01)
}

public enum ChatMessageType
{
    Text = 1,           // Tin nhắn văn bản thông thường (mở cả sau khi Accepted - BR-NEG-02)
    Image = 2,          // Tin nhắn đính kèm ảnh chụp phế liệu thực tế trên Cloudflare R2
    Proposal = 3,       // Bản đề xuất / phản đề xuất Giá, Khối lượng, Khung giờ (BR-NEG-02, BR-NEG-03)
    System = 4          // Tin nhắn tự động của hệ thống ghi nhận sự kiện chốt/từ chối đề xuất
}

public enum ProposalStatus
{
    Pending = 1,        // Đề xuất mới nhất đang chờ đối phương phản hồi
    Accepted = 2,       // Đã được đối phương chấp nhận và lưu cứng vào đơn (BR-NEG-03)
    Superseded = 3,     // Đã bị ghi đè bởi một đề xuất mới hơn (BR-NEG-03)
    Rejected = 4        // Bị đối phương từ chối
}

public enum FeedbackStatus
{
    Pending = 1,
    Verified = 2,
    Rejected = 3
}

public enum ServiceTargetRole
{
    User = 1,       // Dành cho Citizen (U-16)
    Recycler = 2,   // Dành cho Cơ sở thu mua (R-07)
    All = 3
}

public enum ServiceOrderStatus
{
    Pending = 1,    // Đang chờ chuyển khoản VietQR (TTL 15 phút)
    Paid = 2,       // Đã thanh toán & kích hoạt gói thuê bao
    Expired = 3,    // Hết hạn thanh toán QR
    Cancelled = 4,  // Người dùng hủy đơn khi còn Pending
    Refunded = 5    // Đã hoàn tiền (cho các khoản bất thường sau khi Admin đối soát)
}

public enum ServicePaymentStatus
{
    Pending = 1,
    Success = 2,
    Anomaly = 3,
    Failed = 4,
    Refunded = 5
}

public enum ReconciliationStatus
{
    Pending = 1,
    Matched = 2,
    Underpaid = 3,
    Overpaid = 4,
    LateTransfer = 5,
    UnmatchedReference = 6,
    Resolved = 7,
    Refunded = 8
}

public enum PaymentAnomalyType
{
    None = 0,
    Underpaid = 1,
    Overpaid = 2,
    LateTransfer = 3,
    UnmatchedReference = 4
}

public enum ReconciliationAction
{
    ApproveAndActivate = 1, // Chấp nhận giao dịch lệch/trễ và kích hoạt gói
    ManualMatchOrder = 2,   // Khớp webhook mồ côi vào 1 OrderCode hợp lệ
    MarkRefunded = 3,       // Xác nhận đã hoàn tiền toàn phần cho khách
    MarkExcessRefunded = 4, // Xác nhận đã hoàn lại phần tiền thừa (Overpaid)
    CloseAnomaly = 5        // Đóng bất thường có lý do đối soát hợp lệ
}

public enum SubscriptionStatus
{
    Active = 1,
    Expired = 2,
    Revoked = 3
}

public enum AiFeedbackReviewDecision
{
    ApproveAndLabel = 1, // Duyệt báo lỗi và chốt luôn nhãn chuẩn + đưa vào VerifiedLabeled
    ApproveToQueue = 2,  // Duyệt ảnh hợp lệ, chuyển sang hàng đợi gán nhãn S-02
    Reject = 3           // Bác bỏ ảnh mờ/nhiễu/không hợp lệ (cấm vào Dataset BR-CLS-06)
}

public enum DatasetItemSourceType
{
    UserFeedback = 1,
    LowConfidenceAuto = 2,
    StaffUpload = 3,
    BenchmarkSeed = 4
}

public enum DatasetItemStatus
{
    Unlabeled = 1,
    VerifiedLabeled = 2,
    Excluded = 3
}

public enum DatasetSplitType
{
    Train = 1,
    Validation = 2,
    Test = 3
}

public enum DatasetVersionStatus
{
    Published = 1,
    SyncedToAi = 2,
    Archived = 3
}

public enum ReviewStatus
{
    Visible = 1,
    Reported = 2,
    Hidden = 3,
    Deleted = 4
}

public enum ComplaintCategory
{
    ScaleDiscrepancy = 1,
    PaymentIssue = 2,
    NoShow = 3,
    Misconduct = 4,
    Other = 5
}

public enum ComplaintStatus
{
    Open = 1,
    Investigating = 2,
    Resolved = 3,
    Dismissed = 4
}

public enum ComplaintResolutionOutcome
{
    ConfirmReceiptAsIs = 1,
    CompensateUser = 2,
    UpholdComplainant = 3,
    CancelDisputedOrder = 4,
    Dismissed = 5
}
