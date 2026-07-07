# Integration Contracts — `ModuleB.Integration.Query`

These are the only two coupling points permitted between Module A and Module
B (constitution Principle V). Both are defined in the `ModuleB.Integration.Query`
project, which has no project references of its own.

## Synchronous: `IOrderIntegrationQuery`

```csharp
namespace ModuleB.Integration.Query;

public interface IOrderIntegrationQuery
{
    bool HasOrdersForProduct(Guid productId);
}
```

- **Implemented by**: Module B (concrete class lives in `ModuleB.Application`
  or `ModuleB.Infrastructure`; only the interface is visible to Module A).
- **Consumed by**: Module A's `Application` layer, to answer
  `POST /api/module-a/products/{id}/check-availability`.
- **Contract**: reflects Module B's current data at call time. No caching.
  Thread-safe for concurrent calls (read-only).

## Asynchronous: `OrderPlacedIntegrationEvent`

```csharp
namespace ModuleB.Integration.Query;

public sealed record OrderPlacedIntegrationEvent(
    Guid EventId,
    Guid ProductId,
    int Quantity,
    DateTime OccurredAtUtc);
```

- **Published by**: Module B, via `ICapPublisher.PublishAsync("moduleb.order.placed", @event)`
  immediately after a new `Order` is successfully persisted.
- **Consumed by**: Module A, via a `[CapSubscribe("moduleb.order.placed")]`
  handler method.
- **Delivery semantics**: at-least-once. Consumers MUST be idempotent on
  `EventId` (see `data-model.md` → `OrderReceipt.IntegrationEventId`).
- **Topic/name**: `moduleb.order.placed` (fixed, per spec input — do not
  rename without updating both the publisher and the subscriber attribute).
