namespace Shipping.Api.Messaging;

public sealed record OrderSubmittedMessage(
    Guid OrderId,
    string CustomerId,
    string Sku,
    int Quantity,
    DateTimeOffset SubmittedAtUtc);
