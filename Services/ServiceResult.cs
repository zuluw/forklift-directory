namespace ForkliftDirectory.Services;

public enum ServiceStatus
{
    Ok,
    Created,
    NoContent,
    BadRequest,
    NotFound,
    Conflict
}

public class ServiceResult
{
    public ServiceStatus Status { get; protected init; }

    public string? ErrorMessage { get; protected init; }

    public string? ErrorCode { get; protected init; }

    public IDictionary<string, string[]>? Errors { get; protected init; }

    public static ServiceResult NoContent() => new() { Status = ServiceStatus.NoContent };

    public static ServiceResult BadRequest(string message, IDictionary<string, string[]>? errors = null) => new()
    {
        Status = ServiceStatus.BadRequest,
        ErrorMessage = message,
        ErrorCode = "VALIDATION_ERROR",
        Errors = errors
    };

    public static ServiceResult NotFound(string message) => new()
    {
        Status = ServiceStatus.NotFound,
        ErrorMessage = message,
        ErrorCode = "NOT_FOUND"
    };

    public static ServiceResult Conflict(string message, string? code = null) => new()
    {
        Status = ServiceStatus.Conflict,
        ErrorMessage = message,
        ErrorCode = code ?? "CONFLICT"
    };
}

public sealed class ServiceResult<T> : ServiceResult
{
    public T? Data { get; private init; }

    public static ServiceResult<T> Ok(T data) => new()
    {
        Status = ServiceStatus.Ok,
        Data = data
    };

    public static ServiceResult<T> Created(T data) => new()
    {
        Status = ServiceStatus.Created,
        Data = data
    };

    public new static ServiceResult<T> BadRequest(string message, IDictionary<string, string[]>? errors = null) => new()
    {
        Status = ServiceStatus.BadRequest,
        ErrorMessage = message,
        ErrorCode = "VALIDATION_ERROR",
        Errors = errors
    };

    public new static ServiceResult<T> NotFound(string message) => new()
    {
        Status = ServiceStatus.NotFound,
        ErrorMessage = message,
        ErrorCode = "NOT_FOUND"
    };

    public new static ServiceResult<T> Conflict(string message, string? code = null) => new()
    {
        Status = ServiceStatus.Conflict,
        ErrorMessage = message,
        ErrorCode = code ?? "CONFLICT"
    };
}