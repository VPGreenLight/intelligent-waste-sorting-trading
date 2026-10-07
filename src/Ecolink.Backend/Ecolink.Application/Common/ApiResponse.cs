namespace Ecolink.Application.Common;

/// <summary>
/// Khung dữ liệu phản hồi API chuẩn hoá toàn hệ thống EcoLink (Chuẩn RFC / Enterprise Envelope)
/// </summary>
/// <typeparam name="T">Kiểu dữ liệu của payload trả về</typeparam>
public class ApiResponse<T>
{
    public bool Success { get; set; }
    public string? MessageCode { get; set; }
    public string? Message { get; set; }
    public T? Data { get; set; }
    public List<string>? Errors { get; set; }
    public DateTime Timestamp { get; set; } = DateTime.UtcNow;

    public static ApiResponse<T> Ok(T data, string? message = null, string? messageCode = null) =>
        new()
        {
            Success = true,
            Data = data,
            Message = message,
            MessageCode = messageCode,
            Timestamp = DateTime.UtcNow
        };

    public static ApiResponse<T> Fail(string message, string? messageCode = null, List<string>? errors = null) =>
        new()
        {
            Success = false,
            Data = default,
            Message = message,
            MessageCode = messageCode,
            Errors = errors,
            Timestamp = DateTime.UtcNow
        };
}
