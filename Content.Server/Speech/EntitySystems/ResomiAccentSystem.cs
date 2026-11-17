using System.Text.RegularExpressions;
using Content.Server.Speech.Components;
using Robust.Shared.Random;

namespace Content.Server.Speech.EntitySystems;

public sealed partial class ResomiAccentSystem : EntitySystem
{

    [Dependency] private IRobustRandom _random = default!;

// LuaM-start
    private static readonly Regex RegexLowerRussianSilly = new Regex("глупый");
    private static readonly Regex RegexFirstCapRussianSilly = new Regex("Глупый");
    private static readonly Regex RegexUpperRussianSilly = new Regex("ГЛУПЫЙ");
// LuaM-end

    public override void Initialize()
    {
        base.Initialize();
        SubscribeLocalEvent<ResomiAccentComponent, AccentGetEvent>(OnAccent);
    }

    private void OnAccent(EntityUid uid, ResomiAccentComponent component, AccentGetEvent args)
    {
        var message = args.Message;

        if (_random.Prob(component.BawkChance))
        {

            // bawk
// LuaM-start
            message = RegexLowerRussianSilly.Replace(message, "глупый... бяк");
            message = RegexFirstCapRussianSilly.Replace(message, "Глупый... Бяк");
            message = RegexUpperRussianSilly.Replace(message, "ГЛУПЫЙ... БЯК");
// LuaM-end
        }

        args.Message = message;
    }
}