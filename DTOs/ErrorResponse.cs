namespace ForkliftDirectory.DTOs;

public sealed record ErrorResponse(
    string Message,
    string? Code = null,
    IDictionary<string, string[]>? Errors = null);