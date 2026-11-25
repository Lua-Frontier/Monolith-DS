using System.Linq;
using Content.Shared._LuaM.Hearing;

namespace Content.Server._LuaM.Hearing;

/// <summary>
/// Keeps <see cref="DeafComponent"/> in line with every reason the creature has to be deaf,
/// so taking one reason away (a hearing aid against inborn deafness) never cures another (a berserk heart).
/// </summary>
public sealed partial class DeafnessSystem : EntitySystem
{
    /// <param name="partialSpeech">The creature still makes out part of the speech, like after a concussion.</param>
    public void AddSource(EntityUid uid, string source, float soundVolume, bool partialSpeech = false)
    {
        if (TerminatingOrDeleted(uid))
            return;

        var sources = EnsureComp<DeafSourcesComponent>(uid);
        if (sources.Sources.Count == 0)
            sources.AddedDeaf = !HasComp<DeafComponent>(uid);

        sources.Sources[source] = soundVolume;
        if (partialSpeech)
            sources.PartialSpeechSources.Add(source);
        else
            sources.PartialSpeechSources.Remove(source);

        Apply((uid, sources));
    }

    public void RemoveSource(EntityUid uid, string source)
    {
        if (!TryComp<DeafSourcesComponent>(uid, out var sources) || !sources.Sources.Remove(source))
            return;

        sources.PartialSpeechSources.Remove(source);
        Apply((uid, sources));
    }

    private void Apply(Entity<DeafSourcesComponent> ent)
    {
        if (TerminatingOrDeleted(ent))
            return;

        if (ent.Comp.Sources.Count == 0)
        {
            if (ent.Comp.AddedDeaf)
                RemComp<DeafComponent>(ent);

            RemComp(ent, ent.Comp);
            return;
        }

        if (!ent.Comp.AddedDeaf)
            return;

        var deaf = EnsureComp<DeafComponent>(ent);
        deaf.SoundVolume = ent.Comp.Sources.Values.Min();
        deaf.PartialSpeech = ent.Comp.Sources.Keys.All(ent.Comp.PartialSpeechSources.Contains);
        Dirty(ent, deaf);
    }
}
