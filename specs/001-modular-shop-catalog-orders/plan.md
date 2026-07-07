# Implementation Plan: ModularShop — Catalog & Orders Reference Modules

**Branch**: `001-modular-shop-catalog-orders` | **Date**: 2026-07-07 | **Spec**: [spec.md](./spec.md)

**Input**: Feature specification from `/specs/001-modular-shop-catalog-orders/spec.md`

> **Updated 2026-07-08 to match the current code.** The solution itself was
> renamed `ModularShop` → `SingleMultiIoc` (`SingleMultiIoc.slnx`); the
> feature branch/spec-folder slug (`001-modular-shop-catalog-orders`) and
> this document's title were left as-is. More significantly, the
> implementation diverged from several of this plan's original architectural
> decisions during a later refactor. Those divergences are called out inline
> below rather than silently rewritten, since the project constitution
> (`.specify/memory/constitution.md`, v1.2.0) has already been amended to
> match some of them (the DI pattern) but not others (the six-project
> layering, the integration-query mechanism).

## Summary

Two-module .NET 10 modular monolith (solution `SingleMultiIoc`) that proves
the project's constitution end-to-end: Module A (Catalog) lists products and
answers a cross-module availability check by dispatching a MediatR request
(`ModuleB.Integration.Query.HasOrdersForProduct.Query`) that a handler in
`ModuleB.Application` answers directly against Module B's own data; Module B
lists/creates orders and publishes an order-placed integration event via
DotNetCore.CAP that Module A subscribes to and idempotently records. Both
modules register their application services (DbContext, MediatR, CAP
subscriber handlers) **directly on the Gateway's one global DI container**
("Single IoC" — see Constitution Check row III) rather than each running in
its own isolated child container. The only DI isolation left per module is a
small private, **publish-only** child container used solely to obtain an
outbound `ICapPublisher` (wrapped as `IModule{Name}CapPublisher`), because
DotNetCore.CAP allows only one `AddCap()` call per container and the Gateway,
Module A, and Module B each need a distinct CAP identity (schema + group).
`ChildContainerHost` starts/stops those two publish-only child containers'
hosted services; the Gateway's own global CAP instance is where Module A's
inbound subscriber actually runs (its own CAP call is publish-only too).

## Technical Context

**Language/Version**: C# / .NET 10

**Primary Dependencies**: ASP.NET Core Minimal APIs (Gateway only), MediatR
14.x (in-module CQRS **and** the sole mechanism for the cross-module
synchronous seam — see Constitution Check row V), DotNetCore.CAP 10.x
(cross-module async messaging, SQL Server storage, RabbitMQ transport with an
`InMemory` transport fallback for local dev — storage stays SQL Server either
way), EF Core 10.x (SQL Server provider)

**Storage**: SQL Server — one `DbContext`/schema per module (`modulea`,
`moduleb`) plus per-instance CAP tables under dedicated schemas
(`cap_modulea`, `cap_moduleb`, `cap_gateway`). Both modules now ship fixed-GUID
EF Core seed data (`SeedData.cs`, applied via the `SeedProducts`/`SeedOrders`
migrations) — 3 products and 2 orders exist immediately after the first
`Database.Migrate()` run at Gateway startup; no manual SQL seeding step is
required (this supersedes the original quickstart's manual-seed assumption).

**Testing**: No automated test project for this reference feature (spec
Assumptions: demo/reference scope). Verified via `.http` files
(`contracts/module-a-catalog.http`, `contracts/module-b-orders.http`) and the
manual event-flow check in `quickstart.md`, per the constitution's
Development Workflow gate.

**Target Platform**: Linux/Windows server (ASP.NET Core Kestrel), single
Gateway process

**Project Type**: Web service — modular monolith, single executable
(Gateway). **Module A (Catalog) is 4 class libraries** (`Domain`,
`Infrastructure`, `Application`, `Api` — no `Query` project, no
`Integration.Query` project). **Module B (Orders) is 5 class libraries**
(`Domain`, `Infrastructure`, `Application`, `Integration.Query`, `Api` — no
`Query` project). See Constitution Check row II — this is a departure from
the six-project-per-module shape originally planned here; DTOs live inline
under each module's `Application` feature folders instead of a dedicated
`Query` project.

**Performance Goals**: Check-availability responds in <1s under normal
demo-scale load (spec SC-003); order-placed notification observed on the
Catalog side within 10s of placement in ≥99% of placements (spec SC-004)

**Constraints**: Constitution-mandated project/reference-graph shape
(Principle II — currently violated, see Constitution Check), Single IoC
global registration with isolated publish-only child containers (Principle
III, amended), CAP raw SQL Server storage with unique group/table isolation
per instance (Principle IV, amended), MediatR-only in-module dispatch with
`Integration.Query`/CAP as the only cross-module seams (Principle V), one
schema per module (Principle VI)

**Scale/Scope**: Reference/demo scale — single-instance deployment, no
concurrent-write conflict handling needed (no update/delete on either
entity), low data volume

## Constitution Check

*GATE: Must pass before Phase 0 research. Re-check after Phase 1 design.*
*Re-checked 2026-07-08 against current code and constitution v1.2.0.*

| Principle | Check | Result |
|---|---|---|
| I. Gateway-Only Execution & Module Boundaries | Single executable (Gateway); Module A/B are class libraries under `src/Modules/`; the only cross-module project reference is `ModuleA.Application` → `ModuleB.Integration.Query` | PASS |
| II. Strict Six-Project Module Layering | Constitution still mandates exactly six projects per module (`Domain, Infrastructure, Application, Query, Integration.Query, Api`). **Actual code**: Module A has 4 (no `Query`, no `Integration.Query`); Module B has 5 (no `Query`). DTOs live inline in `Application/Features/...` (Module A) and `Application/{GetOrders,PlaceOrder,HasOrdersForProduct}/...` (Module B) instead of a dedicated `Query` project. | **VIOLATION (unresolved)** — recorded in Complexity Tracking below; the code has not been brought back in line and the constitution has not been amended to drop the `Query`/per-module `Integration.Query` requirement |
| III. Single IoC: Global Registration with Isolated Publish-Only Child Containers | `ModuleAStartup.AddModuleAServices`/`ModuleBStartup.AddModuleBServices` register DbContext, MediatR, and (Module A only) the CAP subscriber directly on the Gateway's global `IServiceCollection`; each also builds a small private child container solely to obtain its own `ICapPublisher`, returned to `Program.cs` purely so `ChildContainerHost` can pump its hosted services | PASS (matches constitution v1.2.0, amended from the original per-module-child-container design this plan first described) |
| IV. CAP Messaging Isolation via Publish-Only Child Containers | Three distinct `AddCap()` calls, each `x.UseSqlServer(rawConnString)` (never `UseEntityFramework`), each a unique group/schema: Gateway global (`cap_gateway` / `gateway.global`, configured directly in `Program.cs`), Module A publish-only (`cap_modulea` / `modulea.catalog`, in `ModuleA.Application`'s `AddLocalServiceProviderServices`), Module B publish-only (`cap_moduleb` / `moduleb.orders`, in `ModuleB.Application`'s `AddLocalServiceProvider`). Module A's inbound `[CapSubscribe("moduleb.order.placed")]` handler is registered on the **Gateway's global** CAP instance (its own CAP call is publish-only), since only one `AddCap()` call can exist per container | PASS (matches constitution v1.2.0, amended) |
| V. In-Module Mediation & Cross-Module Contracts | MediatR dispatches all in-module commands/queries. The synchronous cross-module seam is **not** an injected interface as originally designed — `ModuleA.Application.Features.CheckAvailability.CheckAvailabilityCommandHandler` dispatches `ModuleB.Integration.Query.HasOrdersForProduct.Query` via `ISender`, answered by `ModuleB.Application.HasOrdersForProduct.Handler`. This still respects the reference-graph rule (`ModuleA.Application` compiles only against `ModuleB.Integration.Query`, never `ModuleB.Application`) and works because both modules' MediatR handlers share the one global container (Principle III). The original `IOrderIntegrationQuery` interface and its `OrderIntegrationQuery` implementation exist only as fully commented-out dead code in `ModuleB.Application/OrderIntegrationQuery.cs`. The async seam (`OrderPlacedIntegrationEvent`) is published/consumed on CAP topic `moduleb.order.placed` as originally designed, but **not** as one shared type — see Key Entities note in `data-model.md` | PASS in effect (contract type still lives only in `ModuleB.Integration.Query`, no forbidden project references), but the mechanism differs materially from this plan's original design |
| VI. Per-Module Data Ownership | `ModuleADbContext` → schema `modulea` (Product, OrderReceipt, plus seed data); `ModuleBDbContext` → schema `moduleb` (Order, plus seed data); no cross-schema reads/writes | PASS |

*Post-Phase 1 re-check (2026-07-08)*: No new cross-module coupling beyond the
two seams above. Principle II remains an open violation; Principles III–V
reflect amendments/mechanism changes made after this plan was first written,
not new violations introduced since.

## Project Structure

### Documentation (this feature)

```text
specs/001-modular-shop-catalog-orders/
├── plan.md              # This file
├── research.md          # Phase 0 output (not re-verified in this update — may still describe the original design)
├── data-model.md         # Phase 1 output
├── quickstart.md        # Phase 1 output
├── contracts/
│   ├── integration-contracts.md
│   ├── module-a-catalog.http
│   └── module-b-orders.http
└── tasks.md              # Phase 2 output
```

### Source Code (repository root)

```text
SingleMultiIoc.slnx                       # renamed from ModularShop.slnx; no leftover .sln/.slnx of the old name
Directory.Build.props                     # net10.0, Nullable=enable, ImplicitUsings=enable, TreatWarningsAsErrors=false

src/
├── Gateway/
│   ├── Gateway.csproj                    # the ONLY executable
│   ├── Program.cs                        # configures the Gateway's own global CAP (cap_gateway / gateway.global,
│   │                                     #   RabbitMQ or InMemory per Cap:Transport); calls AddModuleAServices(...)
│   │                                     #   and AddModuleBServices(...) directly on that SAME global
│   │                                     #   IServiceCollection (Single IoC — not separate child containers);
│   │                                     #   registers ChildContainerHost with each module's private publish-only
│   │                                     #   CAP provider; runs Database.Migrate() for both DbContexts at startup;
│   │                                     #   maps both endpoint groups; Swagger enabled
│   ├── ChildContainerHost.cs             # IHostedService — starts each module's publish-only child container's
│   │                                     #   hosted services in order (Module A, then Module B) on StartAsync,
│   │                                     #   stops in reverse on StopAsync
│   ├── appsettings.json                  # ConnectionStrings: ModuleA/ModuleB/Global → SingleMultiIoc_* databases
│   │                                     #   (renamed from ModularShop_*); Cap:Transport; RabbitMQ:HostName
│   └── appsettings.Development.json      # NOT updated for the rename — still ModularShop_Module names for all
│                                         #   three connection strings, shared single DB, and no Cap:Transport key
│
└── Modules/
    ├── ModuleA/                          # Catalog — 4 projects (no Query, no Integration.Query)
    │   ├── ModuleA.Domain/               # Product, OrderReceipt entities
    │   ├── ModuleA.Infrastructure/       # → Domain. ModuleADbContext (schema "modulea"), EF migrations
    │   │                                 #   (InitialCreate, AddOrderReceipt, SeedProducts), SeedData.cs
    │   │                                 #   (3 fixed-GUID products), ModuleADbContextFactory (design-time;
    │   │                                 #   still hardcodes the pre-rename "ModularShop_ModuleA" connection string)
    │   ├── ModuleA.Application/          # → Infrastructure, ModuleB.Integration.Query (the one permitted
    │   │                                 #   cross-module reference)
    │   │   ├── Features/GetProducts/     #   GetProductsQuery + Handler, ProductDto
    │   │   ├── Features/CheckAvailability/ # CheckAvailabilityCommand + Handler (dispatches
    │   │   │                             #   ModuleB.Integration.Query.HasOrdersForProduct.Query via ISender),
    │   │   │                             #   AvailabilityResultDto
    │   │   ├── Subscribers/OrderPlacedIntegrationEvent/ # Message (local event-payload record) + Subscriber
    │   │   │                             #   (ICapSubscribe, [CapSubscribe("moduleb.order.placed")]), upserts
    │   │   │                             #   OrderReceipt idempotently by EventId
    │   │   └── Utilities/                # IModuleACapPublisher + ModuleACapPublisher (outbound-only wrapper;
    │   │                                 #   Module A doesn't publish anything in this feature, kept for symmetry)
    │   └── ModuleA.Api/                  # ModuleAStartup.AddModuleAServices(...) — registers Application
    │                                     #   services + the CapSubscribe handler on the GLOBAL IServiceCollection,
    │                                     #   builds/returns Module A's private publish-only CAP child provider;
    │                                     #   CatalogEndpoints.MapCatalogEndpoints(...)
    │
    └── ModuleB/                          # Orders — 5 projects (no Query project)
        ├── ModuleB.Domain/               # Order entity
        ├── ModuleB.Infrastructure/       # → Domain. ModuleBDbContext (schema "moduleb"), EF migrations
        │                                 #   (InitialCreate, SeedOrders), SeedData.cs (2 fixed-GUID orders
        │                                 #   against Module A's seeded product ids), ModuleBCapPublisher.cs
        │                                 #   (defines BOTH IModuleBCapPublisher and its implementation here,
        │                                 #   unlike Module A where the equivalent lives in Application/Utilities),
        │                                 #   ModuleBDbContextFactory (still hardcodes pre-rename "ModularShop_ModuleB")
        ├── ModuleB.Application/          # → Infrastructure, ModuleB.Integration.Query
        │   ├── GetOrders/                #   GetOrdersQuery + Handler, OrderDto
        │   ├── PlaceOrder/                #  PlaceOrderCommand + Handler (rejects quantity <= 0), local
        │   │                             #   OrderPlacedIntegrationEvent record (see data-model.md — this is
        │   │                             #   NOT the same type as anything in Integration.Query), publishes via
        │   │                             #   IModuleBCapPublisher.PublishAsync("moduleb.order.placed", ...)
        │   ├── HasOrdersForProduct/Handler.cs # IRequestHandler<HasOrdersForProduct.Query, bool> — the actual
        │   │                             #   implementation of the cross-module synchronous seam
        │   └── OrderIntegrationQuery.cs  #   DEAD CODE — fully commented out; the original interface-based
        │                                 #   implementation this feature was first built around
        ├── ModuleB.Integration.Query/    # (no project references) — contains only
        │                                 #   HasOrdersForProduct/Query.cs:
        │                                 #   `public sealed record Query(Guid ProductId) : IRequest<bool>`.
        │                                 #   No IOrderIntegrationQuery interface and no shared
        │                                 #   OrderPlacedIntegrationEvent type exist here
        └── ModuleB.Api/                  # ModuleBStartup.AddModuleBServices(...) — registers Application
                                          #   services on the GLOBAL IServiceCollection, builds/returns Module B's
                                          #   private publish-only CAP child provider (no subscriber — Module B
                                          #   never consumes events); OrdersEndpoints.MapOrdersEndpoints(...)
                                          #   (PlaceOrderRequest record defined inline in the same file)
```

**Structure Decision**: Single-solution modular monolith, one executable
(`Gateway`), two modules. The reference-graph rule (only
`ModuleA.Application` → `ModuleB.Integration.Query`) still holds exactly.
The six-project-per-module shape and the isolated-child-container DI model
originally planned here were **not** what shipped — see Constitution Check
above for the specifics and Complexity Tracking below for the unresolved
Principle II violation.

## Complexity Tracking

*Constitution Check reports one unresolved violation (Principle II). This
was not a deliberate, evaluated trade-off recorded at plan time — it is
drift introduced by a later refactor (see `git log`: "reorganize modules
into vertical feature slices, add CAP publisher and seed data") that has not
been reconciled with the constitution. Recorded here per governance
requirements rather than silently ignored.*

| Violation | Why it exists | Simpler/compliant alternative rejected because |
|---|---|---|
| Module A ships 4 projects and Module B ships 5, instead of six each (`Query` missing from both; `Integration.Query` missing from Module A) | The refactor moved DTOs inline into `Application/Features/...` (Module A) and per-command folders (Module B) instead of a dedicated `Query` project, and Module A was never given an `Integration.Query` project since nothing in this feature calls back into Catalog synchronously | No alternative was evaluated — this needs a decision from the team: either amend Principle II to drop the mandatory `Query`/always-present `Integration.Query` requirement, or restore the missing projects to bring the code back into compliance |
