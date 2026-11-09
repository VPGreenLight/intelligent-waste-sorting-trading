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
    Suspended = 3,              // Đình chỉ do vi phạm quy chuẩn
    PendingVerification = 4     // Chờ xác minh email
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
