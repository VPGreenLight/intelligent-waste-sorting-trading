namespace Ecolink.Domain.Exceptions;

/// <summary>
/// Ngoại lệ vi phạm quy tắc nghiệp vụ hệ thống (Business Rule Exception -> HTTP 400)
/// </summary>
public class BusinessRuleException : Exception
{
    public string RuleCode { get; }
    public string MessageCode { get; }

    public BusinessRuleException(string ruleCode, string messageCode, string message) 
        : base(message)
    {
        RuleCode = ruleCode;
        MessageCode = messageCode;
    }
}

/// <summary>
/// Ngoại lệ không tìm thấy tài nguyên (Not Found Exception -> HTTP 404)
/// </summary>
public class NotFoundException : Exception
{
    public NotFoundException(string message) : base(message) { }
}

/// <summary>
/// Ngoại lệ truy cập trái phép tài nguyên thuộc quyền sở hữu của người khác (Forbidden Exception -> HTTP 403)
/// </summary>
public class ForbiddenAccessException : Exception
{
    public string RuleCode { get; }
    public string MessageCode { get; }

    public ForbiddenAccessException(string ruleCode, string messageCode, string message)
        : base(message)
    {
        RuleCode = ruleCode;
        MessageCode = messageCode;
    }
}
