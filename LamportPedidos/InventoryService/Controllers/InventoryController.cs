using Microsoft.AspNetCore.Mvc;

namespace InventoryService.Controllers;

[ApiController]
[Route("api/inventory")]
public class InventoryController : ControllerBase
{
    private readonly LamportClock _clock;
    private readonly ILogger<InventoryController> _logger;

    public InventoryController(LamportClock clock, ILogger<InventoryController> logger)
    {
        _clock = clock;
        _logger = logger;
    }

    [HttpGet("{productId}")]
    public IActionResult Check(string productId)
    {
        if (!long.TryParse(Request.Headers["X-Lamport-Time"].ToString(), out long sentTime))
            return BadRequest("Falta el encabezado X-Lamport-Time válido.");

        long received = _clock.Receive(sentTime);
        _logger.LogInformation("EVENTO mensaje_recibido producto={ProductId} fisico={Now:o} lamport={L}", productId, DateTimeOffset.UtcNow, received);

        long checkedAt = _clock.Tick();
        _logger.LogInformation("EVENTO inventario_verificado fisico={Now:o} lamport={L}", DateTimeOffset.UtcNow, checkedAt);

        long responseTime = _clock.Tick();
        Response.Headers["X-Lamport-Time"] = responseTime.ToString();
        _logger.LogInformation("EVENTO respuesta_enviada fisico={Now:o} lamport={L}", DateTimeOffset.UtcNow, responseTime);

        return Ok(new { ProductId = productId, Available = true, Stock = 25 });
    }
}