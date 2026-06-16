# SPEC TODO — Chained relationship: filters, self-joins, and pivots

> **Status:** Deferred / parking lot. Not part of the first cut.
> **Source:** Split out of the R7 (chained relationships) recommendation in
> [`../proposals/eidos-roles-and-transitive-relationships.md`](../proposals/eidos-roles-and-transitive-relationships.md)
> so the proposal can be woven into a v0.2 standard without these unresolved questions distracting
> from the core. **Blocked on** the §7.3 expression-language decision in the base spec.

## Context

The proposal recommends shipping `chained relationship` as **linear hop chains only**: a `path` is an
ordered list of hops, each walking one already-declared reified relationship from one participant slot
to another, with the exit type of each hop matching the entry type of the next. That case needs **no**
expression language and **no** filtering — it is pure type-continuity checking.

Everything below is the part that was deliberately taken *out* of the first cut.

## What is deferred

### 1. Self-joins around a shared pivot

Some useful chained relationships are not a single linear chain but a **V-shape** that meets at a
common pivot entity. The motivating example:

> **Colleagues** — two `Person`s are colleagues if they are employed by the *same* `Organization`.

```
// NOT yet expressible — illustration only
chained relationship Colleagues between a: Person, b: Person {
  // two Employment hops that must terminate at the SAME Organization (the pivot)
  // ...plus a filter to exclude a == b (a person is not their own colleague)
}
```

This needs two capabilities the linear hop model lacks:

- **Shared-pivot binding** — a way to state that two hops must converge on the *same* intermediate
  endurant, rather than just chaining end to end.
- **Filtering** — a `where` clause to drop trivial/unwanted tuples (here, `a == b`).

### 2. The `where` filter and the expression-language question

The firm constraint, when this is eventually designed: **do not introduce a second expression
language.** Eidos already has exactly one — the `guard-expr` of base-spec §4.6:

```
guard-expr  =  guard-atom *( ( "&&" / "||" ) guard-atom )
guard-atom  =  name                       ; named predicate, resolved externally
            /  participant-state-guard     ; name.state in [ ... ]
            /  "!" guard-atom
            /  "(" guard-expr ")"
```

Any `where` clause on a chained relationship **must reuse this grammar**, not a parallel one.

**The blocker:** `guard-expr` has **no value-comparison operator** — there is no `==` or `!=` in
v0.1. So `where a != b` is *not* expressible with today's grammar; it would be new syntax. Whether
Eidos gains a structural comparison language (property comparisons, identity comparisons, role
membership tests) is exactly the open item in base-spec **§7.3 (Guard predicate language)**. This
feature is therefore blocked on §7.3.

### 3. Interim workaround (no new syntax)

Until §7.3 is settled, a filtered/self-join chain could still be expressed **without** any new
operators by leaning on the existing *named predicate* escape hatch — a guard name resolved
server-side, exactly as lifecycle guards already work:

```
where: distinctParticipants      // a named predicate, resolved externally — no new grammar
```

This keeps the door open without committing the language to comparison syntax. It is noted as an
option, not a recommendation.

## Resolution checklist (for whoever picks this up)

- [ ] §7.3 decided: does Eidos get a structural comparison language, or stay name-only?
- [ ] If structural: define shared-pivot binding syntax in the `path`.
- [ ] Define `where` as a `guard-expr` (reused, not forked) and confirm no value-comparison leaks in
      unless §7.3 added it.
- [ ] Decide whether the interim `where: <namedPredicate>` form ships in the meantime.
- [ ] Re-fold the resolved design back into the R7 section of the proposal before v0.2 freeze.

## Out of scope for this TODO

The linear-hop-chain core, the `chained` keyword, the read-only/identity-less semantics, and the
endpoint surface are all settled in the proposal and are **not** revisited here.
