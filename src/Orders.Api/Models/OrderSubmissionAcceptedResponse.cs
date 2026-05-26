namespace Orders.Api.Models;

public sealed record OrderSubmissionAcceptedResponse(
    Guid OrderId,
    string Status,
    DateTimeOffset SubmittedAtUtc,
    string CustomerId,
    string Sku,
    int Quantity);
