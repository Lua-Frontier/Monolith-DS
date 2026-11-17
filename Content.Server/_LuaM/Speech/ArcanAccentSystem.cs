using Content.Server.Speech;
using Content.Server.Speech.EntitySystems;
using Robust.Shared.Random;

namespace Content.Server._LuaM.Speech;

public sealed partial class ArcanAccentSystem : EntitySystem
{
    [Dependency] private IRobustRandom _random = default!;
    [Dependency] private ReplacementAccentSystem _replacement = default!;

    private static readonly string[] Prefixes =
    [
        "luam-accent-arcan-prefix-1",
        "luam-accent-arcan-prefix-2",
        "luam-accent-arcan-prefix-3",
        "luam-accent-arcan-prefix-4",
    ];

    public override void Initialize()
    {
        base.Initialize();

        SubscribeLocalEvent<ArcanAccentComponent, AccentGetEvent>(OnAccentGet);
    }

    private void OnAccentGet(Entity<ArcanAccentComponent> ent, ref AccentGetEvent args)
    {
        var message = _replacement.ApplyReplacements(args.Message, "luamArcan");

        if (message.Length > 0 && _random.Prob(ent.Comp.PrefixChance))
        {
            var first = KeepsCapital(message) ? message : char.ToLower(message[0]) + message[1..];
            message = Loc.GetString(_random.Pick(Prefixes)) + " " + first;
        }

        args.Message = message;
    }

    private static bool KeepsCapital(string message)
    {
        var end = 0;
        while (end < message.Length && char.IsLetter(message[end]))
            end++;

        var word = message[..end];
        return word is "Вы" or "Вас" or "Вам" or "Вами" || end > 1 && word.ToUpperInvariant() == word;
    }
}
