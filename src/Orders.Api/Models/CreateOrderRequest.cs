using System.ComponentModel.DataAnnotations;

namespace Orders.Api.Models;

public sealed class CreateOrderRequest
{
    [Required]
    [MinLength(1)]
    public string CustomerId { get; init; } = string.Empty;

    [Required]
    [MinLength(1)]
    public string Sku { get; init; } = string.Empty;

    [Range(1, 1_000)]
    public int Quantity { get; init; }
}
