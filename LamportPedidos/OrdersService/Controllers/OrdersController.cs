using System.Net.Http.Json;
using Microsoft.AspNetCore.Mvc;

namespace OrdersService.Controllers;

public record CreateOrderRequest(string OrderId, string ProductId, decimal Amount);

[ApiController]
[Route("api/orders")]
public class OrdersController : ControllerBase
{
    private readonly LamportClock _clock;
    private readonly IHttpClientFactory _clients;
    private readonly ILogger<OrdersController> _logger;

    public OrdersController(LamportClock clock, IHttpClientFactory clients, ILogger<OrdersController> logger)
    {
        _clock = clock;
        _clients = clients;
        _logger = logger;
    }

    private void Log(string name, long time) =>
        _logger.LogInformation("EVENTO {Event} fisico={Now:o} lamport={L}", name, DateTimeOffset.UtcNow, time);

    [HttpPost]
    public async Task<IActionResult> Create([FromBody] CreateOrderRequest order, CancellationToken cancellationToken)
    {
        Log("pedido_recibido", _clock.Tick());

        var inventory = _clients.CreateClient("inventory");
        long inventorySend = _clock.Tick();
        Log("consulta_inventario_enviada", inventorySend);
        using var invRequest = new HttpRequestMessage(HttpMethod.Get, $"/api/inventory/{Uri.EscapeDataString(order.ProductId)}");
        invRequest.Headers.Add("X-Lamport-Time", inventorySend.ToString());
        using var invResponse = await inventory.SendAsync(invRequest, cancellationToken);
        
        long inventoryReplyTime = long.Parse(invResponse.Headers.GetValues("X-Lamport-Time").Single());
        Log("respuesta_inventario_recibida", _clock.Receive(inventoryReplyTime));

        var payments = _clients.CreateClient("payments");
        long paymentSend = _clock.Tick();
        Log("solicitud_pago_enviada", paymentSend);
        using var payRequest = new HttpRequestMessage(HttpMethod.Post, "/api/payments")
        {
            Content = JsonContent.Create(new { order.OrderId, order.Amount })
        };
        payRequest.Headers.Add("X-Lamport-Time", paymentSend.ToString());
        using var payResponse = await payments.SendAsync(payRequest, cancellationToken);
        
        long paymentReplyTime = long.Parse(payResponse.Headers.GetValues("X-Lamport-Time").Single());
        Log("respuesta_pago_recibida", _clock.Receive(paymentReplyTime));

        long confirmed = _clock.Tick();
        Log("pedido_confirmado", confirmed);
        return Ok(new { order.OrderId, Status = "Confirmed", Lamport = confirmed });
    }
}