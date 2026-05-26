using Microsoft.AspNetCore.Mvc;
using Shipping.Api.Messaging;
using Shipping.Api.Models;

namespace Shipping.Api.Controllers;

[ApiController]
[Route("api/[controller]")]
public sealed class ShipmentsController(ShipmentStore shipmentStore) : ControllerBase
{
    [HttpGet]
    [ProducesResponseType<IReadOnlyCollection<ShipmentRecord>>(StatusCodes.Status200OK)]
    public ActionResult<IReadOnlyCollection<ShipmentRecord>> GetAll()
    {
        return Ok(shipmentStore.GetAll());
    }

    [HttpGet("{orderId:guid}")]
    [ProducesResponseType<ShipmentRecord>(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public ActionResult<ShipmentRecord> GetByOrderId(Guid orderId)
    {
        var shipment = shipmentStore.GetByOrderId(orderId);
        return shipment is null ? NotFound() : Ok(shipment);
    }
}
