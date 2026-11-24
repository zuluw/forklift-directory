using System.Text.Json;
using ForkliftDirectory.DTOs;

namespace ForkliftDirectory.Middleware;

public sealed class ExceptionHandlingMiddleware
{
    private readonly RequestDelegate _next;
    private readonly ILogger<ExceptionHandlingMiddleware> _logger;

    public ExceptionHandlingMiddleware(RequestDelegate next, ILogger<ExceptionHandlingMiddleware> logger)
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
        catch (OperationCanceledException) when (context.RequestAborted.IsCancellationRequested)
        {
            // Клиент закрыл соединение — не считаем это ошибкой сервера.
        }
        catch (Exception ex) when (ex is BadHttpRequestException or JsonException)
        {
            _logger.LogWarning(ex, "Некорректный формат HTTP-запроса.");
            await WriteErrorAsync(
                context,
                StatusCodes.Status400BadRequest,
                new ErrorResponse(
                    "Некорректный формат данных в запросе. Проверьте правильность заполнения полей.",
                    "INVALID_REQUEST"));
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Необработанная ошибка при обработке запроса {Path}.", context.Request.Path);
            await WriteErrorAsync(
                context,
                StatusCodes.Status500InternalServerError,
                new ErrorResponse(
                    "Внутренняя ошибка сервера. Пожалуйста, повторите попытку позже.",
                    "INTERNAL_ERROR"));
        }
    }

    private static async Task WriteErrorAsync(HttpContext context, int statusCode, ErrorResponse error)
    {
        if (context.Response.HasStarted)
        {
            return;
        }

        context.Response.Clear();
        context.Response.StatusCode = statusCode;
        context.Response.ContentType = "application/json; charset=utf-8";

        var json = JsonSerializer.Serialize(error, new JsonSerializerOptions
        {
            PropertyNamingPolicy = JsonNamingPolicy.CamelCase
        });

        await context.Response.WriteAsync(json);
    }
}