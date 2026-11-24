using ForkliftDirectory.Data;
using ForkliftDirectory.DTOs;
using ForkliftDirectory.Models;
using Microsoft.EntityFrameworkCore;

namespace ForkliftDirectory.Services;

public sealed class ForkliftService : IForkliftService
{
    private const string DefaultUserName = "Иванов И.И.";
    private const decimal MaxCapacity = 9999999.999m;

    private readonly AppDbContext _db;

    public ForkliftService(AppDbContext db)
    {
        _db = db;
    }

    public async Task<IReadOnlyList<ForkliftResponseDto>> GetAllAsync(string? search, CancellationToken ct)
    {
        IQueryable<Forklift> query = _db.Forklifts.AsNoTracking();

        if (!string.IsNullOrWhiteSpace(search))
        {
            var pattern = EscapeLikePattern(search.Trim());
            query = query.Where(f => EF.Functions.ILike(f.Number, $"%{pattern}%", "\\"));
        }

        var rows = await query
            .OrderBy(f => f.Id)
            .Select(f => new
            {
                f.Id,
                f.Brand,
                f.Number,
                f.Capacity,
                f.IsActive,
                f.UpdatedAt,
                f.UpdatedBy,
                DowntimesCount = f.Downtimes.Count
            })
            .ToListAsync(ct);

        return rows
            .Select(r => new ForkliftResponseDto
            {
                Id = r.Id,
                Brand = r.Brand,
                Number = r.Number,
                Capacity = r.Capacity,
                IsActive = r.IsActive,
                UpdatedAt = DateTime.SpecifyKind(r.UpdatedAt, DateTimeKind.Utc),
                UpdatedBy = r.UpdatedBy,
                DowntimesCount = r.DowntimesCount
            })
            .ToList();
    }

    public async Task<ServiceResult<ForkliftResponseDto>> GetByIdAsync(int id, CancellationToken ct)
    {
        var row = await _db.Forklifts
            .AsNoTracking()
            .Where(f => f.Id == id)
            .Select(f => new
            {
                f.Id,
                f.Brand,
                f.Number,
                f.Capacity,
                f.IsActive,
                f.UpdatedAt,
                f.UpdatedBy,
                DowntimesCount = f.Downtimes.Count
            })
            .FirstOrDefaultAsync(ct);

        if (row is null)
        {
            return ServiceResult<ForkliftResponseDto>.NotFound($"Погрузчик с кодом {id} не найден.");
        }

        return ServiceResult<ForkliftResponseDto>.Ok(new ForkliftResponseDto
        {
            Id = row.Id,
            Brand = row.Brand,
            Number = row.Number,
            Capacity = row.Capacity,
            IsActive = row.IsActive,
            UpdatedAt = DateTime.SpecifyKind(row.UpdatedAt, DateTimeKind.Utc),
            UpdatedBy = row.UpdatedBy,
            DowntimesCount = row.DowntimesCount
        });
    }

    public async Task<ServiceResult<ForkliftResponseDto>> CreateAsync(CreateForkliftRequestDto request, CancellationToken ct)
    {
        var validationError = ValidateAndNormalize(
            request.Brand,
            request.Number,
            request.Capacity,
            request.UpdatedBy,
            out var brand,
            out var number,
            out var capacity,
            out var updatedBy);

        if (validationError is not null)
        {
            return validationError;
        }

        var entity = new Forklift
        {
            Brand = brand,
            Number = number,
            Capacity = capacity,
            IsActive = request.IsActive,
            UpdatedAt = DateTime.UtcNow,
            UpdatedBy = updatedBy
        };

        _db.Forklifts.Add(entity);
        await _db.SaveChangesAsync(ct);

        return ServiceResult<ForkliftResponseDto>.Created(MapToDto(entity, downtimesCount: 0));
    }

    public async Task<ServiceResult<ForkliftResponseDto>> UpdateAsync(int id, UpdateForkliftRequestDto request, CancellationToken ct)
    {
        var entity = await _db.Forklifts.FirstOrDefaultAsync(f => f.Id == id, ct);
        if (entity is null)
        {
            return ServiceResult<ForkliftResponseDto>.NotFound($"Погрузчик с кодом {id} не найден.");
        }

        var validationError = ValidateAndNormalize(
            request.Brand,
            request.Number,
            request.Capacity,
            request.UpdatedBy,
            out var brand,
            out var number,
            out var capacity,
            out var updatedBy);

        if (validationError is not null)
        {
            return validationError;
        }

        entity.Brand = brand;
        entity.Number = number;
        entity.Capacity = capacity;
        entity.IsActive = request.IsActive;
        entity.UpdatedAt = DateTime.UtcNow;
        entity.UpdatedBy = updatedBy;

        await _db.SaveChangesAsync(ct);

        var downtimesCount = await _db.Downtimes.CountAsync(d => d.ForkliftId == id, ct);
        return ServiceResult<ForkliftResponseDto>.Ok(MapToDto(entity, downtimesCount));
    }

    public async Task<ServiceResult> DeleteAsync(int id, CancellationToken ct)
    {
        var entity = await _db.Forklifts.FirstOrDefaultAsync(f => f.Id == id, ct);
        if (entity is null)
        {
            return ServiceResult.NotFound($"Погрузчик с кодом {id} не найден.");
        }

        var hasDowntimes = await _db.Downtimes.AnyAsync(d => d.ForkliftId == id, ct);
        if (hasDowntimes)
        {
            return ServiceResult.Conflict(
                "Удаление запрещено: для данного погрузчика зарегистрированы простои.",
                "FORKLIFT_HAS_DOWNTIMES");
        }

        _db.Forklifts.Remove(entity);

        try
        {
            await _db.SaveChangesAsync(ct);
            return ServiceResult.NoContent();
        }
        catch (DbUpdateException)
        {
            return ServiceResult.Conflict(
                "Удаление запрещено: для данного погрузчика зарегистрированы простои.",
                "FORKLIFT_HAS_DOWNTIMES");
        }
    }

    private static ServiceResult<ForkliftResponseDto>? ValidateAndNormalize(
        string? rawBrand,
        string? rawNumber,
        decimal? rawCapacity,
        string? rawUpdatedBy,
        out string brand,
        out string number,
        out decimal capacity,
        out string updatedBy)
    {
        brand = rawBrand?.Trim() ?? string.Empty;
        number = rawNumber?.Trim() ?? string.Empty;
        capacity = rawCapacity ?? 0m;
        updatedBy = string.IsNullOrWhiteSpace(rawUpdatedBy) ? DefaultUserName : rawUpdatedBy.Trim();

        var errors = new Dictionary<string, string[]>(StringComparer.OrdinalIgnoreCase);

        if (string.IsNullOrWhiteSpace(brand))
        {
            errors["brand"] = ["Поле «Марка» обязательно для заполнения."];
        }
        else if (brand.Length > 150)
        {
            errors["brand"] = ["Длина поля «Марка» не должна превышать 150 символов."];
        }

        if (string.IsNullOrWhiteSpace(number))
        {
            errors["number"] = ["Поле «Номер» обязательно для заполнения."];
        }
        else if (number.Length > 100)
        {
            errors["number"] = ["Длина поля «Номер» не должна превышать 100 символов."];
        }

        if (!rawCapacity.HasValue)
        {
            errors["capacity"] = ["Поле «Грузоподъемность» обязательно для заполнения."];
        }
        else if (rawCapacity.Value <= 0m)
        {
            errors["capacity"] = ["Поле «Грузоподъемность» должно быть больше 0."];
        }
        else if (rawCapacity.Value > MaxCapacity)
        {
            errors["capacity"] = [$"Значение поля «Грузоподъемность» не должно превышать {MaxCapacity}."];
        }
        else if (decimal.Round(rawCapacity.Value, 3) != rawCapacity.Value)
        {
            errors["capacity"] = ["Поле «Грузоподъемность» должно иметь точность не более 3 знаков после запятой."];
        }

        if (updatedBy.Length > 150)
        {
            errors["updatedBy"] = ["Длина ФИО пользователя не должна превышать 150 символов."];
        }

        if (errors.Count > 0)
        {
            var firstError = errors.Values.SelectMany(v => v).First();
            return ServiceResult<ForkliftResponseDto>.BadRequest(firstError, errors);
        }

        return null;
    }

    private static ForkliftResponseDto MapToDto(Forklift entity, int downtimesCount) => new()
    {
        Id = entity.Id,
        Brand = entity.Brand,
        Number = entity.Number,
        Capacity = entity.Capacity,
        IsActive = entity.IsActive,
        UpdatedAt = DateTime.SpecifyKind(entity.UpdatedAt, DateTimeKind.Utc),
        UpdatedBy = entity.UpdatedBy,
        DowntimesCount = downtimesCount
    };

    private static string EscapeLikePattern(string input)
    {
        return input
            .Replace("\\", "\\\\")
            .Replace("%", "\\%")
            .Replace("_", "\\_");
    }
}