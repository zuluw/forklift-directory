using ForkliftDirectory.DTOs;
using ForkliftDirectory.Services;
using Microsoft.AspNetCore.Mvc;

namespace ForkliftDirectory.Controllers;

[ApiController]
[Route("api")]
public sealed class DowntimesController : ControllerBase
{
    private readonly IDowntimeService _downtimeService;

    public DowntimesController(IDowntimeService downtimeService)
    {
        _downtimeService = downtimeService;
    }

    [HttpGet("forklifts/{forkliftId:int}/downtimes")]
    public async Task<IActionResult> GetByForkliftId(int forkliftId, CancellationToken ct)
    {
        var result = await _downtimeService.GetByForkliftIdAsync(forkliftId, ct);
        return ToActionResult(result);
    }

    [HttpPost("forklifts/{forkliftId:int}/downtimes")]
    public async Task<IActionResult> Create(
        int forkliftId,
        [FromBody] CreateDowntimeRequestDto request,
        CancellationToken ct)
    {
        var result = await _downtimeService.CreateAsync(forkliftId, request, ct);
        return ToActionResult(result);
    }

    [HttpPut("downtimes/{id:int}")]
    public async Task<IActionResult> Update(
        int id,
        [FromBody] UpdateDowntimeRequestDto request,
        CancellationToken ct)
    {
        var result = await _downtimeService.UpdateAsync(id, request, ct);
        return ToActionResult(result);
    }

    [HttpDelete("downtimes/{id:int}")]
    public async Task<IActionResult> Delete(int id, CancellationToken ct)
    {
        var result = await _downtimeService.DeleteAsync(id, ct);
        return ToActionResult(result);
    }

    private IActionResult ToActionResult<T>(ServiceResult<T> result) => result.Status switch
    {
        ServiceStatus.Ok => Ok(result.Data),
        ServiceStatus.Created => StatusCode(StatusCodes.Status201Created, result.Data),
        ServiceStatus.BadRequest => BadRequest(new ErrorResponse(result.ErrorMessage!, result.ErrorCode, result.Errors)),
        ServiceStatus.NotFound => NotFound(new ErrorResponse(result.ErrorMessage!, result.ErrorCode)),
        ServiceStatus.Conflict => Conflict(new ErrorResponse(result.ErrorMessage!, result.ErrorCode)),
        _ => StatusCode(StatusCodes.Status500InternalServerError, new ErrorResponse("Неизвестная ошибка."))
    };

    private IActionResult ToActionResult(ServiceResult result) => result.Status switch
    {
        ServiceStatus.NoContent => NoContent(),
        ServiceStatus.BadRequest => BadRequest(new ErrorResponse(result.ErrorMessage!, result.ErrorCode, result.Errors)),
        ServiceStatus.NotFound => NotFound(new ErrorResponse(result.ErrorMessage!, result.ErrorCode)),
        ServiceStatus.Conflict => Conflict(new ErrorResponse(result.ErrorMessage!, result.ErrorCode)),
        _ => StatusCode(StatusCodes.Status500InternalServerError, new ErrorResponse("Неизвестная ошибка."))
    };
}