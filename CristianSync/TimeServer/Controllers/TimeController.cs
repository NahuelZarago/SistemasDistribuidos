using Microsoft.AspNetCore.Mvc;

namespace TimeServer.Controllers;

[ApiController]
[Route("api/time")]
public class TimeController : ControllerBase
{
    [HttpGet]
    public IActionResult GetTime()
    {
        // El valor UTC se obtiene inmediatamente antes de generar la respuesta.
        return Ok(new TimeResponse(DateTimeOffset.UtcNow));
    }
}

public record TimeResponse(DateTimeOffset UtcTime);