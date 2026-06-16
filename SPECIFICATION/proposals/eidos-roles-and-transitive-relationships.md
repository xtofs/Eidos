# Design Proposal: Roles and Derived Relationships

> **Status:** Incorporated into /eidos-specification-v0.2.md.
> The spec portions of this proposal (R1–R7) have been merged into v0.2. What remains below is
> **implementation guidance** for the .NET runtime — not part of the language specification.

---

## Incorporated into v0.2

The following recommendations are now part of the v0.2 spec:

| Recommendation                             | v0.2 Section  |
|--------------------------------------------|---------------|
| R1 — Participation objects                 | §5.3.1        |
| R2 — Role eligibility enforcement          | §5.3.2        |
| R3 — Roles facet (/_roles)                 | §5.3.3        |
| R4 — Role-disambiguated projections (?as=) | §5.3.4        |
| R5 — Derived relationships principle       | §2.4, §5.4    |
| R6 — Recursive relationships               | §4.11, §5.4.1 |
| R7 — Chained relationships                 | §4.11, §5.4.2 |
| Response representation (_via, /endpoint)  | §5.4.3        |

Open questions were merged into §7.10–§7.12.

---

## Implementation guidance (runtime, not spec)

The following is guidance for implementing derived relationships in the .NET runtime. It is **not**
part of the language specification.

### Precedent: how `?expand` works today

The v0.1 runtime separates **API handlers** (HTTP responses) from **data getters** (raw objects).
`Entity(...)` registers an **entity resolver** via `RegisterEntityResolver(typeName, key => ...)`.
When handling `?expand`, the relationship builder invokes the resolver to embed participants inline.

**Key lesson:** API handlers are not sufficient for composition. A `GET /persons/{key}` handler
returns an `IResult`; expand logic needs the raw `PersonDto`. The resolver provides that access.

### Design for derived relationships

| Component                          | Type      | Purpose                                                                                   |
|------------------------------------|-----------|-------------------------------------------------------------------------------------------|
| `IRelationshipEdgeProvider<TEdge>` | Interface | Given a participant key and slot, enumerate edges where that participant fills that slot. |
| `IEntityGetter<TEntity>`           | Interface | Retrieve an entity by key (already implicit in resolver pattern).                         |

For each `recursive` or `chained` relationship, the runtime registers a default **naïve traversal**:

1. Uses `IRelationshipEdgeProvider` of each underlying reified relationship.
2. Iterates in memory (depth-first or breadth-first).
3. Applies depth limits and cycle detection (visited set).

### Override hook

```csharp
// Default — runtime traverses in-memory using edge providers
builder.RecursiveRelationship("ReportsTo", options => { ... });

// Override — database-backed implementation
builder.RecursiveRelationship("ReportsTo", options =>
{
    options.ResolveDescendants(async (key, depth) =>
    {
        // SQL recursive CTE or graph DB query
        return await _db.GetDescendantsAsync(key, depth);
    });
});
```

The override bypasses naïve traversal; user takes responsibility for depth limits and cycle handling.

### Summary

| Concern              | Default                           | Override                          |
|----------------------|-----------------------------------|-----------------------------------|
| Edge enumeration     | `IRelationshipEdgeProvider<T>`    | N/A — always user-provided        |
| Traversal            | Naïve in-memory graph walk        | Custom resolver (e.g. SQL CTE)    |
| `?expand` on results | Existing entity-resolver registry | Existing per-participant resolver |

