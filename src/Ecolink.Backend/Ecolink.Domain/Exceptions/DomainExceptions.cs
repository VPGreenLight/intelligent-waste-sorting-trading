namespace Ecolink.Domain.Exceptions;

/// <summary>
/// Ngoại lệ vi phạm quy tắc nghiệp vụ hệ thống (Business Rule Exception)
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
/// Ngoại lệ không tìm thấy tài nguyên (Not Found Exception)
/// </summary>
public class NotFoundException : Exception
{
    public NotFoundException(string message) : base(message) { }
}
