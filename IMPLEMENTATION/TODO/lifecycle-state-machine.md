# Compile and enforce lifecycle state machines (Activatable & friends) — OPEN

**Status: open / large.** Today a lifecycle is a *marker*, not a *behavior*. This item is about making
the declared state machine actually mean something at runtime and in the generated OpenAPI.

## The gap

A `lifecycle:` clause currently collapses to a single boolean — "does this resource have a lifecycle?" —
everywhere it's consumed:

- **Parsing** turns `lifecycle: Activatable` into `ArchetypeReferenceLifecycleSyntax(["Activatable"])`
  (`EidosGrammarParser.cs:494`). It's just the *name* as a string; nothing resolves what `Activatable` means.
  In the HR sample `Activatable` isn't even declared as an `archetype` in the document.
- **Route wiring** maps `PUT /{collection}/{key}/_state` to the user's `.Transition(...)` handler
  (`EidosMapBuilder.cs:488`), gated only by `Members.OfType<EntityLifecycleMemberSyntax>().Any()`
  (`EidosMapBuilder.cs:97`) — presence, not content.
- **Coverage** (`DefaultEidosOperationPolicy.HasLifecycle`, `IEidosOperationPolicy.cs:46`) returns `true` for
  any lifecycle clause regardless of name or contents, promoting `PutState` to a required operation.
- **The handler is the whole implementation.** `TransitionPerson` does `existing with { State = request.State }`
  (`HumanResourcesService.cs:95`) — it writes whatever state is PUT. Nothing checks that the target state
  exists, that the transition is legal from the current state, that a guard passes, or that a non-deterministic
  machine was given the disambiguating `transition` name.

What *is* built but unused on the request path: `LifecycleAnalyzer` (`Analysis/LifecycleAnalyzer.cs`) resolves
archetype references against same-document `archetype` declarations and classifies a machine as
Deterministic / NonDeterministic / Ambiguous (`Analysis/LifecycleAnalysisResult.cs`, spec §5.1). The rich AST is
already there: `StateDeclarationSyntax`, `TransitionDeclarationSyntax` (source state-set → target, with
`GuardTransitionOptionSyntax` / `EmitsTransitionOptionSyntax`), `InitialClauseSyntax`, and per-property
`WritableInOptionSyntax` / `VisibleInOptionSyntax` (`EidosSyntax.cs:182`–`236`). The analyzer only emits
diagnostics — it never produces a *resolved machine model* anyone can execute against, and `OpenApiDocumentGenerator`
notes that archetype-based operation pruning and per-state schemas "are not yet applied" (`OpenApiDocumentGenerator.cs:14`).

## Architectural crux to settle first

The framework does **not** own storage — the handler reads/writes the entity. So the framework can't know an
endurant's *current* state without help, which is what enforcement needs (legal-transition checks are
`(currentState, target, transitionName)`). Two shapes:

- **(A) Framework-enforced.** Add a "current-state accessor" hook the app supplies (e.g. `Func<string, Task<string?>>`
  per resource, or read it off the resource the handler already loads). The framework validates the transition
  against the resolved machine *before* invoking the handler and rejects illegal ones with a 409/422.
- **(B) Handler-enforced with a helper.** Surface the resolved machine to the handler (e.g. inject an
  `ILifecycleMachine` for the resource) and let the handler call `machine.Validate(current, target, name)`. Less
  magic, no storage hook, but enforcement is opt-in.

Recommendation: **(B) first** (a resolved model + a validation helper — low risk, no new storage contract),
then **(A)** as an opt-in convenience once the model exists. Decide before building.

## Suggested phases

1. **Resolved lifecycle model + resolver.** Introduce an immutable `ResolvedLifecycle` (initial state, set of
   states, transitions as `(name, sourceStates, target, guards, emits)`) and a resolver that turns a
   `LifecycleClauseSyntax` into one: inline clause → direct; archetype reference → look up the `archetype`
   declaration. Extend `LifecycleAnalysisResult` (or add a sibling) to carry this model, not just the
   classification. Reuse the existing classification pass.

2. **Built-in archetype library (or strict resolution).** `Activatable` is referenced but undeclared, yet the
   sample works because nothing resolves it. Decide: ship standard archetypes (`Activatable`, etc.) as built-in
   declarations the resolver knows, OR make undeclared-archetype a hard error. (Spec §5.3 / §7.1 — also note
   archetype *composition* `A + B` is explicitly TBD; `LifecycleAnalyzer.cs:86` already warns and skips it.)

3. **Transition enforcement at `PUT /_state`.** Using the resolved model: reject unknown target states and
   illegal transitions; for `NonDeterministic` machines require the `transition` field (already optional on
   `StateTransitionRequest`) and 422 when missing/ambiguous; `Ambiguous` machines are a build-time error.
   Wire via shape (A) or (B) above.

4. **Per-state field schemas.** Honor `writable-in` / `visible-in`: `RepresentationWriter` filters response
   fields by current state; `OpenApiDocumentGenerator` emits per-state request/response schemas and prunes
   operations not valid in a state (the file's stated not-yet-done work).

5. **Guards, emits, coupling (advanced, later).** Evaluate `GuardTransitionOptionSyntax`, surface
   `EmitsTransitionOptionSyntax` events, and handle relationship→participant coupling
   (`CouplingRuleSyntax`, `ParticipantStateGuardSyntax`). Likely its own follow-up item.

## Out of scope for the first cut
- Archetype composition (`A + B`) — keep the existing "not analyzed" warning until the spec settles it.
- Event emission/coupling (phase 5) — track separately if it grows.

Driver: discovered while explaining why `PUT /persons/ada/_state` works even though the `Activatable`
archetype is never compiled — it works purely because a lifecycle is *present*, not because its machine is
enforced.
