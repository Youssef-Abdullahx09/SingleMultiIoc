<!--
Sync Impact Report
Version change: 1.1.0 → 1.2.0
Modified principles:
  - III. Isolated DI via Child Containers (Multi IoC), with a Recorded
    Single IoC Exception → III. Single IoC: Global Registration with
    Isolated Publish-Only Child Containers: Module B has converged onto the
    same Single IoC pattern as Module A (all application services on the
    Gateway's global container); no module demonstrates the original
    per-module Multi IoC child-container pattern anymore. Only each
    module's outbound CAP publisher keeps a private child container.
  - IV. CAP Messaging Isolation (per Module's DI Variant) → IV. CAP
    Messaging Isolation via Publish-Only Child Containers: documents Module
    B's own private publish-only CAP container (schema cap_moduleb, group
    moduleb.orders) and its IModuleBCapPublisher abstraction, alongside
    Module A's existing split (private publish / global subscribe).
Added sections: none (expanded existing III/IV in place)
Removed sections: none
Templates requiring updates:
  - .specify/templates/plan-template.md ✅ no change needed (Constitution Check gate is derived dynamically per-plan)
  - .specify/templates/spec-template.md ✅ no change needed (technology-agnostic, no principle references)
  - .specify/templates/tasks-template.md ✅ no change needed (generic phase structure, no principle references)
  - .specify/templates/checklist-template.md ✅ no change needed (generic)
Follow-up TODOs: none
-->

# TechnoIsland SingleMultiIOC Constitution

## Core Principles

### I. Gateway-Only Execution & Module Boundaries

There is exactly one executable in the solution: the Gateway host (ASP.NET Core
Minimal APIs). All business modules are class libraries, never executables.
Modules live under `src/Modules/{ModuleName}`. A module MUST NOT reference any
other module's internal projects (Domain, Infrastructure, Application, Query,
Api); the only legal cross-module reference is to the target module's
`Integration.Query` project, which exposes public contracts and integration
events.

Rationale: A single executable keeps deployment and hosting simple while the
module boundary enforced through `Integration.Query` prevents modules from
silently coupling to each other's internals, preserving the ability to split
modules into separate services later without a rewrite.

### II. Strict Six-Project Module Layering (NON-NEGOTIABLE)

Every module MUST consist of exactly six class libraries: `Domain`,
`Infrastructure`, `Application`, `Query`, `Integration.Query`, `Api`. No
module may add, remove, merge, or rename these projects. Project references
are restricted to exactly:

- `Infrastructure` → `Domain`
- `Application` → `Query` AND `Infrastructure`
- `Api` → `Application`
- `Integration.Query` → (no project references; contracts and integration
  events only)

The `Application → Infrastructure` reference is intentionally inverted from
textbook Clean Architecture and MUST NOT be "corrected" toward
`Infrastructure → Application` or an interface-inversion pattern. No other
cross-layer reference is permitted (e.g. `Domain` MUST NOT reference
`Infrastructure` or `Application`; `Api` MUST NOT reference `Infrastructure`
or `Domain` directly).

Rationale: A fixed, uniform six-project shape makes every module predictable
to navigate and lets tooling/reviews check layering violations mechanically.
The inverted `Application → Infrastructure` reference is a deliberate,
recorded decision for this codebase, not an oversight — it must survive
future refactors and code reviews unchanged.

### III. Single IoC: Global Registration with Isolated Publish-Only Child Containers

Every module registers its application services (DbContext, MediatR,
cross-module query instances, CAP subscriber handlers) directly on the
Gateway's global `IServiceCollection`, via a `Module{Name}Startup.AddModule{Name}Services(this IServiceCollection, ...)` extension inside that
module's `Api` project. Module endpoints resolve dependencies through normal
minimal API parameter injection against the global provider — no
module-local scope or provider. The one exception per module is its outbound
CAP publisher (Principle IV): each keeps a small, private child container
solely to obtain an isolated `ICapPublisher`, wrapped behind a
`IModule{Name}CapPublisher` abstraction registered as a singleton on the
global container. Background work belonging to those private containers
(the CAP hosted services) MUST be started through a `ChildContainerHost`
`IHostedService` registered in the global container — the global container
starts these hosted services, it does not host module application services
itself.

Rationale: This project (`SingleMultiIOC`) originally recorded both a
"Multi IoC" per-module child-container pattern and a "Single IoC" global
pattern side by side (Module B and Module A respectively) as a deliberate
comparison. Both modules have since converged on Single IoC: a single shared
container is simpler to reason about, and the isolation a full child
container gives is no longer worth its indirection once every module needs
the same cross-module DI access. The publish-only child container survives
per module purely because CAP allows only one `AddCap()` call per container
(Principle IV) — it is a messaging necessity, not a residual DI-isolation
stance.

### IV. CAP Messaging Isolation via Publish-Only Child Containers

DotNetCore.CAP is the only messaging mechanism for asynchronous, cross-module
communication. CAP MUST NOT be bound to a module's `DbContext` via
`UseEntityFramework`; every `AddCap(...)` call configures a RAW SQL Server
connection string (`x.UseSqlServer(connString)`) instead. Every CAP instance
MUST use a unique CAP group name and a distinct schema, so that
`cap.Published`/`cap.Received` tables never collide across instances.

Only one `AddCap()` call may exist per `IServiceCollection`/container — a
second call's configuration silently overwrites the first's scalar options
(schema, group, connection string), so instances that must stay distinct
MUST live in separate containers. Since Principle III puts every module's
application services on the one global container, each module's `AddCap()`
call instead lives in a small private child container built solely for that
purpose, and only its `ICapPublisher` is pulled out — wrapped as
`IModule{Name}CapPublisher` and registered as a singleton on the global
container — for publishing:

- **Module A**: private container (schema `cap_modulea`, group
  `modulea.catalog`) for publishing only. It has no CAP call of its own for
  subscribing — its inbound subscriber (`OrderPlacedIntegrationEventHandler`,
  an `ICapSubscribe` registered on the global container) is instead
  discovered and run by the **Gateway's own global CAP instance** (schema
  `cap_gateway`, group `gateway.global`), since the global container may only
  host one `AddCap()` call.
- **Module B**: private container (schema `cap_moduleb`, group
  `moduleb.orders`) for publishing only. It has no subscriber at all.
- The Gateway's own global CAP instance therefore serves two roles: its own
  independent messaging identity, and Module A's subscription engine.

Application code MUST depend on the `IModule{Name}CapPublisher` abstraction,
never on `ICapPublisher` directly — the private container that produces it
is an implementation detail.

Rationale: Binding CAP to a module's own `DbContext` would couple message
outbox persistence to EF Core migrations and transaction scope in ways that
leak across module boundaries; raw connection strings plus unique
group/schema isolation keep each messaging instance independently deployable
and debuggable. The publish-only child container per module is the direct,
necessary consequence of Principle III: a module cannot register a second
`AddCap()` call on the global container it shares with the Gateway and every
other module.

### V. In-Module Mediation & Cross-Module Contracts

Within a module, commands and queries MUST be dispatched through MediatR.
A module MUST NOT call into another module's `Application`, `Infrastructure`,
`Domain`, `Query`, or `Api` projects directly. Synchronous cross-module reads
go exclusively through the target module's `Integration.Query` interfaces.
Cross-module asynchronous communication goes exclusively through CAP
integration events published from `Integration.Query` contracts.

Rationale: Two well-defined seams — `Integration.Query` for synchronous
contracts and CAP events for asynchronous ones — are the only legal points
of coupling between modules, keeping module boundaries enforceable in code
review rather than relying on discipline alone.

### VI. Per-Module Data Ownership

Persistence uses EF Core against SQL Server. Each module owns exactly one
`DbContext`, mapped to its own distinct database schema (e.g. `modulea`,
`moduleb`). A module's `DbContext` MUST NOT read or write tables belonging to
another module's schema.

Rationale: Schema-per-module gives each module physical data isolation inside
a shared database, matching the logical isolation enforced by the module
boundary and DI rules, without requiring separate databases from day one.

## Technology Stack Constraints

- Runtime/Framework: .NET 10, ASP.NET Core Minimal APIs (Gateway host only).
- In-module CQRS/mediation: MediatR.
- Cross-module async messaging: DotNetCore.CAP (SQL Server transport).
- Persistence: EF Core + SQL Server, one `DbContext` per module.
- No module may introduce an alternative mediation, messaging, or ORM
  technology without a constitution amendment.

## Development Workflow & Quality Gates

- The solution MUST build successfully after every completed task; a task is
  not "done" if it leaves the solution in a non-building state.
- Every new or changed endpoint MUST be verified via an `.http` file or
  `curl` request before the task is considered complete.
- Layering (Principle II) and messaging isolation (Principle IV) violations
  are release blockers, not style nits — they MUST be fixed before merge.

## Governance

This constitution supersedes conflicting guidance in READMEs, prior plans, or
ad-hoc conventions. Amendments require:

1. A written proposal describing the change and its rationale.
2. Update of this file, including the Sync Impact Report header comment.
3. A version bump per semantic versioning:
   - MAJOR: backward-incompatible removal or redefinition of a principle
     (e.g. changing the six-project layering shape or its reference rules).
   - MINOR: a new principle or materially expanded guidance added.
   - PATCH: wording, clarification, or typo fixes with no rule change.
4. Propagation check across `.specify/templates/*.md` for now-outdated
   references, updating them in the same change.

All plans and PRs MUST verify compliance with the Core Principles above;
deviations must be recorded in the plan's Complexity Tracking table with a
concrete justification, or rejected.

**Version**: 1.2.0 | **Ratified**: 2026-07-07 | **Last Amended**: 2026-07-08
