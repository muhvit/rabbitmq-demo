using System.Text.Json;
using Microsoft.Extensions.Options;
using RabbitMQ.Client;

namespace Orders.Api.Messaging;

public sealed class RabbitMqPublisher(
    IOptions<RabbitMqOptions> options,
    ILogger<RabbitMqPublisher> logger) : IOrderPublisher, IAsyncDisposable
{
    private readonly RabbitMqOptions _options = options.Value;
    private readonly ILogger<RabbitMqPublisher> _logger = logger;
    private readonly SemaphoreSlim _sync = new(1, 1);

    private IConnection? _connection;
    private IChannel? _channel;

    public async Task PublishAsync(OrderSubmittedMessage message, CancellationToken cancellationToken)
    {
        await EnsureChannelAsync(cancellationToken);

        var body = JsonSerializer.SerializeToUtf8Bytes(message);
        var properties = new BasicProperties
        {
            ContentType = "application/json",
            DeliveryMode = DeliveryModes.Persistent,
            MessageId = message.OrderId.ToString(),
            Type = nameof(OrderSubmittedMessage)
        };

        await _channel!.BasicPublishAsync(
            _options.ExchangeName,
            _options.RoutingKey,
            true,
            properties,
            body,
            cancellationToken);

        _logger.LogInformation(
            "Queued order {OrderId} for customer {CustomerId} and sku {Sku}",
            message.OrderId,
            message.CustomerId,
            message.Sku);
    }

    public async ValueTask DisposeAsync()
    {
        if (_channel is not null)
        {
            await _channel.CloseAsync();
            await _channel.DisposeAsync();
        }

        if (_connection is not null)
        {
            await _connection.CloseAsync();
            await _connection.DisposeAsync();
        }

        _sync.Dispose();
    }

    private async Task EnsureChannelAsync(CancellationToken cancellationToken)
    {
        if (_channel is not null)
        {
            return;
        }

        await _sync.WaitAsync(cancellationToken);

        try
        {
            if (_channel is not null)
            {
                return;
            }

            var factory = new ConnectionFactory
            {
                HostName = _options.HostName,
                Port = _options.Port,
                UserName = _options.UserName,
                Password = _options.Password,
                VirtualHost = _options.VirtualHost,
                AutomaticRecoveryEnabled = true,
                TopologyRecoveryEnabled = true,
                ClientProvidedName = "orders-api-publisher"
            };

            _connection = await factory.CreateConnectionAsync(cancellationToken);
            _channel = await _connection.CreateChannelAsync(cancellationToken: cancellationToken);

            await _channel.ExchangeDeclareAsync(
                _options.ExchangeName,
                ExchangeType.Direct,
                durable: true,
                autoDelete: false,
                cancellationToken: cancellationToken);

            await _channel.QueueDeclareAsync(
                _options.QueueName,
                durable: true,
                exclusive: false,
                autoDelete: false,
                arguments: null,
                cancellationToken: cancellationToken);

            await _channel.QueueBindAsync(
                _options.QueueName,
                _options.ExchangeName,
                _options.RoutingKey,
                arguments: null,
                cancellationToken: cancellationToken);

            _logger.LogInformation(
                "Connected to RabbitMQ at {Host}:{Port} and declared topology",
                _options.HostName,
                _options.Port);
        }
        finally
        {
            _sync.Release();
        }
    }
}
