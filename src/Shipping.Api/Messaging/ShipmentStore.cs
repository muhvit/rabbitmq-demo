using System.Collections.Concurrent;
using Shipping.Api.Models;

namespace Shipping.Api.Messaging;

public sealed class ShipmentStore
{
    private readonly ConcurrentDictionary<Guid, ShipmentRecord> _shipments = new();

    public void Record(OrderSubmittedMessage message)
    {
        var shipment = new ShipmentRecord(
            message.OrderId,
            message.CustomerId,
            message.Sku,
            message.Quantity,
            "Processed",
            DateTimeOffset.UtcNow,
            "rabbitmq");

        _shipments[message.OrderId] = shipment;
    }

    public IReadOnlyCollection<ShipmentRecord> GetAll()
    {
        return _shipments.Values
            .OrderByDescending(item => item.ProcessedAtUtc)
            .ToArray();
    }

    public ShipmentRecord? GetByOrderId(Guid orderId)
    {
        return _shipments.TryGetValue(orderId, out var shipment)
            ? shipment
            : null;
    }
}
