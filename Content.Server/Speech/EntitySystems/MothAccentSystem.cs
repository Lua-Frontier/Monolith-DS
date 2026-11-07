using System.Text.RegularExpressions;
using Content.Server.Speech.Components;
using Robust.Shared.Random; // LuaM
using Content.Shared.Speech; // LuaM

namespace Content.Server.Speech.EntitySystems;

public sealed partial class MothAccentSystem : EntitySystem
{
    private static readonly Regex RegexLowerBuzz = new Regex("z{1,3}");
    private static readonly Regex RegexUpperBuzz = new Regex("Z{1,3}");
    // LuaM-start
    private static readonly Regex RegexRuLowerZh = new("ж+");
    private static readonly Regex RegexRuUpperZh = new("Ж+");
    private static readonly Regex RegexRuLowerZ = new("з+");
    private static readonly Regex RegexRuUpperZ = new("З+");
    private static readonly string[] RuLowerZh = ["жж", "жжж"];
    private static readonly string[] RuUpperZh = ["ЖЖ", "ЖЖЖ"];
    private static readonly string[] RuLowerZ = ["зз", "ззз"];
    private static readonly string[] RuUpperZ = ["ЗЗ", "ЗЗЗ"];
    // LuaM-end

    [Dependency] private IRobustRandom _random = default!; // LuaM

    public override void Initialize()
    {
        base.Initialize();
        SubscribeLocalEvent<MothAccentComponent, AccentGetEvent>(OnAccent);
    }

    private void OnAccent(EntityUid uid, MothAccentComponent component, AccentGetEvent args)
    {
        var message = args.Message;

        // buzzz
        message = RegexLowerBuzz.Replace(message, "zzz");
        // buZZZ
        message = RegexUpperBuzz.Replace(message, "ZZZ");

// LuaM-start:
        message = RegexRuLowerZh.Replace(message, _random.Pick(RuLowerZh));
        message = RegexRuUpperZh.Replace(message, _random.Pick(RuUpperZh));
        message = RegexRuLowerZ.Replace(message, _random.Pick(RuLowerZ));
        message = RegexRuUpperZ.Replace(message, _random.Pick(RuUpperZ));
// LuaM-end.

        args.Message = message;
    }
}
