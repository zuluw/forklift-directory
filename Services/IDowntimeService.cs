using ForkliftDirectory.DTOs;

namespace ForkliftDirectory.Services;

public interface IDowntimeService
{
    Task<ServiceResult<IReadOnlyList<DowntimeResponseDto>>> GetByForkliftIdAsync(int forkliftId, CancellationToken ct);

    Task<ServiceResult<DowntimeResponseDto>> CreateAsync(int forkliftId, CreateDowntimeRequestDto request, CancellationToken ct);

    Task<ServiceResult<DowntimeResponseDto>> UpdateAsync(int id, UpdateDowntimeRequestDto request, CancellationToken ct);

    Task<ServiceResult> DeleteAsync(int id, CancellationToken ct);
}