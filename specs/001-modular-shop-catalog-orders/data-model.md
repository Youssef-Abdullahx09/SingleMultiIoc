# Data Model: ModularShop — Catalog & Orders Reference Modules

> **Updated 2026-07-08 to match the current code.** The entity shapes below
> are unchanged from the original design. The **Cross-Module Contracts**
> section has been rewritten — the synchronous seam is a MediatR
> request/handler pair, not the originally-designed `IOrderIntegrationQuery`
> interface (now dead code), and the async event is not a single shared
> type. See `plan.md`'s Constitution Check for the full list of divergences.

## Module A (Catalog) — owns schema `modulea`

### Product (`Domain`, persisted by `Infrastructure`)

| Field | Type | Rules |
|---|---|---|
| `Id` | `Guid` | Primary key. Generated on creation. |
| `Name` | `string` | Required, non-empty. |
| `Price` | `decimal` | Required, `>= 0`. |
| `CreatedAtUtc` | `DateTime` (UTC) | Required, set at creation, immutable. |

No state transitions (create + read only, per spec Assumptions — no
update/delete). No relationship to `Order` inside Module A's own schema;
Product's relationship to Orders is answered only through the cross-module
synchronous seam (see below), never a foreign key.

**Seed data**: `ModuleA.Infrastructure/SeedData.cs` ships 3 fixed-GUID
products (applied via the `SeedProducts` EF Core migration, so they exist
immediately after `Database.Migrate()` runs at Gateway startup — no manual
SQL insert needed, superseding the original quickstart's assumption of no
seeding mechanism).

### OrderReceipt (`Domain`, persisted by `Infrastructure`) — internal bookkeeping

Represents Module A's record that it received an order-placed integration
event (FR-008/FR-009). Not exposed via any public endpoint (spec Assumption).

| Field | Type | Rules |
|---|---|---|
| `Id` | `Guid` | Primary key. Generated on receipt. |
| `IntegrationEventId` | `Guid` | The event's id from the payload. Unique index — enforces FR-009 (duplicate deliveries are recognized and ignored, not reprocessed as new). |
| `ProductId` | `Guid` | From the event payload. |
| `Quantity` | `int` | From the event payload. |
| `OccurredAtUtc` | `DateTime` (UTC) | From the event payload. |
| `ReceivedAtUtc` | `DateTime` (UTC) | Set when Module A processes the event. |

**Idempotency rule**: `ModuleA.Application.Subscribers.OrderPlacedIntegrationEvent.Subscriber`
checks `OrderReceipts.AnyAsync(r => r.IntegrationEventId == message.EventId)`
before inserting; if a row with that id already exists, it logs the
duplicate and returns without inserting again (FR-009).

## Module B (Orders) — owns schema `moduleb`

### Order (`Domain`, persisted by `Infrastructure`)

| Field | Type | Rules |
|---|---|---|
| `Id` | `Guid` | Primary key. Generated on creation. |
| `ProductId` | `Guid` | Required. Not validated against Module A (Orders does not depend on Catalog — spec keeps ownership one-directional: Catalog depends on Orders, not vice versa). |
| `Quantity` | `int` | Required, `> 0` (FR-006 / SC-006). |
| `PlacedAtUtc` | `DateTime` (UTC) | Required, set at creation, immutable. |

No state transitions (create + read only).

**Seed data**: `ModuleB.Infrastructure/SeedData.cs` ships 2 fixed-GUID
orders referencing Module A's seeded product ids (applied via the
`SeedOrders` EF Core migration; the product ids are duplicated by value on
purpose — the two modules stay decoupled and Module B does not reference
Module A's schema or entities).

## Cross-Module Contracts (as implemented)

### Synchronous: `HasOrdersForProduct.Query` (MediatR request, defined in `ModuleB.Integration.Query`, no project references)

```csharp
namespace ModuleB.Integration.Query.HasOrdersForProduct;

public sealed record Query(Guid ProductId) : IRequest<bool>;
```

- **Implemented by**: `ModuleB.Application.HasOrdersForProduct.Handler : IRequestHandler<Query, bool>`,
  which queries `ModuleBDbContext.Orders.AnyAsync(o => o.ProductId == request.ProductId)`.
- **Consumed by**: `ModuleA.Application.Features.CheckAvailability.CheckAvailabilityCommandHandler`,
  which dispatches this request via `ISender.Send(...)` — **not** via an
  injected interface. This works because both modules' MediatR handlers are
  registered on the Gateway's one global container (Single IoC —
  `plan.md` Constitution Check row III); `ModuleA.Application` takes a
  compile-time reference only to `ModuleB.Integration.Query` (for the
  `Query` record type), never to `ModuleB.Application`.
- **Contract**: reflects Module B's current data at call time. No caching.
  Behaviorally identical to the originally-designed
  `IOrderIntegrationQuery.HasOrdersForProduct(productId)` — only the
  plumbing mechanism changed. That original interface and its
  `OrderIntegrationQuery` implementation still exist in the codebase as
  fully commented-out dead code in `ModuleB.Application/OrderIntegrationQuery.cs`.
- Existence of the product itself is Module A's own concern (FR-003) — this
  seam only answers the orders question.

### Asynchronous: order-placed notification (CAP topic `moduleb.order.placed`)

There is **no single shared `OrderPlacedIntegrationEvent` type** consumed by
both sides, despite that being the original design. Instead, two
independently-declared records with an identical shape play this role:

| Field | Type | Notes |
|---|---|---|
| `EventId` | `Guid` | Unique id for this occurrence — the idempotency key `OrderReceipt.IntegrationEventId` stores (FR-009). |
| `ProductId` | `Guid` | The product ordered. |
| `Quantity` | `int` | The quantity ordered. |
| `OccurredAtUtc` | `DateTime` (UTC) | When the order was placed. |

- **Published by** `ModuleB.Application.PlaceOrder.OrderPlacedIntegrationEvent`
  (a record local to `ModuleB.Application`, in the `PlaceOrder` folder), via
  `IModuleBCapPublisher.PublishAsync("moduleb.order.placed", @event)`
  immediately after a new `Order` is successfully persisted.
- **Consumed by** `ModuleA.Application.Subscribers.OrderPlacedIntegrationEvent.Message`
  (a separately-declared record, same four fields, local to
  `ModuleA.Application`), via `Subscriber : ICapSubscribe` with
  `[CapSubscribe("moduleb.order.placed")]`. This subscriber is registered on
  the **Gateway's own global CAP instance** (schema `cap_gateway`, group
  `gateway.global`) — not a Module-A-owned CAP instance — because Module A's
  own `AddCap()` call is publish-only and only one `AddCap()` call can exist
  per container.
- `ModuleB.Integration.Query` — the project meant to hold cross-module
  contracts — declares **neither** of these types. The two sides agree on
  the event shape and topic name by convention only, not a shared compiled
  type. This is a real design gap relative to the original plan, recorded
  here rather than fixed as part of this documentation update.
- **Delivery semantics**: at-least-once, unchanged. Consumers MUST stay
  idempotent on `EventId` (enforced via `OrderReceipt.IntegrationEventId`'s
  unique index).
- **Topic/name**: `moduleb.order.placed` (fixed, matches code exactly on
  both the publish and subscribe sides).

## Entity Relationship Summary

```
Module A (Catalog)                    Module B (Orders)
┌─────────────┐                       ┌─────────────┐
│ Product     │                       │ Order       │
│ Id          │                       │ Id          │
│ Name        │                       │ ProductId   │─┐
│ Price       │                       │ Quantity    │ │
│ CreatedAtUtc│                       │ PlacedAtUtc │ │
└─────────────┘                       └─────────────┘ │
      ▲                                                │
      │ synchronous (MediatR HasOrdersForProduct.Query,│
      │ dispatched via ISender — see Cross-Module      │
      │ Contracts above)                               │
      └────────────────────────────────────────────────┘
┌─────────────┐          async (two independently-declared
│ OrderReceipt│◄─────────  OrderPlacedIntegrationEvent records,
│ (internal)  │           same shape, topic "moduleb.order.placed")
└─────────────┘
```

No shared tables, no foreign keys across schemas — the two arrows above are
the only coupling, and both still cross through the `Integration.Query`
project boundary at the reference-graph level (only the synchronous seam's
contract type actually lives there; the async event's shape is duplicated,
not shared — see note above).
