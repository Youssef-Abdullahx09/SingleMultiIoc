# Integration Contracts — as implemented

> **Updated 2026-07-08 to match the current code.** The original design put
> both seams in `ModuleB.Integration.Query` as a plain interface and a
> shared event record. Only the synchronous seam's contract type actually
> lives there today, and it's a MediatR request record, not an interface.
> The async event is not a shared type at all. See `plan.md`'s Constitution
> Check (rows II, V) and `data-model.md` for the full picture.

These are the only two coupling points between Module A and Module B.
`ModuleA.Application` has a compile-time project reference only to
`ModuleB.Integration.Query` (no project references of its own) — never to
`ModuleB.Application` or any other Module B project.

## Synchronous: `HasOrdersForProduct.Query` (MediatR request)

```csharp
namespace ModuleB.Integration.Query.HasOrdersForProduct;

public sealed record Query(Guid ProductId) : IRequest<bool>;
```

- **Defined in**: `ModuleB.Integration.Query` (the project's only file).
- **Implemented by**: `ModuleB.Application.HasOrdersForProduct.Handler : IRequestHandler<Query, bool>`,
  querying `ModuleBDbContext.Orders.AnyAsync(o => o.ProductId == request.ProductId)`.
- **Consumed by**: `ModuleA.Application.Features.CheckAvailability.CheckAvailabilityCommandHandler`,
  which does `await sender.Send(new Query(productId))` — a MediatR dispatch,
  not a call through an injected `IOrderIntegrationQuery` interface. This
  works only because both modules' MediatR handlers are registered on the
  Gateway's single global DI container ("Single IoC" — see
  `plan.md` Constitution Check row III); there is no direct project
  reference from Module A to Module B's handler.
- **Contract**: reflects Module B's current data at call time. No caching.
  Thread-safe for concurrent calls (read-only).
- **Superseded design**: an `IOrderIntegrationQuery` interface
  (`bool HasOrdersForProduct(Guid productId)`) and its `OrderIntegrationQuery`
  implementation were the original mechanism for this seam. Both still exist
  in the codebase, but fully commented out, in
  `ModuleB.Application/OrderIntegrationQuery.cs` — dead code, not deleted.

## Asynchronous: order-placed notification (no shared type)

Unlike the synchronous seam, this is **not** backed by a single record type
that both sides reference. Two independently-declared records, with an
identical shape, play this role by convention:

```csharp
// Published side — ModuleB.Application.PlaceOrder
public sealed record OrderPlacedIntegrationEvent(
    Guid EventId,
    Guid ProductId,
    int Quantity,
    DateTime OccurredAtUtc);

// Consumed side — ModuleA.Application.Subscribers.OrderPlacedIntegrationEvent
public sealed record Message(
    Guid EventId,
    Guid ProductId,
    int Quantity,
    DateTime OccurredAtUtc);
```

- **Published by**: Module B's `PlaceOrderCommandHandler`, via
  `IModuleBCapPublisher.PublishAsync("moduleb.order.placed", @event)`
  immediately after a new `Order` is successfully persisted.
- **Consumed by**: Module A's `Subscriber : ICapSubscribe`, via a
  `[CapSubscribe("moduleb.order.placed")] Handle(Message message)` method.
  This subscriber is registered on the **Gateway's own global CAP
  instance** (schema `cap_gateway`, group `gateway.global`) — Module A's own
  `AddCap()` call is publish-only (see `plan.md` Constitution Check row IV).
- `ModuleB.Integration.Query` declares neither of the above records — this
  is a design gap relative to the original plan (nothing enforces the two
  shapes staying in sync except manual discipline). Recorded here rather
  than silently fixed as part of this documentation update.
- **Delivery semantics**: at-least-once. Consumers MUST be idempotent on
  `EventId` (see `data-model.md` → `OrderReceipt.IntegrationEventId`).
- **Topic/name**: `moduleb.order.placed` (fixed; matches code exactly on
  both sides — do not rename one without the other).
