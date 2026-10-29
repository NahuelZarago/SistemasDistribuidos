using Microsoft.AspNetCore.Mvc;

namespace PaymentsService.Controllers;

public record PaymentRequest(string OrderId, decimal Amount);

[ApiController]
[Route("api/payments")]
public class PaymentsController : ControllerBase
{
    private readonly LamportClock _clock;
    private readonly ILogger<PaymentsController> _logger;

    public PaymentsController(LamportClock clock, ILogger<PaymentsController> logger)
    {
        _clock = clock;
        _logger = logger;
    }

    [HttpPost]
    public IActionResult Pay([FromBody] PaymentRequest request)
    {
        if (!long.TryParse(Request.Headers["X-Lamport-Time"].ToString(), out long sentTime))
            return BadRequest("Falta el encabezado X-Lamport-Time válido.");

        long received = _clock.Receive(sentTime);
        _logger.LogInformation("EVENTO pago_recibido orden={OrderId} fisico={Now:o} lamport={L}", request.OrderId, DateTimeOffset.UtcNow, received);

        long processed = _clock.Tick();
        _logger.LogInformation("EVENTO pago_procesado fisico={Now:o} lamport={L}", DateTimeOffset.UtcNow, processed);

        long responseTime = _clock.Tick();
        Response.Headers["X-Lamport-Time"] = responseTime.ToString();
        _logger.LogInformation("EVENTO respuesta_enviada fisico={Now:o} lamport={L}", DateTimeOffset.UtcNow, responseTime);

        return Ok(new { request.OrderId, Status = "Approved", request.Amount });
    }
}