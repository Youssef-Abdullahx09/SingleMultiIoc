# SingleMultiIOC.POC

Reference/demo .NET 10 modular monolith proving out a Single-IoC-with-publish-only-child-containers architecture. Two modules — Catalog (Module A) and Orders (Module B) — run inside one Gateway host, share the Gateway's global DI container for application services, and talk cross-module through exactly two seams: a synchronous `Integration.Query` interface and an async CAP integration event.

See `.specify/memory/constitution.md` for the full, authoritative architecture rules. See `specs/001-modular-shop-catalog-orders/` for the feature spec, plan, data model, and API contracts this code implements.

## Architecture at a glance

- **Gateway** (`src/Gateway`) — the only executable. ASP.NET Core Minimal APIs + Swagger. Owns its own global CAP instance (schema `cap_gateway`, group `gateway.global`) and hosts `ChildContainerHost`, which pumps the hosted services of each module's private CAP-publisher child container.
- **Module A (Catalog)** and **Module B (Orders)** each register their application services (DbContext, MediatR, CAP subscriber handlers) directly on the Gateway's global container — this is the "Single IoC" pattern. The only DI isolation left per module is a small private child container used solely to obtain an outbound `ICapPublisher`, wrapped as `IModule{Name}CapPublisher`.
- Current project shape (per module, as actually built — see note below): Module A is `Domain`, `Infrastructure`, `Application`, `Api` (no `Query` or `Integration.Query` project — DTOs live under `Application/Features/...`). Module B is `Domain`, `Infrastructure`, `Application`, `Integration.Query`, `Api` (DTOs live under `Application/GetOrders`, `Application/PlaceOrder`, etc.). The constitution (`.specify/memory/constitution.md`, Principle II) still mandates a uniform six-project shape with a dedicated `Query` project per module — the code has not caught up to that yet.
- Cross-module coupling is limited to `ModuleA.Application` → `ModuleB.Integration.Query`: a synchronous `IOrderIntegrationQuery.HasOrdersForProduct(productId)` call, and an async `OrderPlacedIntegrationEvent` published by Module B and subscribed to by Module A (topic `moduleb.order.placed`).
- Each module owns one EF Core `DbContext` mapped to its own SQL Server schema (`modulea`, `moduleb`); each module's CAP instance uses a raw SQL Server connection (never `UseEntityFramework`) with its own schema/group so `cap.Published`/`cap.Received` tables never collide.

## Prerequisites

- .NET 10 SDK
- SQL Server reachable locally (default connection strings point at `127.0.0.1,1433` with Windows/trusted auth — see `src/Gateway/appsettings.json`)
- RabbitMQ reachable, **or** set `Cap:Transport` to `InMemory` in `appsettings.Development.json` to skip it for local dev (storage stays SQL Server either way)

## Build & run

```bash
dotnet restore
dotnet build
dotnet run --project src/Gateway
```

EF Core migrations for both modules' `DbContext`s are applied automatically on startup — no manual `dotnet ef database update` needed. Swagger UI is available once the Gateway is running.

## API surface

| Method | Route | Module |
|---|---|---|
| `GET` | `/api/module-a/products` | Catalog — list products |
| `POST` | `/api/module-a/products/{id}/check-availability` | Catalog — check whether a product has existing orders (404 if product id unknown) |
| `GET` | `/api/module-b/orders` | Orders — list orders |
| `POST` | `/api/module-b/orders` | Orders — place an order (`{ productId, quantity }`, rejects non-positive quantity) |

Ready-to-run request files: `specs/001-modular-shop-catalog-orders/contracts/module-a-catalog.http` and `module-b-orders.http`.

## Repository layout

```
src/
├── Gateway/                        # the only executable
└── Modules/
    ├── ModuleA/ (Catalog)          # Domain, Infrastructure, Application, Api
    └── ModuleB/ (Orders)           # Domain, Infrastructure, Application, Integration.Query, Api
specs/001-modular-shop-catalog-orders/   # spec, plan, data model, contracts, quickstart, tasks
.specify/memory/constitution.md          # architecture rules (source of truth; supersedes README/plans)
```
