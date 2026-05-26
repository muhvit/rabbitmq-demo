namespace Orders.Api.Messaging;

public interface IOrderPublisher
{
    Task PublishAsync(OrderSubmittedMessage message, CancellationToken cancellationToken);
}
