# Quickstart: ModularShop — Catalog & Orders Reference Modules

## Prerequisites

- .NET 10 SDK
- SQL Server reachable (local, container, or LocalDB) for the three
  connection strings: `ModuleA`, `ModuleB`, `Global`
- RabbitMQ reachable for full transport verification, **or** set
  `Cap:Transport` to `InMemory` in `appsettings.Development.json` to skip it
  (see `research.md` §2 — storage stays SQL Server regardless)

## Setup

1. Restore and build the solution:
   ```
   dotnet restore
   dotnet build
   ```
2. Confirm the three connection strings in
   `src/Gateway/appsettings.json` (or `appsettings.Development.json`) point
   at a reachable SQL Server. EF Core migrations apply automatically at
   startup (see `research.md` §7) — no manual `dotnet ef database update`
   step is required.
3. Run the Gateway:
   ```
   dotnet run --project src/Gateway
   ```
   Confirm in the console output that: Module A's container started, Module
   B's container started, and `ChildContainerHost` started both modules'
   CAP hosted services.

## Validate User Story 1 — Browse Catalog & Check Availability

1. Seed at least one `Product` (see `data-model.md` — this feature has no
   seeding endpoint by design; insert directly via SQL or a temporary
   startup seed for demo purposes).
2. Run `contracts/module-a-catalog.http` → `GET /api/module-a/products`.
   Confirm the seeded product appears with id, name, price,
   `createdAtUtc`.
3. Run the check-availability request for that product id before any order
   exists. Confirm the response indicates **no** existing orders
   (spec Acceptance Scenario 1.3).
4. Run the check-availability request for a product id that does not exist
   in the catalog. Confirm a not-found result (spec Acceptance Scenario
   1.4).

## Validate User Story 2 — Place an Order & Notify Catalog

1. Run `contracts/module-b-orders.http` → `POST /api/module-b/orders` with
   the seeded product's id and a positive quantity. Confirm the response
   returns the order's id, product, quantity, and `placedAtUtc`.
2. Run the invalid-quantity request (quantity `0`). Confirm a `400`
   response and that `GET /api/module-b/orders` does not show a new order
   for it (spec SC-006).
3. Within ~10 seconds of the valid order, check the Gateway console log for
   Module A's subscriber log line acknowledging receipt of
   `OrderPlacedIntegrationEvent` for that product/quantity (spec SC-004).
4. Re-run the check-availability request from User Story 1 for the same
   product id. Confirm it now indicates existing orders (spec Acceptance
   Scenario 2.4) — this answer comes from the synchronous
   `IOrderIntegrationQuery` call, independent of whether step 3's
   notification has arrived yet.
5. (Optional, idempotency) Manually re-publish/redeliver the same event
   (e.g. via the CAP dashboard's re-execute action on `cap_moduleb`'s
   published table) and confirm Module A logs a duplicate rather than
   recording a second `OrderReceipt` (spec Edge Cases, FR-009).

## Contracts Reference

- REST shapes: `contracts/module-a-catalog.http`,
  `contracts/module-b-orders.http`
- Cross-module contracts: `contracts/integration-contracts.md`
- Entity/field detail: `data-model.md`
