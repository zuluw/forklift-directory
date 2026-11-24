using ForkliftDirectory.DTOs;

namespace ForkliftDirectory.Services;

public interface IForkliftService
{
    Task<IReadOnlyList<ForkliftResponseDto>> GetAllAsync(string? search, CancellationToken ct);

    Task<ServiceResult<ForkliftResponseDto>> GetByIdAsync(int id, CancellationToken ct);

    Task<ServiceResult<ForkliftResponseDto>> CreateAsync(CreateForkliftRequestDto request, CancellationToken ct);

    Task<ServiceResult<ForkliftResponseDto>> UpdateAsync(int id, UpdateForkliftRequestDto request, CancellationToken ct);

    Task<ServiceResult> DeleteAsync(int id, CancellationToken ct);
}