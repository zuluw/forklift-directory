using ForkliftDirectory.Data;
using ForkliftDirectory.DTOs;
using ForkliftDirectory.Models;
using Microsoft.EntityFrameworkCore;

namespace ForkliftDirectory.Services;

public sealed class DowntimeService : IDowntimeService
{
    private readonly AppDbContext _db;

    public DowntimeService(AppDbContext db)
    {
        _db = db;
    }

    public async Task<ServiceResult<IReadOnlyList<DowntimeResponseDto>>> GetByForkliftIdAsync(
        int forkliftId,
        CancellationToken ct)
    {
        var forkliftExists = await _db.Forklifts.AnyAsync(f => f.Id == forkliftId, ct);
        if (!forkliftExists)
        {
            return ServiceResult<IReadOnlyList<DowntimeResponseDto>>.NotFound(
                $"Погрузчик с кодом {forkliftId} не найден.");
        }

        var items = await _db.Downtimes
            .AsNoTracking()
            .Where(d => d.ForkliftId == forkliftId)
            .OrderByDescending(d => d.StartTime)
            .ThenByDescending(d => d.Id)
            .ToListAsync(ct);

        var nowUtc = DateTime.UtcNow;
        var dtos = items.Select(d => MapToDto(d, nowUtc)).ToList();

        return ServiceResult<IReadOnlyList<DowntimeResponseDto>>.Ok(dtos);
    }

    public async Task<ServiceResult<DowntimeResponseDto>> CreateAsync(
        int forkliftId,
        CreateDowntimeRequestDto request,
        CancellationToken ct)
    {
        var forkliftExists = await _db.Forklifts.AnyAsync(f => f.Id == forkliftId, ct);
        if (!forkliftExists)
        {
            return ServiceResult<DowntimeResponseDto>.NotFound($"Погрузчик с кодом {forkliftId} не найден.");
        }

        var validationError = ValidateAndNormalize(
            request.StartTime,
            request.EndTime,
            request.Description,
            out var startUtc,
            out var endUtc,
            out var description);

        if (validationError is not null)
        {
            return validationError;
        }

        var entity = new Downtime
        {
            ForkliftId = forkliftId,
            StartTime = startUtc,
            EndTime = endUtc,
            Description = description
        };

        _db.Downtimes.Add(entity);
        await _db.SaveChangesAsync(ct);

        return ServiceResult<DowntimeResponseDto>.Created(MapToDto(entity, DateTime.UtcNow));
    }

    public async Task<ServiceResult<DowntimeResponseDto>> UpdateAsync(
        int id,
        UpdateDowntimeRequestDto request,
        CancellationToken ct)
    {
        var entity = await _db.Downtimes.FirstOrDefaultAsync(d => d.Id == id, ct);
        if (entity is null)
        {
            return ServiceResult<DowntimeResponseDto>.NotFound($"Запись о простое с кодом {id} не найдена.");
        }

        var validationError = ValidateAndNormalize(
            request.StartTime,
            request.EndTime,
            request.Description,
            out var startUtc,
            out var endUtc,
            out var description);

        if (validationError is not null)
        {
            return validationError;
        }

        entity.StartTime = startUtc;
        entity.EndTime = endUtc;
        entity.Description = description;

        await _db.SaveChangesAsync(ct);

        return ServiceResult<DowntimeResponseDto>.Ok(MapToDto(entity, DateTime.UtcNow));
    }

    public async Task<ServiceResult> DeleteAsync(int id, CancellationToken ct)
    {
        var entity = await _db.Downtimes.FirstOrDefaultAsync(d => d.Id == id, ct);
        if (entity is null)
        {
            return ServiceResult.NotFound($"Запись о простое с кодом {id} не найдена.");
        }

        _db.Downtimes.Remove(entity);
        await _db.SaveChangesAsync(ct);

        return ServiceResult.NoContent();
    }

    private static ServiceResult<DowntimeResponseDto>? ValidateAndNormalize(
        DateTime? rawStartTime,
        DateTime? rawEndTime,
        string? rawDescription,
        out DateTime startUtc,
        out DateTime? endUtc,
        out string description)
    {
        startUtc = default;
        endUtc = null;
        description = rawDescription?.Trim() ?? string.Empty;

        var errors = new Dictionary<string, string[]>(StringComparer.OrdinalIgnoreCase);

        if (!rawStartTime.HasValue || rawStartTime.Value == DateTime.MinValue)
        {
            errors["startTime"] = ["Дата и время начала обязательны для заполнения."];
        }
        else
        {
            startUtc = NormalizeToUtc(rawStartTime.Value);
        }

        if (rawEndTime.HasValue && rawEndTime.Value != DateTime.MinValue)
        {
            endUtc = NormalizeToUtc(rawEndTime.Value);
            if (!errors.ContainsKey("startTime") && endUtc.Value < startUtc)
            {
                errors["endTime"] = ["Дата и время окончания не могут быть раньше даты и времени начала."];
            }
        }

        if (string.IsNullOrWhiteSpace(description))
        {
            errors["description"] = ["Описание инцидента обязательно для заполнения."];
        }
        else if (description.Length > 2000)
        {
            errors["description"] = ["Длина описания инцидента не должна превышать 2000 символов."];
        }

        if (errors.Count > 0)
        {
            var firstError = errors.Values.SelectMany(v => v).First();
            return ServiceResult<DowntimeResponseDto>.BadRequest(firstError, errors);
        }

        return null;
    }

    private static DateTime NormalizeToUtc(DateTime dt)
    {
        return dt.Kind switch
        {
            DateTimeKind.Utc => dt,
            DateTimeKind.Local => dt.ToUniversalTime(),
            DateTimeKind.Unspecified => DateTime.SpecifyKind(dt, DateTimeKind.Utc),
            _ => DateTime.SpecifyKind(dt, DateTimeKind.Utc)
        };
    }

    private static DowntimeResponseDto MapToDto(Downtime entity, DateTime nowUtc)
    {
        var startUtc = DateTime.SpecifyKind(entity.StartTime, DateTimeKind.Utc);
        DateTime? endUtc = entity.EndTime.HasValue
            ? DateTime.SpecifyKind(entity.EndTime.Value, DateTimeKind.Utc)
            : null;

        var effectiveEnd = endUtc ?? nowUtc;
        var totalMinutes = Math.Max(0, (long)Math.Floor((effectiveEnd - startUtc).TotalMinutes));
        var hours = totalMinutes / 60;
        var minutes = totalMinutes % 60;

        return new DowntimeResponseDto
        {
            Id = entity.Id,
            ForkliftId = entity.ForkliftId,
            StartTime = startUtc,
            EndTime = endUtc,
            IsActive = !endUtc.HasValue,
            DurationMinutes = totalMinutes,
            DurationFormatted = $"{hours}ч {minutes:00} мин",
            Description = entity.Description
        };
    }
}