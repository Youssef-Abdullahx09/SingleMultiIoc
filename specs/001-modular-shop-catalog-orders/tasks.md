---

description: "Task list for ModularShop Catalog/Orders feature implementation"
---

# Tasks: ModularShop — Catalog & Orders Reference Modules

**Input**: Design documents from `/specs/001-modular-shop-catalog-orders/`

**Prerequisites**: plan.md, spec.md, research.md, data-model.md, contracts/, quickstart.md

**Tests**: Not requested in the spec (reference/demo scope) — verification uses
`.http` files and the manual event-flow checks in `quickstart.md` instead of
an automated test project.

**Organization**: Tasks are grouped by user story to enable independent
implementation and testing of each story.

## Format: `[ID] [P?] [Story] Description`

- **[P]**: Can run in parallel (different files, no dependencies)
- **[Story]**: Which user story this task belongs to (US1, US2)
- Every task lists its exact file path

## Path Conventions

Per `plan.md` Project Structure: `src/Gateway/`, `src/Modules/ModuleA/{Domain,Infrastructure,Application,Query,Integration.Query,Api}/`, `src/Modules/ModuleB/{same six}/`, solution root `ModularShop.sln` + `Directory.Build.props`.

---

## Phase 1: Setup (Shared Infrastructure)

**Purpose**: Solution and project skeleton with the exact reference graph mandated by the constitution.

- [ ] T001 Create `ModularShop.sln` and `Directory.Build.props` at repo root (`net10.0`, `Nullable=enable`, `ImplicitUsings=enable`)
- [ ] T002 [P] Create `src/Gateway/Gateway.csproj` (Microsoft.NET.Sdk.Web, net10.0)
- [ ] T003 [P] Create `src/Modules/ModuleA/ModuleA.Domain/ModuleA.Domain.csproj` (classlib, no project references)
- [ ] T004 [P] Create `src/Modules/ModuleA/ModuleA.Infrastructure/ModuleA.Infrastructure.csproj` referencing `ModuleA.Domain`, with `Microsoft.EntityFrameworkCore.SqlServer` + `Microsoft.EntityFrameworkCore.Design`
- [ ] T005 [P] Create `src/Modules/ModuleA/ModuleA.Query/ModuleA.Query.csproj` (classlib, no project references)
- [ ] T006 [P] Create `src/Modules/ModuleA/ModuleA.Application/ModuleA.Application.csproj` referencing `ModuleA.Query` + `ModuleA.Infrastructure`, with `MediatR`
- [ ] T007 [P] Create `src/Modules/ModuleA/ModuleA.Integration.Query/ModuleA.Integration.Query.csproj` (classlib, no project references)
- [ ] T008 [P] Create `src/Modules/ModuleA/ModuleA.Api/ModuleA.Api.csproj` referencing `ModuleA.Application`, with `DotNetCore.CAP`, `DotNetCore.CAP.SqlServer`, `DotNetCore.CAP.RabbitMQ`, `DotNetCore.CAP.InMemoryMessageQueue`
- [ ] T009 [P] Create `src/Modules/ModuleB/ModuleB.Domain/ModuleB.Domain.csproj` (classlib, no project references)
- [ ] T010 [P] Create `src/Modules/ModuleB/ModuleB.Infrastructure/ModuleB.Infrastructure.csproj` referencing `ModuleB.Domain`, with `Microsoft.EntityFrameworkCore.SqlServer` + `Microsoft.EntityFrameworkCore.Design`
- [ ] T011 [P] Create `src/Modules/ModuleB/ModuleB.Query/ModuleB.Query.csproj` (classlib, no project references)
- [ ] T012 [P] Create `src/Modules/ModuleB/ModuleB.Application/ModuleB.Application.csproj` referencing `ModuleB.Query` + `ModuleB.Infrastructure`, with `MediatR`
- [ ] T013 [P] Create `src/Modules/ModuleB/ModuleB.Integration.Query/ModuleB.Integration.Query.csproj` (classlib, no project references)
- [ ] T014 [P] Create `src/Modules/ModuleB/ModuleB.Api/ModuleB.Api.csproj` referencing `ModuleB.Application`, with `DotNetCore.CAP`, `DotNetCore.CAP.SqlServer`, `DotNetCore.CAP.RabbitMQ`, `DotNetCore.CAP.InMemoryMessageQueue`
- [ ] T015 Add project reference from `ModuleA.Application.csproj` to `ModuleB.Integration.Query.csproj` — the single permitted cross-module reference (constitution Principle I)
- [ ] T016 Add `Gateway.csproj` and all twelve module project files to `ModularShop.sln`
- [ ] T017 Configure `src/Gateway/appsettings.json` — `ConnectionStrings:ModuleA`, `ConnectionStrings:ModuleB`, `ConnectionStrings:Global`, `Cap:Transport` (default `RabbitMQ`)
- [ ] T018 Verify `dotnet build` succeeds with zero errors (constitution build-after-every-task gate)

**Checkpoint**: Solution skeleton builds; reference graph matches constitution Principle II exactly.

---

## Phase 2: Foundational (Blocking Prerequisites)

**Purpose**: Entities, DbContexts, the cross-module `Integration.Query` contract + implementation, isolated DI child containers, and Gateway wiring — required before either user story is testable, because US1's availability check depends on Module B's data layer existing.

**⚠️ CRITICAL**: No user story work can begin until this phase is complete.

- [ ] T019 [P] Create `Product` entity in `src/Modules/ModuleA/ModuleA.Domain/Product.cs` (Id Guid, Name, Price, CreatedAtUtc)
- [ ] T020 [P] Create `Order` entity in `src/Modules/ModuleB/ModuleB.Domain/Order.cs` (Id Guid, ProductId, Quantity, PlacedAtUtc)
- [ ] T021 Create `ModuleADbContext` in `src/Modules/ModuleA/ModuleA.Infrastructure/ModuleADbContext.cs` — schema `modulea`, maps `Product` (depends on T019)
- [ ] T022 Create `ModuleBDbContext` in `src/Modules/ModuleB/ModuleB.Infrastructure/ModuleBDbContext.cs` — schema `moduleb`, maps `Order` (depends on T020)
- [ ] T023 [P] Add initial EF Core migration for `ModuleADbContext` in `src/Modules/ModuleA/ModuleA.Infrastructure/Migrations/` (depends on T021)
- [ ] T024 [P] Add initial EF Core migration for `ModuleBDbContext` in `src/Modules/ModuleB/ModuleB.Infrastructure/Migrations/` (depends on T022)
- [ ] T025 Define `IOrderIntegrationQuery` interface in `src/Modules/ModuleB/ModuleB.Integration.Query/IOrderIntegrationQuery.cs` (`HasOrdersForProduct(Guid productId)`)
- [ ] T026 Define `OrderPlacedIntegrationEvent` record in `src/Modules/ModuleB/ModuleB.Integration.Query/OrderPlacedIntegrationEvent.cs` (EventId, ProductId, Quantity, OccurredAtUtc)
- [ ] T027 Implement `OrderIntegrationQuery : IOrderIntegrationQuery` in `src/Modules/ModuleB/ModuleB.Application/OrderIntegrationQuery.cs` — queries `ModuleBDbContext` for matching `ProductId` (depends on T022, T025)
- [ ] T028 Create `ModuleBStartup` in `src/Modules/ModuleB/ModuleB.Api/ModuleBStartup.cs` — builds Module B's child `ServiceProvider`: registers `ModuleBDbContext` (`ModuleB` connection string), MediatR, `OrderIntegrationQuery`, CAP (`x.UseSqlServer` raw `ModuleB` connection string, schema `cap_moduleb`, group `moduleb.orders`, transport from `Cap:Transport`) (depends on T022, T027)
- [ ] T029 Create `ModuleAStartup` in `src/Modules/ModuleA/ModuleA.Api/ModuleAStartup.cs` — builds Module A's child `ServiceProvider`: accepts a resolved `IOrderIntegrationQuery` instance as a parameter and registers it as singleton, registers `ModuleADbContext` (`ModuleA` connection string), MediatR, CAP (raw `ModuleA` connection string, schema `cap_modulea`, group `modulea.catalog`, transport from `Cap:Transport`) (depends on T021)
- [ ] T030 Create `ChildContainerHost` (`IHostedService`) in `src/Gateway/ChildContainerHost.cs` — starts each module child container's registered `IHostedService`s on `StartAsync`, stops them in reverse on `StopAsync` (research.md §6)
- [ ] T031 Wire `src/Gateway/Program.cs` — build Module B's container (T028) first, resolve `IOrderIntegrationQuery` from it, build Module A's container (T029) passing that instance, configure Gateway's own global CAP (raw `Global` connection string, schema `cap_gateway`, group `gateway.global`), register `ChildContainerHost` with both providers, apply EF Core migrations for both `DbContext`s at startup (depends on T028, T029, T030)
- [ ] T032 Verify `dotnet build` succeeds and `dotnet run --project src/Gateway` starts cleanly with both module containers and `ChildContainerHost` initialized, no DI resolution errors (depends on T031)

**Checkpoint**: Foundation ready — user story implementation can now begin.

---

## Phase 3: User Story 1 - Browse Catalog and Check Product Availability (Priority: P1) 🎯 MVP

**Goal**: List catalog products; check whether a product has existing orders via the synchronous `IOrderIntegrationQuery` seam.

**Independent Test**: Seed a product (and zero-or-more orders directly via SQL, per spec Assumptions — no seed endpoint), then call the product list and check-availability endpoints directly.

- [ ] T033 [P] [US1] Create `ProductDto` in `src/Modules/ModuleA/ModuleA.Query/ProductDto.cs`
- [ ] T034 [P] [US1] Create `AvailabilityResultDto` in `src/Modules/ModuleA/ModuleA.Query/AvailabilityResultDto.cs`
- [ ] T035 [US1] Implement `GetProductsQuery` + handler in `src/Modules/ModuleA/ModuleA.Application/GetProductsQuery.cs` — MediatR query returning `List<ProductDto>` from `ModuleADbContext` (FR-001) (depends on T033)
- [ ] T036 [US1] Implement `CheckAvailabilityCommand` + handler in `src/Modules/ModuleA/ModuleA.Application/CheckAvailabilityCommand.cs` — MediatR command: 404 result if product id not found (FR-003), otherwise calls `IOrderIntegrationQuery.HasOrdersForProduct` and returns `AvailabilityResultDto` (FR-002, FR-010) (depends on T034, T027)
- [ ] T037 [US1] Map `GET /api/module-a/products` and `POST /api/module-a/products/{id}/check-availability` in `src/Modules/ModuleA/ModuleA.Api/CatalogEndpoints.cs` — each delegate resolves `IMediator` from Module A's own child-container scope per request (research.md §5) (depends on T035, T036)
- [ ] T038 [US1] Register `CatalogEndpoints` mapping in `src/Gateway/Program.cs` (depends on T037, T031)
- [ ] T039 [US1] Verify via `contracts/module-a-catalog.http` + `quickstart.md` US1 steps: seeded product appears in list; availability is false with no orders; 404 for unknown product id

**Checkpoint**: User Story 1 fully functional and independently testable.

---

## Phase 4: User Story 2 - Place an Order and Notify the Catalog (Priority: P2)

**Goal**: Create orders; publish `OrderPlacedIntegrationEvent` via CAP; Module A subscribes and idempotently records receipt.

**Independent Test**: Place an order via Module B, then observe Module A's recorded receipt of the notification, independent of any product-list/availability call in the same run.

- [ ] T040 [P] [US2] Create `OrderDto` in `src/Modules/ModuleB/ModuleB.Query/OrderDto.cs`
- [ ] T041 [US2] Implement `GetOrdersQuery` + handler in `src/Modules/ModuleB/ModuleB.Application/GetOrdersQuery.cs` — MediatR query returning `List<OrderDto>` (FR-004) (depends on T040)
- [ ] T042 [US2] Implement `PlaceOrderCommand` + handler in `src/Modules/ModuleB/ModuleB.Application/PlaceOrderCommand.cs` — MediatR command: rejects non-positive quantity (FR-006, SC-006), persists `Order`, publishes `OrderPlacedIntegrationEvent` via `ICapPublisher.PublishAsync("moduleb.order.placed", ...)` after successful persistence (FR-007) (depends on T040, T026)
- [ ] T043 [US2] Map `GET /api/module-b/orders` and `POST /api/module-b/orders` in `src/Modules/ModuleB/ModuleB.Api/OrdersEndpoints.cs` (depends on T041, T042)
- [ ] T044 [US2] Register `OrdersEndpoints` mapping in `src/Gateway/Program.cs` (depends on T043)
- [ ] T045 [P] [US2] Create `OrderReceipt` entity in `src/Modules/ModuleA/ModuleA.Domain/OrderReceipt.cs` (Id, IntegrationEventId unique, ProductId, Quantity, OccurredAtUtc, ReceivedAtUtc)
- [ ] T046 [US2] Add `OrderReceipt` mapping + unique index on `IntegrationEventId` to `ModuleADbContext` and a new EF Core migration in `src/Modules/ModuleA/ModuleA.Infrastructure/` (depends on T045, T021)
- [ ] T047 [US2] Implement `OrderPlacedIntegrationEventHandler` (`[CapSubscribe("moduleb.order.placed")]`) in `src/Modules/ModuleA/ModuleA.Application/OrderPlacedIntegrationEventHandler.cs` — upserts `OrderReceipt` by `IntegrationEventId`, logs duplicates without reprocessing (FR-008, FR-009) (depends on T046, T026)
- [ ] T048 [US2] Register `OrderPlacedIntegrationEventHandler` as a CAP subscriber in `src/Modules/ModuleA/ModuleA.Api/ModuleAStartup.cs` (depends on T047, T029)
- [ ] T049 [US2] Verify via `contracts/module-b-orders.http` + `quickstart.md` US2 steps 1-4: order placed and listed; invalid quantity rejected (SC-006); Module A logs receipt within 10s (SC-004); subsequent availability check for that product now returns true
- [ ] T050 [US2] Verify idempotency via `quickstart.md` US2 step 5: redeliver the same `OrderPlacedIntegrationEvent` and confirm no duplicate `OrderReceipt` is created (FR-009)

**Checkpoint**: Both user stories independently functional; full spec success criteria (SC-001–SC-006) achievable end-to-end.

---

## Phase 5: Polish & Cross-Cutting Concerns

- [ ] T051 Run full `quickstart.md` validation end-to-end (US1 + US2 in sequence) confirming SC-001 through SC-006
- [ ] T052 [P] Audit project references across the solution against constitution Principle II (no forbidden cross-layer or cross-module references beyond `ModuleA.Application` → `ModuleB.Integration.Query`)

---

## Dependencies & Execution Order

### Phase Dependencies

- **Setup (Phase 1)**: No dependencies — start immediately.
- **Foundational (Phase 2)**: Depends on Setup completion — BLOCKS both user stories (US1 needs Module B's data layer + `IOrderIntegrationQuery` to answer availability, even with zero orders).
- **User Story 1 (Phase 3)**: Depends on Foundational completion only.
- **User Story 2 (Phase 4)**: Depends on Foundational completion; independent of US1's endpoint code, but shares `ModuleADbContext` (extended in T046) — sequenced after US1 here because US1's verification (T039) is cleanest before the schema changes in T046, not because of a hard code dependency.
- **Polish (Phase 5)**: Depends on both user stories being complete.

### Within Foundational

T019/T020 → T021/T022 → T023/T024 (migrations); T025/T026 → T027 → T028; T021 → T029; T028+T029+T030 → T031 → T032.

### Within Each User Story

Query/Command + DTOs before endpoint mapping; endpoint mapping before Gateway registration; Gateway registration before verification.

### Parallel Opportunities

- All Setup project-creation tasks (T002–T014) marked [P] can run in parallel once T001 exists.
- T019/T020 (entities) and T023/T024 (migrations, after their DbContexts) can run in parallel across modules.
- T033/T034 (US1 DTOs) and T040 (US2 DTO) can run in parallel — different files, different stories.
- T045 (OrderReceipt entity) can start in parallel with T041/T042 (US2 Orders side) since it's a different module's file.

---

## Parallel Example: Setup Phase

```bash
# After T001 (sln + Directory.Build.props) exists, launch all project-creation tasks together:
Task: "Create src/Modules/ModuleA/ModuleA.Domain/ModuleA.Domain.csproj"
Task: "Create src/Modules/ModuleA/ModuleA.Query/ModuleA.Query.csproj"
Task: "Create src/Modules/ModuleA/ModuleA.Integration.Query/ModuleA.Integration.Query.csproj"
Task: "Create src/Modules/ModuleB/ModuleB.Domain/ModuleB.Domain.csproj"
Task: "Create src/Modules/ModuleB/ModuleB.Query/ModuleB.Query.csproj"
Task: "Create src/Modules/ModuleB/ModuleB.Integration.Query/ModuleB.Integration.Query.csproj"
```

---

## Implementation Strategy

### MVP First (User Story 1 Only)

1. Complete Phase 1: Setup
2. Complete Phase 2: Foundational (CRITICAL — includes Module B's data layer, required even for US1)
3. Complete Phase 3: User Story 1
4. **STOP and VALIDATE**: Run T039 independently
5. Demo the availability check against manually-seeded data

### Incremental Delivery

1. Setup + Foundational → Foundation ready (T001–T032)
2. Add User Story 1 → Validate (T033–T039) → Demo catalog browsing + availability
3. Add User Story 2 → Validate (T040–T050) → Demo order placement + async notification + idempotency
4. Polish (T051–T052) → Full spec success-criteria pass

---

## Notes

- [P] tasks = different files, no dependencies on incomplete tasks.
- [US1]/[US2] labels map tasks to their user story for traceability.
- No test-project tasks were generated — the spec does not request automated
  tests; verification is via `.http` files and manual log/state checks per
  `quickstart.md`, matching the constitution's Development Workflow gate.
- Constitution build gate: every phase ends with (or is immediately followed
  by) a `dotnet build` verification task (T018, T032; T039/T049 exercise the
  running app).
