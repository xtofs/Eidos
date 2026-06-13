namespace Eidos.AspNetCore;

public sealed record StateTransitionRequest(string State, string? Transition = null);
