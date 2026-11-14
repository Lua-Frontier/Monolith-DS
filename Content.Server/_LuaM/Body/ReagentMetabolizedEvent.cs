using Content.Shared.Chemistry.Reagent;
using Content.Shared.FixedPoint;
using Robust.Shared.Prototypes;

namespace Content.Server._LuaM.Body;

/// <summary>
/// Raised on a body every time one of its organs metabolizes some of a reagent.
/// </summary>
[ByRefEvent]
public readonly record struct ReagentMetabolizedEvent(ProtoId<ReagentPrototype> Reagent, FixedPoint2 Amount);
