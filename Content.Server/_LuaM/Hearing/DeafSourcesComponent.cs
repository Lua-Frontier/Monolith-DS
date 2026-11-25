namespace Content.Server._LuaM.Hearing;

/// <summary>
/// Every reason the creature currently has to be deaf, managed by <see cref="DeafnessSystem"/>.
/// </summary>
[RegisterComponent, Access(typeof(DeafnessSystem))]
public sealed partial class DeafSourcesComponent : Component
{
    /// <summary>
    /// Each reason and how much of the normal volume it leaves. The quietest one wins.
    /// </summary>
    [ViewVariables]
    public Dictionary<string, float> Sources = new();

    /// <summary>
    /// Reasons that leave part of the speech audible. Speech is only partly heard when every reason is one of these.
    /// </summary>
    [ViewVariables]
    public HashSet<string> PartialSpeechSources = new();

    /// <summary>
    /// Whether these reasons made the creature deaf, so they do not cure a deafness they did not cause.
    /// </summary>
    [ViewVariables]
    public bool AddedDeaf;
}
