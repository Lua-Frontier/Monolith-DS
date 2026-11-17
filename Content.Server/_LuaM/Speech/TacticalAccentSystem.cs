using Content.Server.Speech;
using Content.Server.Speech.EntitySystems;
using Robust.Shared.Random;

namespace Content.Server._LuaM.Speech;

public sealed partial class TacticalAccentSystem : EntitySystem
{
    [Dependency] private IRobustRandom _random = default!;
    [Dependency] private ReplacementAccentSystem _replacement = default!;

    private static readonly string[] Suffixes =
    [
        "luam-accent-tactical-suffix-1",
        "luam-accent-tactical-suffix-2",
        "luam-accent-tactical-suffix-3",
    ];

    public override void Initialize()
    {
        base.Initialize();

        SubscribeLocalEvent<TacticalAccentComponent, AccentGetEvent>(OnAccentGet);
    }

    private void OnAccentGet(Entity<TacticalAccentComponent> ent, ref AccentGetEvent args)
    {
        var message = _replacement.ApplyReplacements(args.Message, "luamTactical");

        if (message.Length > 0 && _random.Prob(ent.Comp.SuffixChance))
            message = message.TrimEnd(' ', '.', '!', '?', '…') + Loc.GetString(_random.Pick(Suffixes));

        args.Message = message;
    }
}
