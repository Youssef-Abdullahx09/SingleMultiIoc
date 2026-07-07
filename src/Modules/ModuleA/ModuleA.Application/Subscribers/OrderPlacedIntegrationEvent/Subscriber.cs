using DotNetCore.CAP;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using ModuleA.Domain;
using ModuleA.Infrastructure;

namespace ModuleA.Application.Subscribers.OrderPlacedIntegrationEvent;

// CAP subscriber (constitution Principle V - the async seam). Idempotent on
// EventId so at-least-once redelivery (FR-009) never records a duplicate receipt.
public sealed class Subscriber(
    ModuleADbContext dbContext,
    ILogger<Subscriber> logger) : ICapSubscribe
{
    [CapSubscribe("moduleb.order.placed")]
    public async Task Handle(Message integrationEvent)
    {
        var alreadyReceived = await dbContext.OrderReceipts
            .AnyAsync(r => r.IntegrationEventId == integrationEvent.EventId);

        if (alreadyReceived)
        {
            logger.LogInformation(
                "Duplicate OrderPlacedIntegrationEvent {EventId} for product {ProductId} - already recorded, skipping.",
                integrationEvent.EventId, integrationEvent.ProductId);
            return;
        }

        dbContext.OrderReceipts.Add(new OrderReceipt
        {
            Id = Guid.NewGuid(),
            IntegrationEventId = integrationEvent.EventId,
            ProductId = integrationEvent.ProductId,
            Quantity = integrationEvent.Quantity,
            OccurredAtUtc = integrationEvent.OccurredAtUtc,
            ReceivedAtUtc = DateTime.UtcNow,
        });
        await dbContext.SaveChangesAsync();

        logger.LogInformation(
            "Received OrderPlacedIntegrationEvent {EventId}: product {ProductId}, quantity {Quantity}.",
            integrationEvent.EventId, integrationEvent.ProductId, integrationEvent.Quantity);
    }
}
