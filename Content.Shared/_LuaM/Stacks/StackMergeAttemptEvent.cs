namespace Content.Shared._LuaM.Stacks;

/// <summary>
/// Raised on the stack that would receive items from <see cref="Donor"/> before two stacks of the same type merge.
/// Cancel it when the two stacks hold things that only look alike, e.g. produce with different genes.
/// </summary>
[ByRefEvent]
public record struct StackMergeAttemptEvent(EntityUid Donor, bool Cancelled = false);
