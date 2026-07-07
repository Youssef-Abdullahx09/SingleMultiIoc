# Data Model: ModularShop — Catalog & Orders Reference Modules

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
Product's relationship to Orders is answered only through the
`Integration.Query` seam (see below), never a foreign key.

### OrderReceipt (`Domain`, persisted by `Infrastructure`) — internal bookkeeping

Represents Module A's record that it received an `OrderPlacedIntegrationEvent`
(FR-008/FR-009). Not exposed via any public endpoint (spec Assumption).

| Field | Type | Rules |
|---|---|---|
| `Id` | `Guid` | Primary key. Generated on receipt. |
| `IntegrationEventId` | `Guid` | The CAP message id / event id from the payload. Unique index — enforces FR-009 (duplicate deliveries are recognized and ignored, not reprocessed as new). |
| `ProductId` | `Guid` | From the event payload. |
| `Quantity` | `int` | From the event payload. |
| `OccurredAtUtc` | `DateTime` (UTC) | From the event payload (`OrderPlacedIntegrationEvent.OccurredAtUtc`). |
| `ReceivedAtUtc` | `DateTime` (UTC) | Set when Module A processes the event. |

**Idempotency rule**: subscriber handler upserts by `IntegrationEventId`;
if a row with that id already exists, the handler logs the duplicate and
returns without inserting again (FR-009).

## Module B (Orders) — owns schema `moduleb`

### Order (`Domain`, persisted by `Infrastructure`)

| Field | Type | Rules |
|---|---|---|
| `Id` | `Guid` | Primary key. Generated on creation. |
| `ProductId` | `Guid` | Required. Not validated against Module A (Orders does not depend on Catalog — spec keeps ownership one-directional: Catalog depends on Orders, not vice versa). |
| `Quantity` | `int` | Required, `> 0` (FR-006 / SC-006). |
| `PlacedAtUtc` | `DateTime` (UTC) | Required, set at creation, immutable. |

No state transitions (create + read only).

## Cross-Module Contracts (`ModuleB.Integration.Query` — no project references)

### `IOrderIntegrationQuery` (interface, implemented in Module B, consumed by Module A)

```
bool HasOrdersForProduct(Guid productId)
```

- Synchronous. Backed directly by Module B's current `Order` data at call
  time (FR-002, FR-010) — not by anything Module A has cached from events.
- Returns `true` if at least one `Order.ProductId` equals `productId`;
  `false` otherwise. Existence of the product itself is Module A's own
  concern (FR-003) — this interface only answers the orders question.

### `OrderPlacedIntegrationEvent` (integration event, published by Module B, consumed by Module A)

| Field | Type | Notes |
|---|---|---|
| `EventId` | `Guid` | Unique id for this occurrence — the idempotency key Module A's `OrderReceipt.IntegrationEventId` stores (FR-009). |
| `ProductId` | `Guid` | The product ordered. |
| `Quantity` | `int` | The quantity ordered. |
| `OccurredAtUtc` | `DateTime` (UTC) | When the order was placed. |

Published on CAP topic/name `moduleb.order.placed`.

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
      │ synchronous (IOrderIntegrationQuery)           │
      └────────────────────────────────────────────────┘
┌─────────────┐          async (OrderPlacedIntegrationEvent,
│ OrderReceipt│◄─────────  topic "moduleb.order.placed")
│ (internal)  │
└─────────────┘
```

No shared tables, no foreign keys across schemas — the two arrows above are
the only coupling, and both cross the `Integration.Query` seam.
