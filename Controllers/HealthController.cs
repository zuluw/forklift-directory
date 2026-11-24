using ForkliftDirectory.Data;
using Microsoft.AspNetCore.Mvc;

namespace ForkliftDirectory.Controllers;

[ApiController]
[Route("api/health")]
public sealed class HealthController : ControllerBase
{
    private readonly AppDbContext _db;

    public HealthController(AppDbContext db)
    {
        _db = db;
    }

    [HttpGet]
    public async Task<IActionResult> Check(CancellationToken ct)
    {
        var canConnect = await _db.Database.CanConnectAsync(ct);
        if (!canConnect)
        {
            return StatusCode(StatusCodes.Status503ServiceUnavailable, new { status = "unhealthy" });
        }

        return Ok(new { status = "ok" });
    }
}