using Messaging.Abstractions;
using Messaging.Events;
using Microsoft.Extensions.Logging;

namespace Messaging;

public sealed class NullEventPublisher : IEventPublisher
{
    public NullEventPublisher(ILogger<NullEventPublisher> logger)
    {
        logger.LogInformation("NullEventPublisher is used");
    }

    public Task<bool> PublishAsync<TEvent>(TEvent @event) where TEvent : IntegrationEvent
    {
        return Task.FromResult(true);
    }
}