using Microsoft.AspNetCore.Mvc;
using Orders.Api.Messaging;
using Orders.Api.Models;

namespace Orders.Api.Controllers;

[ApiController]
[Route("api/[controller]")]
public sealed class OrdersController(IOrderPublisher orderPublisher) : ControllerBase
{
    [HttpPost]
    [ProducesResponseType<OrderSubmissionAcceptedResponse>(StatusCodes.Status202Accepted)]
    public async Task<ActionResult<OrderSubmissionAcceptedResponse>> SubmitOrder(
        [FromBody] CreateOrderRequest request,
        CancellationToken cancellationToken)
    {
        var submittedAtUtc = DateTimeOffset.UtcNow;
        var orderId = Guid.NewGuid();

        var message = new OrderSubmittedMessage(
            orderId,
            request.CustomerId,
            request.Sku,
            request.Quantity,
            submittedAtUtc);

        await orderPublisher.PublishAsync(message, cancellationToken);

        var response = new OrderSubmissionAcceptedResponse(
            orderId,
            "Queued",
            submittedAtUtc,
            request.CustomerId,
            request.Sku,
            request.Quantity);

        return AcceptedAtAction(nameof(GetOrderStatusHint), new { orderId }, response);
    }

    [HttpGet("{orderId:guid}")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    public ActionResult<object> GetOrderStatusHint(Guid orderId)
    {
        return Ok(new
        {
            orderId,
            nextStep = "Query Shipping.Api to inspect whether the queued order has been processed."
        });
    }
}
