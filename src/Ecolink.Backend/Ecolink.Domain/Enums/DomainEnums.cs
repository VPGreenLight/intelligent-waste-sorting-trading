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
    Pending = 1,
    Accepted = 2,
    Rejected = 3,
    Completed = 4,
    Cancelled = 5
}

public enum FeedbackStatus
{
    Pending = 1,
    Verified = 2,
    Rejected = 3
}
