<!--
Sync Impact Report
Version change: [TEMPLATE] → 1.0.0 (initial ratification)
Modified principles: n/a (first concrete version)
Added sections:
  - Core Principles I–VI (Gateway-Only Execution & Module Boundaries;
    Strict Six-Project Module Layering; Isolated DI via Child Containers;
    CAP Messaging Isolation; In-Module Mediation & Cross-Module Contracts;
    Per-Module Data Ownership)
  - Technology Stack Constraints
  - Development Workflow & Quality Gates
  - Governance
Removed sections: none
Templates requiring updates:
  - .specify/templates/plan-template.md ✅ no change needed (Constitution Check gate is derived dynamically per-plan)
  - .specify/templates/spec-template.md ✅ no change needed (technology-agnostic, no principle references)
  - .specify/templates/tasks-template.md ✅ no change needed (generic phase structure, no principle references)
  - .specify/templates/checklist-template.md ✅ no change needed (generic)
Follow-up TODOs:
  - TODO(RATIFICATION_DATE): original adoption date unknown at authoring time; set to
    the date this constitution was first written (2026-07-07) per project convention.
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

### III. Isolated DI via Child Containers

Each module builds and owns its own `ServiceCollection`/`ServiceProvider`
(child container), wired up by a `Module{Name}Startup` class inside that
module's `Api` project. The Gateway's global container MUST NOT register any
module-internal service, repository, handler, or `DbContext`. Background work
belonging to a module's child container (e.g. CAP hosted services) MUST be
started through a `ChildContainerHost` `IHostedService` registered in the
global container — the global container starts child hosted services, it does
not host module services itself.

Rationale: Per-module containers keep module internals genuinely private
(not just by convention) and prevent lifetime/registration collisions between
modules that would otherwise share one global container.

### IV. CAP Messaging Isolation

DotNetCore.CAP is the only messaging mechanism for asynchronous, cross-module
communication. Each module's child container configures its own CAP
publisher/subscriber using a RAW SQL Server connection string
(`x.UseSqlServer(connString)`); CAP MUST NOT be bound to a module's
`DbContext` via `UseEntityFramework`. The Gateway's global container
configures its own, separate CAP instance. Every CAP instance (each module's
and the Gateway's) MUST use a unique CAP group name and a distinct table
prefix or database, so that `cap.Published`/`cap.Received` tables never
collide across instances.

Rationale: Binding CAP to a module's own `DbContext` would couple message
outbox persistence to EF Core migrations and transaction scope in ways that
leak across module boundaries; raw connection strings plus unique
group/table isolation keep each module's messaging infrastructure
independently deployable and debuggable.

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

**Version**: 1.0.0 | **Ratified**: 2026-07-07 | **Last Amended**: 2026-07-07
