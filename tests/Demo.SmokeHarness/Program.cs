using System.Net;
using System.Net.Http.Json;
using System.Text.Json;

var options = SmokeHarnessOptions.Parse(args);
using var httpClient = new HttpClient
{
    Timeout = TimeSpan.FromSeconds(10)
};

var createOrderRequest = new CreateOrderRequest(
    CustomerId: "demo-customer",
    Sku: $"sku-{DateTimeOffset.UtcNow:yyyyMMddHHmmss}",
    Quantity: 3);

Console.WriteLine($"Posting an order to {options.OrdersApiBaseUrl} ...");

using var orderResponse = await httpClient.PostAsJsonAsync(
    new Uri(options.OrdersApiBaseUrl, "/api/orders"),
    createOrderRequest);

orderResponse.EnsureSuccessStatusCode();

var orderAccepted = await orderResponse.Content.ReadFromJsonAsync<OrderSubmissionAcceptedResponse>(
    SmokeHarnessOptions.JsonSerializerOptions)
    ?? throw new InvalidOperationException("Orders API returned an empty response body.");

Console.WriteLine($"Order {orderAccepted.OrderId} queued at {orderAccepted.SubmittedAtUtc:O}.");
Console.WriteLine($"Polling {options.ShippingApiBaseUrl} for shipment processing ...");

var deadline = DateTimeOffset.UtcNow.Add(options.Timeout);
while (DateTimeOffset.UtcNow < deadline)
{
    using var shipmentResponse = await httpClient.GetAsync(
        new Uri(options.ShippingApiBaseUrl, $"/api/shipments/{orderAccepted.OrderId}"));

    if (shipmentResponse.StatusCode == HttpStatusCode.OK)
    {
        var shipment = await shipmentResponse.Content.ReadFromJsonAsync<ShipmentRecord>(
            SmokeHarnessOptions.JsonSerializerOptions)
            ?? throw new InvalidOperationException("Shipping API returned an empty shipment response.");

        Console.WriteLine(
            $"Shipment for order {shipment.OrderId} processed at {shipment.ProcessedAtUtc:O} via {shipment.Transport}.");
        return;
    }

    if (shipmentResponse.StatusCode != HttpStatusCode.NotFound)
    {
        var payload = await shipmentResponse.Content.ReadAsStringAsync();
        throw new InvalidOperationException(
            $"Unexpected response from Shipping API: {(int)shipmentResponse.StatusCode} {shipmentResponse.StatusCode}. {payload}");
    }

    await Task.Delay(options.PollInterval);
}

throw new TimeoutException(
    $"Timed out after {options.Timeout.TotalSeconds} seconds waiting for Shipping API to process order {orderAccepted.OrderId}.");

internal sealed record CreateOrderRequest(string CustomerId, string Sku, int Quantity);

internal sealed record OrderSubmissionAcceptedResponse(
    Guid OrderId,
    string Status,
    DateTimeOffset SubmittedAtUtc,
    string CustomerId,
    string Sku,
    int Quantity);

internal sealed record ShipmentRecord(
    Guid OrderId,
    string CustomerId,
    string Sku,
    int Quantity,
    string Status,
    DateTimeOffset ProcessedAtUtc,
    string Transport);

internal sealed record SmokeHarnessOptions(
    Uri OrdersApiBaseUrl,
    Uri ShippingApiBaseUrl,
    TimeSpan Timeout,
    TimeSpan PollInterval)
{
    public static readonly JsonSerializerOptions JsonSerializerOptions = new(JsonSerializerDefaults.Web);

    public static SmokeHarnessOptions Parse(IReadOnlyList<string> args)
    {
        var ordersApiBaseUrl = new Uri("http://orders.localtest.me:8080");
        var shippingApiBaseUrl = new Uri("http://shipping.localtest.me:8080");
        var timeout = TimeSpan.FromSeconds(45);
        var pollInterval = TimeSpan.FromSeconds(2);

        for (var index = 0; index < args.Count; index++)
        {
            switch (args[index])
            {
                case "--orders-url":
                    ordersApiBaseUrl = new Uri(args[++index], UriKind.Absolute);
                    break;
                case "--shipping-url":
                    shippingApiBaseUrl = new Uri(args[++index], UriKind.Absolute);
                    break;
                case "--timeout-seconds":
                    timeout = TimeSpan.FromSeconds(int.Parse(args[++index]));
                    break;
                case "--poll-interval-seconds":
                    pollInterval = TimeSpan.FromSeconds(int.Parse(args[++index]));
                    break;
            }
        }

        return new SmokeHarnessOptions(ordersApiBaseUrl, shippingApiBaseUrl, timeout, pollInterval);
    }
}
