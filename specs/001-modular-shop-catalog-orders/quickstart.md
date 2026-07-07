# Quickstart: ModularShop — Catalog & Orders Reference Modules

> **Updated 2026-07-08 to match the current code.** Solution root is
> `SingleMultiIoc.slnx` (renamed from `ModularShop.slnx`); default
> connection strings now point at `SingleMultiIoc_ModuleA/ModuleB/Global`
> databases. Both modules ship EF Core seed data now — the manual-seeding
> step below is no longer required. See `plan.md`'s Constitution Check for
> the full list of architecture changes since this doc was first written.

## Prerequisites

- .NET 10 SDK
- SQL Server reachable (local, container, or LocalDB) for the three
  connection strings: `ModuleA`, `ModuleB`, `Global` (see
  `src/Gateway/appsettings.json` — default points at `127.0.0.1,1433` with
  trusted/Windows auth; `appsettings.Development.json` has **not** been
  updated for the `SingleMultiIoc` rename and still uses the old
  `ModularShop_Module` naming with SQL auth — check which file your run
  configuration actually loads)
- RabbitMQ reachable for full transport verification, **or** set
  `Cap:Transport` to `InMemory` to skip it (storage stays SQL Server
  regardless)

## Setup

1. Restore and build the solution:
   ```
   dotnet restore
   dotnet build
   ```
2. Confirm the three connection strings in
   `src/Gateway/appsettings.json` (or `appsettings.Development.json`) point
   at a reachable SQL Server. EF Core migrations apply automatically at
   startup — no manual `dotnet ef database update` step is required, and
   both modules' seed data (see below) applies as part of those migrations.
3. Run the Gateway:
   ```
   dotnet run --project src/Gateway
   ```
   Confirm in the console output that: Module A's services and Module B's
   services registered on the Gateway's global container, migrations ran
   for both `DbContext`s, and `ChildContainerHost` started each module's
   publish-only CAP child container's hosted services.

## Validate User Story 1 — Browse Catalog & Check Availability

1. Seed data ships automatically now: `ModuleA.Infrastructure/SeedData.cs`
   (3 fixed-GUID products) and `ModuleB.Infrastructure/SeedData.cs` (2
   fixed-GUID orders against two of those product ids) apply via EF Core
   migrations at Gateway startup — no manual SQL insert is needed. Call
   `GET /api/module-a/products` and `GET /api/module-b/orders` first to note
   which seeded product ids have orders and which don't, so you can exercise
   both branches of check-availability below.
2. Run `contracts/module-a-catalog.http` → `GET /api/module-a/products`.
   Confirm the seeded products appear with id, name, price,
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
   `HasOrdersForProduct.Query` MediatR dispatch (see
   `contracts/integration-contracts.md`), independent of whether step 3's
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
