using ForkliftDirectory.DTOs;
using ForkliftDirectory.Services;
using Microsoft.AspNetCore.Mvc;

namespace ForkliftDirectory.Controllers;

[ApiController]
[Route("api/forklifts")]
public sealed class ForkliftsController : ControllerBase
{
    private readonly IForkliftService _forkliftService;

    public ForkliftsController(IForkliftService forkliftService)
    {
        _forkliftService = forkliftService;
    }

    [HttpGet]
    public async Task<ActionResult<IReadOnlyList<ForkliftResponseDto>>> GetAll(
        [FromQuery] string? search,
        CancellationToken ct)
    {
        var result = await _forkliftService.GetAllAsync(search, ct);
        return Ok(result);
    }

    [HttpGet("{id:int}")]
    public async Task<IActionResult> GetById(int id, CancellationToken ct)
    {
        var result = await _forkliftService.GetByIdAsync(id, ct);
        return ToActionResult(result);
    }

    [HttpPost]
    public async Task<IActionResult> Create([FromBody] CreateForkliftRequestDto request, CancellationToken ct)
    {
        var result = await _forkliftService.CreateAsync(request, ct);
        if (result.Status == ServiceStatus.Created)
        {
            return CreatedAtAction(nameof(GetById), new { id = result.Data!.Id }, result.Data);
        }

        return ToActionResult(result);
    }

    [HttpPut("{id:int}")]
    public async Task<IActionResult> Update(int id, [FromBody] UpdateForkliftRequestDto request, CancellationToken ct)
    {
        var result = await _forkliftService.UpdateAsync(id, request, ct);
        return ToActionResult(result);
    }

    [HttpDelete("{id:int}")]
    public async Task<IActionResult> Delete(int id, CancellationToken ct)
    {
        var result = await _forkliftService.DeleteAsync(id, ct);
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