namespace Ecolink.Domain.Enums;

public enum UserRole
{
    Citizen = 1,
    Collector = 2,
    Admin = 3
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
