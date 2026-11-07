using System.Text.RegularExpressions;
using Content.Server.Speech.Components;
using Robust.Shared.Random; // LuaM
using Content.Shared.Speech; // LuaM

namespace Content.Server.Speech.EntitySystems;

public sealed partial class FrontalLispSystem : EntitySystem
{
    // @formatter:off
    private static readonly Regex RegexUpperTh = new(@"[T]+[Ss]+|[S]+[Cc]+(?=[IiEeYy]+)|[C]+(?=[IiEeYy]+)|[P][Ss]+|([S]+[Tt]+|[T]+)(?=[Ii]+[Oo]+[Uu]*[Nn]*)|[C]+[Hh]+(?=[Ii]*[Ee]*)|[Z]+|[S]+|[X]+(?=[Ee]+)");
    private static readonly Regex RegexLowerTh = new(@"[t]+[s]+|[s]+[c]+(?=[iey]+)|[c]+(?=[iey]+)|[p][s]+|([s]+[t]+|[t]+)(?=[i]+[o]+[u]*[n]*)|[c]+[h]+(?=[i]*[e]*)|[z]+|[s]+|[x]+(?=[e]+)");
    private static readonly Regex RegexUpperEcks = new(@"[E]+[Xx]+[Cc]*|[X]+");
    private static readonly Regex RegexLowerEcks = new(@"[e]+[x]+[c]*|[x]+");
    // LuaM-start
    private static readonly Regex RegexRuLowerT = new(@"\B[т](?![АЕЁИОУЫЭЮЯаеёиоуыэюя])");
    private static readonly Regex RegexRuUpperT = new(@"\B[Т](?![АЕЁИОУЫЭЮЯаеёиоуыэюя])");
    // LuaM-end
    // @formatter:on

    [Dependency] private IRobustRandom _random = default!; // LuaM

    public override void Initialize()
    {
        base.Initialize();
        SubscribeLocalEvent<FrontalLispComponent, AccentGetEvent>(OnAccent);
    }

    private void OnAccent(EntityUid uid, FrontalLispComponent component, AccentGetEvent args)
    {
        var message = args.Message;

        // handles ts, sc(i|e|y), c(i|e|y), ps, st(io(u|n)), ch(i|e), z, s
        message = RegexUpperTh.Replace(message, "Th");
        message = RegexLowerTh.Replace(message, "th");
        // handles ex(c), x
        message = RegexUpperEcks.Replace(message, "Ekth");
        message = RegexLowerEcks.Replace(message, "ekth");

// LuaM-start:
        message = message.Replace('с', _random.Prob(0.90f) ? 'ш' : 'с');
        message = message.Replace('С', _random.Prob(0.90f) ? 'Ш' : 'С');
        message = message.Replace('ч', _random.Prob(0.90f) ? 'ш' : 'ч');
        message = message.Replace('Ч', _random.Prob(0.90f) ? 'Ш' : 'Ч');
        message = message.Replace('ц', _random.Prob(0.90f) ? 'ч' : 'ц');
        message = message.Replace('Ц', _random.Prob(0.90f) ? 'Ч' : 'Ц');
        message = RegexRuLowerT.Replace(message, _random.Prob(0.90f) ? "ч" : "т");
        message = RegexRuUpperT.Replace(message, _random.Prob(0.90f) ? "Ч" : "Т");
        message = message.Replace('з', _random.Prob(0.90f) ? 'ж' : 'з');
        message = message.Replace('З', _random.Prob(0.90f) ? 'Ж' : 'З');
// LuaM-end.

        args.Message = message;
    }
}
