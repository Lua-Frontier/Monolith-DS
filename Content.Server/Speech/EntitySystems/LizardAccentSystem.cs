using System.Text.RegularExpressions;
using Content.Server.Speech.Components;
using Robust.Shared.Random; // LuaM
using Content.Shared.Speech; // LuaM

namespace Content.Server.Speech.EntitySystems;

public sealed partial class LizardAccentSystem : EntitySystem
{
    private static readonly Regex RegexLowerS = new("s+");
    private static readonly Regex RegexUpperS = new("S+");
    private static readonly Regex RegexInternalX = new(@"(\w)x");
    private static readonly Regex RegexLowerEndX = new(@"\bx([\-|r|R]|\b)");
    private static readonly Regex RegexUpperEndX = new(@"\bX([\-|r|R]|\b)");
    // LuaM-start
    private static readonly Regex RegexRuLowerS = new("с+");
    private static readonly Regex RegexRuUpperS = new("С+");
    private static readonly Regex RegexRuLowerZ = new("з+");
    private static readonly Regex RegexRuUpperZ = new("З+");
    private static readonly Regex RegexRuLowerSh = new("ш+");
    private static readonly Regex RegexRuUpperSh = new("Ш+");
    private static readonly Regex RegexRuLowerCh = new("ч+");
    private static readonly Regex RegexRuUpperCh = new("Ч+");
    private static readonly string[] RuLowerHiss = ["сс", "ссс"];
    private static readonly string[] RuUpperHiss = ["СС", "ССС"];
    private static readonly string[] RuLowerShh = ["шш", "шшш"];
    private static readonly string[] RuUpperShh = ["ШШ", "ШШШ"];
    private static readonly string[] RuLowerShch = ["щщ", "щщщ"];
    private static readonly string[] RuUpperShch = ["ЩЩ", "ЩЩЩ"];
    // LuaM-end

    [Dependency] private IRobustRandom _random = default!; // LuaM

    public override void Initialize()
    {
        base.Initialize();
        SubscribeLocalEvent<LizardAccentComponent, AccentGetEvent>(OnAccent);
    }

    private void OnAccent(EntityUid uid, LizardAccentComponent component, AccentGetEvent args)
    {
        var message = args.Message;

        // hissss
        message = RegexLowerS.Replace(message, "sss");
        // hiSSS
        message = RegexUpperS.Replace(message, "SSS");
        // ekssit
        message = RegexInternalX.Replace(message, "$1kss");
        // ecks
        message = RegexLowerEndX.Replace(message, "ecks$1");
        // eckS
        message = RegexUpperEndX.Replace(message, "ECKS$1");

// LuaM-start:
        message = RegexRuLowerS.Replace(message, _random.Pick(RuLowerHiss));
        message = RegexRuUpperS.Replace(message, _random.Pick(RuUpperHiss));
        message = RegexRuLowerZ.Replace(message, _random.Pick(RuLowerHiss));
        message = RegexRuUpperZ.Replace(message, _random.Pick(RuUpperHiss));
        message = RegexRuLowerSh.Replace(message, _random.Pick(RuLowerShh));
        message = RegexRuUpperSh.Replace(message, _random.Pick(RuUpperShh));
        message = RegexRuLowerCh.Replace(message, _random.Pick(RuLowerShch));
        message = RegexRuUpperCh.Replace(message, _random.Pick(RuUpperShch));
// LuaM-end.

        args.Message = message;
    }
}
