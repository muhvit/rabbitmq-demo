namespace Shipping.Api.Models;

public sealed record ShipmentRecord(
    Guid OrderId,
    string CustomerId,
    string Sku,
    int Quantity,
    string Status,
    DateTimeOffset ProcessedAtUtc,
    string Transport);
