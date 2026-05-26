namespace Orders.Api.Messaging;

public sealed class RabbitMqOptions
{
    public const string SectionName = "RabbitMq";

    public string HostName { get; set; } = "localhost";

    public int Port { get; set; } = 5672;

    public string UserName { get; set; } = "demo";

    public string Password { get; set; } = "demo-password";

    public string VirtualHost { get; set; } = "/";

    public string ExchangeName { get; set; } = "orders.exchange";

    public string QueueName { get; set; } = "shipping.orders";

    public string RoutingKey { get; set; } = "orders.submitted";
}
