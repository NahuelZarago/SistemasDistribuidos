using Microsoft.AspNetCore.Mvc;

namespace ClockNode.Controllers;

[ApiController]
[Route("api/clock")]
public sealed class ClockController : ControllerBase
{
    private readonly SimulatedClock _clock;
    private readonly IConfiguration _configuration;

    public ClockController(SimulatedClock clock, IConfiguration configuration)
    {
        _clock = clock;
        _configuration = configuration;
    }

    [HttpGet]
    public IActionResult Get()
    {
        var delay = _configuration.GetValue<int>("RESPONSE_DELAY_MS");
        if (delay < 0) delay = 0;
        return Ok(new
        {
            node = Environment.MachineName,
            utcTime = _clock.Now(),
            offsetMs = _clock.OffsetMilliseconds
        });
    }

    [HttpGet("delayed")]
    public async Task<IActionResult> GetDelayed(CancellationToken ct)
    {
        var delay = Math.Max(0, _configuration.GetValue<int>("RESPONSE_DELAY_MS"));
        if (delay > 0) await Task.Delay(delay, ct);
        return Ok(new
        {
            node = Environment.MachineName,
            utcTime = _clock.Now(),
            offsetMs = _clock.OffsetMilliseconds
        });
    }

    public record AdjustRequest(double DeltaMs);

    [HttpPost("adjust")]
    public IActionResult Adjust([FromBody] AdjustRequest request)
    {
        if (!double.IsFinite(request.DeltaMs) || Math.Abs(request.DeltaMs) > 3_600_000)
            return BadRequest("Corrección fuera del rango permitido.");

        var newOffset = _clock.Adjust(request.DeltaMs);
        return Ok(new { node = Environment.MachineName, newOffsetMs = newOffset });
    }
}