# Research: ModularShop — Catalog & Orders Reference Modules

All Technical Context fields are settled by direct user input (tech stack) or
the project constitution — no open `NEEDS CLARIFICATION` markers remain. This
document records the design decisions needed to turn those fixed choices into
a concrete, constitution-compliant implementation.

## 1. Cross-module DI wiring for `Integration.Query`

**Decision**: The Gateway (composition root) builds Module B's child
`ServiceProvider` first, resolves its `IOrderIntegrationQuery` implementation
instance once, and passes that *instance* into `ModuleAStartup` so Module A's
`ServiceCollection` can register it (as a singleton) before Module A's
provider is built. Module A's project files reference only
`ModuleB.Integration.Query` (the interface + event contract) — never
`ModuleB.Application` or any other ModuleB internal project.

**Rationale**: `ServiceCollection` is immutable once `.Build()` is called, so
handing a live instance across containers requires building the *providing*
module first. The Gateway is the one place in the solution allowed to see
both modules at once (it is not itself bound by the module-boundary rule),
so it is the correct place to perform this handoff. This keeps Principle I
(no module references another module's internals) intact — the interface
reference is the only compile-time coupling, and it points at
`Integration.Query`, which is exactly what that project exists for.

**Alternatives considered**:
- *Service locator / lazy resolution inside Module A* (Module A holds a
  reference to Module B's root `IServiceProvider` and resolves on demand) —
  rejected: leaks Module B's container into Module A's code, defeating
  container isolation (Principle III) even though it avoids the build-order
  constraint.
- *Shared "integration" DI container* separate from both modules — rejected
  as unnecessary indirection for two modules; adds a third container to
  reason about for no isolation benefit.

**Consequence**: Gateway startup order is fixed: build Module B's container
→ resolve `IOrderIntegrationQuery` → build Module A's container (passing the
resolved instance in) → register both with `ChildContainerHost` → map both
modules' endpoints.

## 2. CAP transport vs. storage

**Decision**: CAP *storage* (the `cap.Published`/`cap.Received` outbox/inbox
tables) is always SQL Server via a raw connection string, per constitution
Principle IV — this is non-negotiable and does not vary by environment. CAP
*transport* (the broker moving messages between publisher and subscriber) is
selected via `appsettings.json` → `Cap:Transport` = `RabbitMQ` (default) or
`InMemory`; `RabbitMQ` calls `x.UseRabbitMQ(...)`, `InMemory` calls
`x.UseInMemoryMessageQueue()`. Every CAP instance always calls
`x.UseSqlServer(rawConnectionString)` regardless of the transport setting.

**Rationale**: The user's requested "RabbitMQ, or in-memory for local dev if
RabbitMQ unavailable" only makes sense as a broker-level fallback — CAP's
storage requirement is a constitutional constraint (Principle IV), not an
environment concern, and switching storage per-environment would reintroduce
the exact `UseEntityFramework` coupling risk the constitution forbids.

**Alternatives considered**: Swapping storage to SQLite/in-memory for local
dev too — rejected, violates Principle IV outright.

## 3. CAP group names and table isolation

**Decision**:
- Module A (Catalog) CAP instance: group `modulea.catalog`, CAP table schema
  `cap_modulea`.
- Module B (Orders) CAP instance: group `moduleb.orders`, CAP table schema
  `cap_moduleb`.
- Gateway global CAP instance: group `gateway.global` (per user spec), CAP
  table schema `cap_gateway`.

**Rationale**: Constitution Principle IV requires every CAP instance to use
a unique group name and a distinct table prefix/database. Using a dedicated
schema per instance (rather than a database-per-instance) keeps everything
in one SQL Server instance for a demo/reference project while guaranteeing
`cap.Published`/`cap.Received` never collide.

## 4. Connection strings vs. CAP raw connection

**Decision**: `appsettings.json` connection strings `ModuleA`, `ModuleB`,
`Global` (as specified by the user) point at the same physical SQL Server
database used by that owner's EF Core `DbContext`. Each module's CAP
instance is configured with `x.UseSqlServer(connString)` using that same
string value, but this is a *second, independent* configuration call — CAP
is never wired through `services.AddCap(x => x.UseEntityFramework<TDbContext>())`.
The Gateway's own CAP instance uses the `Global` connection string.

**Rationale**: Reusing the same connection string text for both EF Core and
CAP's raw SQL client satisfies the letter and intent of Principle IV (CAP
must not be *bound to the DbContext*) while matching the three connection
strings the user explicitly asked for (not six). EF Core's migrations still
apply the module's own schema (`modulea`/`moduleb`); CAP's raw client creates
its own tables under its own schema (`cap_modulea`/`cap_moduleb`) in the same
database.

**Alternatives considered**: Separate physical databases per module —
rejected as unnecessary infrastructure for a reference feature; schema
separation already satisfies both EF Core and CAP isolation requirements.

## 5. Module endpoint dispatch through child containers

**Decision**: Each module's `Api` project exposes an
`IEndpointRouteBuilder` extension (`MapCatalogEndpoints`,
`MapOrdersEndpoints`) that captures that module's built `IServiceProvider`.
Each Minimal API delegate opens `moduleProvider.CreateScope()` per request,
resolves `IMediator` (or other module-scoped services) from that scope, and
disposes the scope at the end of the request. The Gateway's own DI container
is never asked to resolve a module-internal service.

**Rationale**: This is the mechanism that makes Principle III's "isolated DI
via child containers" concretely reachable from Minimal API routing, which
by default resolves from the app's root container.

**Alternatives considered**: Registering module services directly into the
Gateway's root container with a naming convention to avoid collisions —
rejected outright, this is precisely what Principle III forbids.

## 6. `ChildContainerHost` shape

**Decision**: A single `ChildContainerHost : IHostedService` registered in
the Gateway's global container. It is constructed with the list of built
module `IServiceProvider`s (Module A, Module B). On `StartAsync`, for each
provider in order, it resolves every registered `IHostedService` (this is
where each module's CAP hosted service — the CAP processor/dispatcher — gets
started) and calls `StartAsync`. On `StopAsync`, it stops them in reverse
order.

**Rationale**: Matches constitution Principle III exactly: "CAP hosted
services inside child containers are started via a `ChildContainerHost`
`IHostedService` in the global container." One host managing an ordered list
is sufficient for two modules and keeps startup/shutdown ordering explicit.

**Alternatives considered**: One `ChildContainerHost` instance per module —
rejected as needless duplication; a single host iterating a list is simpler
and still satisfies the requirement.

## 7. Migrations strategy

**Decision**: Each module owns EF Core migrations in its own
`Infrastructure` project (where its `DbContext` lives, per Principle II:
`Infrastructure → Domain`). Migrations are applied automatically at Gateway
startup (`dbContext.Database.Migrate()`) during that module's
`ModuleXStartup` configuration, scoped to that module's own schema
(`modulea` / `moduleb`).

**Rationale**: Simplest path to "solution builds and runs" (constitution's
quality gate) for a two-module reference feature; a separate migration
runner tool is unjustified extra scope.

**Alternatives considered**: Manual `dotnet ef database update` per module
as a pre-run step — rejected, adds a manual step the constitution's
build-and-verify gate doesn't require.

## 8. Verification approach

**Decision**: No automated test project is added. Verification uses one
`.http` file per module (`contracts/module-a-catalog.http`,
`contracts/module-b-orders.http`) plus a documented manual check of Module
A's subscriber log line after placing an order — matching the constitution's
Development Workflow gate ("verified via an `.http` file or `curl` request")
and the spec's Assumptions (reference/demo feature, not production
commerce).

**Rationale**: Matches both the constitution's stated quality gate and the
spec's explicit scope boundary; adding a test project would be scope beyond
what either document asks for.
