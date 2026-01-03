using System.Text.Json;
using Chord.Messaging.RabitMQ.Messaging;
using Microsoft.AspNetCore.Mvc;
using Service.Orders.Models;

namespace Service.Orders.Controllers;

[ApiController]
[Route("api/[controller]")]
public class OrdersController : ControllerBase
{
    private readonly IChordFlowMessenger _flowMessenger;

    public OrdersController(IChordFlowMessenger flowMessenger)
    {
        _flowMessenger = flowMessenger;
    }

    [HttpPost]
    [ProducesResponseType(StatusCodes.Status202Accepted)]
    public async Task<IActionResult> CreateOrder([FromBody] OrderRequest request, CancellationToken cancellationToken)
    {
        var payload = JsonSerializer.Serialize(request);
        await _flowMessenger.StartAsync(payload, cancellationToken);
        return Accepted(new { status = "queued", request.OrderId });
    }
}
