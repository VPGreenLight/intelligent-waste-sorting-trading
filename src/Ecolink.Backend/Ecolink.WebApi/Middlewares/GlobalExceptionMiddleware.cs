using System.Net;
using System.Text.Json;
using Ecolink.Application.Common;
using Ecolink.Domain.Exceptions;

namespace Ecolink.WebApi.Middlewares;

/// <summary>
/// Middleware xử lý ngoại lệ tập trung chuẩn Clean Architecture theo clean_architecture_and_coding_guidelines.md
/// </summary>
public class GlobalExceptionMiddleware
{
    private readonly RequestDelegate _next;
    private readonly ILogger<GlobalExceptionMiddleware> _logger;

    public GlobalExceptionMiddleware(RequestDelegate next, ILogger<GlobalExceptionMiddleware> logger)
    {
        _next = next;
        _logger = logger;
    }

    public async Task InvokeAsync(HttpContext context)
    {
        try
        {
            await _next(context);
        }
        catch (Exception ex)
        {
            await HandleExceptionAsync(context, ex);
        }
    }

    private async Task HandleExceptionAsync(HttpContext context, Exception exception)
    {
        var response = context.Response;
        response.ContentType = "application/json";

        var (statusCode, messageCode, message) = exception switch
        {
            BusinessRuleException brEx => (HttpStatusCode.BadRequest, brEx.MessageCode, brEx.Message),
            NotFoundException nfEx => (HttpStatusCode.NotFound, "MSG-NOT-FOUND", nfEx.Message),
            UnauthorizedAccessException unAuthEx => (HttpStatusCode.Unauthorized, "MSG-UNAUTHORIZED", unAuthEx.Message),
            _ => (HttpStatusCode.InternalServerError, "MSG-SYS-500", "Đã xảy ra sự cố trong quá trình xử lý hệ thống. Vui lòng thử lại sau.")
        };

        if (statusCode == HttpStatusCode.InternalServerError)
        {
            _logger.LogError(exception, "[Unhandled Error] Tuyến {Path} xảy ra lỗi: {Message}", context.Request.Path, exception.Message);
        }
        else
        {
            _logger.LogWarning("[Business Rule / Expected Exception] {Path} - {MessageCode}: {Message}", context.Request.Path, messageCode, message);
        }

        response.StatusCode = (int)statusCode;
        var apiResponse = ApiResponse<object>.Fail(message, messageCode);

        var jsonOptions = new JsonSerializerOptions
        {
            PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
            WriteIndented = false
        };

        await response.WriteAsync(JsonSerializer.Serialize(apiResponse, jsonOptions));
    }
}
