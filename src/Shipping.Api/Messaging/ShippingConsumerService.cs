using System.Text.Json;
using Microsoft.Extensions.Options;
using RabbitMQ.Client;
using RabbitMQ.Client.Events;

namespace Shipping.Api.Messaging;

public sealed class ShippingConsumerService(
    IOptions<RabbitMqOptions> options,
    ShipmentStore shipmentStore,
    ILogger<ShippingConsumerService> logger) : BackgroundService
{
    private readonly RabbitMqOptions _options = options.Value;
    private readonly ShipmentStore _shipmentStore = shipmentStore;
    private readonly ILogger<ShippingConsumerService> _logger = logger;

    private IConnection? _connection;
    private IChannel? _channel;
    private string? _consumerTag;

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                await StartConsumerAsync(stoppingToken);
                await Task.Delay(Timeout.Infinite, stoppingToken);
            }
            catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
            {
                break;
            }
            catch (Exception exception)
            {
                _logger.LogWarning(
                    exception,
                    "Shipping consumer could not connect to RabbitMQ yet. Retrying in 5 seconds.");

                await CleanupAsync();
                await Task.Delay(TimeSpan.FromSeconds(5), stoppingToken);
            }
        }
    }

    public override async Task StopAsync(CancellationToken cancellationToken)
    {
        await CleanupAsync();

        await base.StopAsync(cancellationToken);
    }

    private async Task HandleDeliveryAsync(object sender, BasicDeliverEventArgs args)
    {
        try
        {
            var message = JsonSerializer.Deserialize<OrderSubmittedMessage>(args.Body.Span)
                ?? throw new InvalidOperationException("Received an empty order message.");

            _shipmentStore.Record(message);

            _logger.LogInformation(
                "Processed order {OrderId} for customer {CustomerId}",
                message.OrderId,
                message.CustomerId);

            await _channel!.BasicAckAsync(args.DeliveryTag, false);
        }
        catch (Exception exception)
        {
            _logger.LogError(exception, "Failed to process delivery {DeliveryTag}", args.DeliveryTag);

            if (_channel is not null)
            {
                await _channel.BasicNackAsync(args.DeliveryTag, false, requeue: false);
            }
        }
    }

    private async Task StartConsumerAsync(CancellationToken stoppingToken)
    {
        var factory = new ConnectionFactory
        {
            HostName = _options.HostName,
            Port = _options.Port,
            UserName = _options.UserName,
            Password = _options.Password,
            VirtualHost = _options.VirtualHost,
            AutomaticRecoveryEnabled = true,
            TopologyRecoveryEnabled = true,
            ClientProvidedName = "shipping-api-consumer"
        };

        _connection = await factory.CreateConnectionAsync(stoppingToken);
        _channel = await _connection.CreateChannelAsync(cancellationToken: stoppingToken);

        await _channel.ExchangeDeclareAsync(
            _options.ExchangeName,
            ExchangeType.Direct,
            durable: true,
            autoDelete: false,
            cancellationToken: stoppingToken);

        await _channel.QueueDeclareAsync(
            _options.QueueName,
            durable: true,
            exclusive: false,
            autoDelete: false,
            arguments: null,
            cancellationToken: stoppingToken);

        await _channel.QueueBindAsync(
            _options.QueueName,
            _options.ExchangeName,
            _options.RoutingKey,
            arguments: null,
            cancellationToken: stoppingToken);

        await _channel.BasicQosAsync(0, 1, false, stoppingToken);

        var consumer = new AsyncEventingBasicConsumer(_channel);
        consumer.ReceivedAsync += HandleDeliveryAsync;

        _consumerTag = await _channel.BasicConsumeAsync(
            _options.QueueName,
            autoAck: false,
            consumer,
            stoppingToken);

        _logger.LogInformation(
            "Consuming queue {QueueName} from RabbitMQ at {Host}:{Port}",
            _options.QueueName,
            _options.HostName,
            _options.Port);
    }

    private async Task CleanupAsync()
    {
        if (_channel is not null && !string.IsNullOrWhiteSpace(_consumerTag))
        {
            await _channel.BasicCancelAsync(_consumerTag);
        }

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

        _consumerTag = null;
        _channel = null;
        _connection = null;
    }
}
