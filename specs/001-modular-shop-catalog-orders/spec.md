# Feature Specification: ModularShop — Catalog & Orders Reference Modules

**Feature Branch**: `001-modular-shop-catalog-orders`

**Created**: 2026-07-07

**Status**: Draft

**Input**: User description: "Build a two-module modular monolith (\"ModularShop\") demonstrating the full module pattern: Module A (Catalog) exposes products and a check-availability capability that consults Module B (Orders); Module B exposes orders and publishes an order-placed notification that Module A reacts to; the Gateway hosts both modules and their background messaging."

## User Scenarios & Testing *(mandatory)*

### User Story 1 - Browse Catalog and Check Product Availability (Priority: P1)

A store operator wants to see the list of products in the catalog and, for any
given product, find out whether that product already has orders placed
against it, without having to look in two separate systems.

**Why this priority**: This is the smallest slice that proves the Catalog
capability stands on its own while also proving it can correctly consult
Order data owned by a different module. Without this, there is no
cross-module read to validate.

**Independent Test**: Can be fully tested by seeding at least one product and
zero-or-more orders, then calling the product list and the
check-availability capability directly — no order-placement flow is required
first, since check-availability must also correctly report "no orders" for a
product with none.

**Acceptance Scenarios**:

1. **Given** the catalog has one or more products, **When** the product list
   is requested, **Then** every product's identifier, name, price, and
   creation timestamp are returned.
2. **Given** a product that has at least one order recorded against it,
   **When** availability is checked for that product, **Then** the response
   indicates the product has existing orders.
3. **Given** a product that has no orders recorded against it, **When**
   availability is checked for that product, **Then** the response indicates
   the product has no existing orders.
4. **Given** a product identifier that does not exist in the catalog, **When**
   availability is checked for it, **Then** the system returns a clear
   not-found result rather than a false availability answer.

---

### User Story 2 - Place an Order and Notify the Catalog (Priority: P2)

A store operator wants to place an order for a product and have the Catalog
side of the system become aware that an order happened, so downstream
catalog-side processes (e.g. future stock adjustments, audit logs) can react
without the Orders module needing to know Catalog exists.

**Why this priority**: This proves the second half of the pattern — one-way,
fire-and-forget notification from Orders to Catalog — building on the data
User Story 1 already knows how to read. It depends on orders being creatable,
so it is sequenced after Story 1's read paths exist.

**Independent Test**: Can be fully tested by placing an order through the
Orders capability and then observing that the Catalog side has recorded
receipt of a corresponding notification, independent of whether any product
list or availability check is performed in the same test run.

**Acceptance Scenarios**:

1. **Given** a valid product identifier and a positive quantity, **When** an
   order is placed, **Then** the order is persisted and returned with its
   identifier, product, quantity, and placement timestamp.
2. **Given** an order has just been placed, **When** enough time has passed
   for asynchronous processing, **Then** the Catalog side has recorded a
   distinct receipt of an order-placed notification carrying that product and
   quantity.
3. **Given** the order list is requested, **When** one or more orders exist,
   **Then** every order's identifier, product, quantity, and placement
   timestamp are returned.
4. **Given** an order is placed for a product, **When** availability is
   subsequently checked for that same product (User Story 1's capability),
   **Then** the result now reflects that the product has existing orders.

---

### Edge Cases

- What happens when an order is placed with a quantity of zero or a negative
  number? System MUST reject it rather than persist a nonsensical order.
- What happens when the order-placed notification cannot be delivered
  immediately (e.g. the Catalog side is temporarily unavailable)? The
  notification MUST be retried/delivered eventually rather than silently lost,
  and placing the order MUST NOT fail or roll back because of a notification
  delivery delay.
- What happens when check-availability is called for a product that exists in
  the catalog but the Orders data is temporarily unreachable? The system MUST
  surface a clear error rather than silently reporting "no orders."
- What happens when the same order-placed notification is delivered more than
  once (at-least-once delivery)? The Catalog side's receipt log MUST tolerate
  duplicates without corrupting its record of what happened.

## Requirements *(mandatory)*

### Functional Requirements

- **FR-001**: System MUST allow retrieval of the full list of catalog
  products, each with identifier, name, price, and creation timestamp.
- **FR-002**: System MUST allow checking, for a given product identifier,
  whether that product has any existing orders, answering strictly based on
  current Orders data at the time of the check.
- **FR-003**: System MUST return a distinguishable not-found result when
  check-availability is requested for a product identifier that does not
  exist in the catalog.
- **FR-004**: System MUST allow retrieval of the full list of orders, each
  with identifier, product identifier, quantity, and placement timestamp.
- **FR-005**: System MUST allow placing a new order given a product
  identifier and a quantity, persisting it and returning its identifier and
  placement timestamp.
- **FR-006**: System MUST reject order placement when quantity is not a
  positive whole number.
- **FR-007**: System MUST emit a notification of every successfully placed
  order (carrying product identifier, quantity, and the time the order
  occurred) for consumption by the Catalog side, independent of and without
  blocking the response to the order-placement request.
- **FR-008**: The Catalog side MUST receive and record each order-placed
  notification it is sent, including at least the product identifier,
  quantity, and a record that the receipt happened.
- **FR-009**: The Catalog side's record of received notifications MUST be
  safe to receive the same notification more than once without producing
  incorrect or duplicated business state.
- **FR-010**: The Catalog side's check-availability capability MUST obtain
  order-existence information from the Orders side through a direct
  synchronous lookup (not from the asynchronous notification stream), so its
  answer reflects current Orders data rather than only previously-notified
  orders.
- **FR-011**: Catalog data and Orders data MUST remain independently owned —
  neither side's records are directly readable or writable by the other
  except through the two integration paths defined in FR-002/FR-010
  (synchronous lookup) and FR-007/FR-008 (asynchronous notification).

### Key Entities

- **Product**: A catalog item. Attributes: unique identifier, name, price,
  timestamp of creation.
- **Order**: A request to purchase a quantity of a product. Attributes:
  unique identifier, the product identifier it refers to, quantity ordered,
  timestamp of placement.
- **Order-Placed Notification**: The fact that an order occurred, as seen by
  the Catalog side. Carries the product identifier, quantity, and the time
  the order occurred. Distinct from the Order entity itself — it is a
  point-in-time announcement, not a queryable record owned by Catalog.

## Success Criteria *(mandatory)*

### Measurable Outcomes

- **SC-001**: The system can be built and started with zero manual
  intervention beyond standard startup.
- **SC-002**: 100% of products seeded or created are visible in the product
  list response.
- **SC-003**: A check-availability request returns the correct existing/
  not-existing-orders answer for a given product in under 1 second under
  normal load.
- **SC-004**: Within 10 seconds of an order being successfully placed, the
  Catalog side has a recorded receipt of the corresponding notification, in
  at least 99% of placements under normal operating conditions.
- **SC-005**: 100% of orders successfully placed are visible in the order
  list response and reflected in subsequent availability checks for their
  product.
- **SC-006**: Order placement requests with invalid quantity are rejected
  100% of the time and never produce a persisted order.

## Assumptions

- "Normal load" / "normal operating conditions" in Success Criteria refer to
  a single-instance development/demo deployment, not production-scale traffic.
- No authentication/authorization is required for this reference feature;
  all endpoints are open. Access control is out of scope.
- No update or delete capability is required for Product or Order in this
  feature — only creation (Order) and retrieval (both), plus the
  availability check.
- Product creation/seeding mechanism (e.g. how initial products get into the
  catalog) is out of scope for this feature's acceptance criteria; only
  retrieval and availability-check behavior are specified.
- The Catalog side's "receipt log" for order-placed notifications is
  internal bookkeeping (for verifying delivery) and does not need its own
  public retrieval endpoint.
- This feature exists to validate the project's modular-monolith
  architecture end-to-end (independent module ownership, one synchronous
  cross-module query path, one asynchronous cross-module event path); it is
  a reference/demo capability, not a production commerce feature (no
  pricing rules, inventory decrement, payment, etc.).
