# Implementation Plan: ModularShop — Catalog & Orders Reference Modules

**Branch**: `001-modular-shop-catalog-orders` | **Date**: 2026-07-07 | **Spec**: [spec.md](./spec.md)

**Input**: Feature specification from `/specs/001-modular-shop-catalog-orders/spec.md`

## Summary

Build a two-module .NET 10 modular monolith ("ModularShop") that proves the
project's constitution end-to-end: Module A (Catalog) lists products and
answers a cross-module availability check by calling Module B (Orders)
through a synchronous `Integration.Query` interface; Module B lists/creates
orders and publishes an `OrderPlacedIntegrationEvent` via DotNetCore.CAP that
Module A subscribes to and idempotently records. Both modules run in their
own isolated DI child container inside a single Gateway host (ASP.NET Core
Minimal APIs), with their own EF Core `DbContext`/schema and their own CAP
instance (raw SQL Server storage, unique group/table isolation); the Gateway
hosts a third, separate global CAP instance and a `ChildContainerHost` that
starts each module's CAP hosted service.

## Technical Context

**Language/Version**: C# / .NET 10

**Primary Dependencies**: ASP.NET Core Minimal APIs (Gateway only), MediatR
(in-module CQRS), DotNetCore.CAP (cross-module async messaging, SQL Server
storage, RabbitMQ transport with an `InMemory` transport fallback for local
dev — storage stays SQL Server either way), EF Core (SQL Server provider)

**Storage**: SQL Server — one `DbContext`/schema per module (`modulea`,
`moduleb`) plus per-instance CAP tables under dedicated schemas
(`cap_modulea`, `cap_moduleb`, `cap_gateway`)

**Testing**: No automated test project for this reference feature (spec
Assumptions: demo/reference scope). Verified via `.http` files
(`contracts/module-a-catalog.http`, `contracts/module-b-orders.http`) and the
manual event-flow check in `quickstart.md`, per the constitution's
Development Workflow gate.

**Target Platform**: Linux/Windows server (ASP.NET Core Kestrel), single
Gateway process

**Project Type**: Web service — modular monolith, single executable
(Gateway), two module class-library sets

**Performance Goals**: Check-availability responds in <1s under normal
demo-scale load (spec SC-003); order-placed notification observed on the
Catalog side within 10s of placement in ≥99% of placements (spec SC-004)

**Constraints**: Constitution-mandated project/reference-graph shape
(Principle II), isolated per-module DI containers (Principle III), CAP raw
SQL Server storage with unique group/table isolation per instance
(Principle IV), MediatR-only in-module dispatch with `Integration.Query`/CAP
as the only cross-module seams (Principle V), one schema per module
(Principle VI)

**Scale/Scope**: Reference/demo scale — single-instance deployment, no
concurrent-write conflict handling needed (no update/delete on either
entity), low data volume

## Constitution Check

*GATE: Must pass before Phase 0 research. Re-check after Phase 1 design.*

| Principle | Check | Result |
|---|---|---|
| I. Gateway-Only Execution & Module Boundaries | Single executable (Gateway); Module A/B are class libraries under `src/Modules/`; the only cross-module reference is Module A → `ModuleB.Integration.Query` | PASS |
| II. Strict Six-Project Module Layering | Both modules get exactly `Domain, Infrastructure, Application, Query, Integration.Query, Api`; reference graph matches the mandated arrows exactly (see Project Structure below) | PASS |
| III. Isolated DI via Child Containers | `ModuleAStartup`/`ModuleBStartup` in each `Api` project build independent `ServiceProvider`s; Gateway root container never registers module-internal services; `ChildContainerHost` starts module CAP hosted services (research.md §6) | PASS |
| IV. CAP Messaging Isolation | Each module's CAP instance uses `x.UseSqlServer(rawConnString)` (never `UseEntityFramework`); Gateway has its own separate CAP config (`gateway.global`); every instance has a unique group (`modulea.catalog`, `moduleb.orders`, `gateway.global`) and dedicated schema (`cap_modulea`, `cap_moduleb`, `cap_gateway`) (research.md §2–3) | PASS |
| V. In-Module Mediation & Cross-Module Contracts | MediatR dispatches all in-module commands/queries; Module A reaches Module B only via `IOrderIntegrationQuery` (sync) and `OrderPlacedIntegrationEvent` (async), both defined in `ModuleB.Integration.Query` | PASS |
| VI. Per-Module Data Ownership | `ModuleADbContext` → schema `modulea` (Product, OrderReceipt); `ModuleBDbContext` → schema `moduleb` (Order); no cross-schema reads/writes | PASS |

No violations. Complexity Tracking table is not needed.

*Post-Phase 1 re-check*: Data model (`data-model.md`) and contracts
(`contracts/`) introduce no new cross-module coupling beyond the two seams
already covered above (`IOrderIntegrationQuery`, `OrderPlacedIntegrationEvent`).
Still PASS on all six principles.

## Project Structure

### Documentation (this feature)

```text
specs/001-modular-shop-catalog-orders/
├── plan.md              # This file
├── research.md          # Phase 0 output
├── data-model.md         # Phase 1 output
├── quickstart.md        # Phase 1 output
├── contracts/
│   ├── integration-contracts.md
│   ├── module-a-catalog.http
│   └── module-b-orders.http
└── tasks.md              # Phase 2 output (/speckit-tasks — not created here)
```

### Source Code (repository root)

```text
ModularShop.sln
Directory.Build.props                     # net10.0, Nullable=enable, ImplicitUsings=enable, shared across all projects

src/
├── Gateway/
│   ├── Gateway.csproj                    # the ONLY executable
│   ├── Program.cs                        # builds ModuleB then ModuleA child containers (research.md §1),
│   │                                     # registers ChildContainerHost, configures gateway.global CAP,
│   │                                     # maps both modules' Minimal API endpoints
│   ├── ChildContainerHost.cs             # IHostedService — starts/stops each module's hosted services in order
│   └── appsettings.json                  # ConnectionStrings: ModuleA, ModuleB, Global; Cap:Transport
│
└── Modules/
    ├── ModuleA/                          # Catalog
    │   ├── ModuleA.Domain/               # Product, OrderReceipt entities
    │   ├── ModuleA.Infrastructure/       # → Domain. ModuleADbContext (schema "modulea"), EF migrations
    │   ├── ModuleA.Application/          # → Query, Infrastructure. MediatR handlers:
    │   │                                 #   GetProductsQuery/Handler, CheckAvailabilityCommand/Handler
    │   │                                 #   (depends on IOrderIntegrationQuery), OrderPlacedIntegrationEventHandler
    │   │                                 #   ([CapSubscribe("moduleb.order.placed")], upserts OrderReceipt)
    │   ├── ModuleA.Query/                # DTOs/read models: ProductDto, AvailabilityResultDto
    │   ├── ModuleA.Integration.Query/    # (no project refs) — reserved for future consumers of Module A;
    │   │                                 # empty/placeholder in this feature, kept per Principle II shape
    │   └── ModuleA.Api/                  # → Application. ModuleAStartup (child container),
    │                                     # CatalogEndpoints.MapCatalogEndpoints(...)
    │
    └── ModuleB/                          # Orders
        ├── ModuleB.Domain/               # Order entity
        ├── ModuleB.Infrastructure/       # → Domain. ModuleBDbContext (schema "moduleb"), EF migrations
        ├── ModuleB.Application/          # → Query, Infrastructure. MediatR handlers:
        │                                 #   GetOrdersQuery/Handler, PlaceOrderCommand/Handler
        │                                 #   (persists Order, publishes OrderPlacedIntegrationEvent via ICapPublisher)
        │                                 #   OrderIntegrationQuery : IOrderIntegrationQuery (implementation)
        ├── ModuleB.Query/                # DTOs/read models: OrderDto
        ├── ModuleB.Integration.Query/    # (no project refs) — IOrderIntegrationQuery interface,
        │                                 # OrderPlacedIntegrationEvent record. Referenced ONLY by ModuleA.Application
        └── ModuleB.Api/                  # → Application. ModuleBStartup (child container),
                                          # OrdersEndpoints.MapOrdersEndpoints(...)
```

**Structure Decision**: Single-solution modular monolith matching the
constitution's mandated shape exactly — one executable (`Gateway`), two
modules each with the fixed six class libraries, reference arrows exactly as
specified in Principle II. `ModuleA.Integration.Query` has no consumers in
this feature (Module B never calls back into Module A) but is still created
per Principle II's "every module has exactly six" rule, ready for a future
module that needs to query Catalog.

## Complexity Tracking

*No entries — Constitution Check reported no violations.*
