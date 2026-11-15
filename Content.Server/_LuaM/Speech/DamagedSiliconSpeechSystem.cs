using System.Text;
using Content.Server.Speech;
using Content.Shared.Damage;
using Content.Shared.Mobs.Systems;
using Robust.Shared.Random;

namespace Content.Server._LuaM.Speech;

public sealed partial class DamagedSiliconSpeechSystem : EntitySystem
{
    [Dependency] private MobThresholdSystem _thresholds = default!;
    [Dependency] private IRobustRandom _random = default!;

    private static readonly char[] GlitchChars = ['#', '%', '&', '@', '*', '$', '?', '!', '0', '1'];

    public override void Initialize()
    {
        base.Initialize();

        SubscribeLocalEvent<DamagedSiliconSpeechComponent, AccentGetEvent>(OnAccentGet);
    }

    private void OnAccentGet(Entity<DamagedSiliconSpeechComponent> ent, ref AccentGetEvent args)
    {
        var chance = GetCorruptionChance(ent);
        if (chance <= 0f)
            return;

        args.Message = Corrupt(args.Message, chance);
    }

    private float GetCorruptionChance(Entity<DamagedSiliconSpeechComponent> ent)
    {
        if (!TryComp<DamageableComponent>(ent, out var damageable)
            || !_thresholds.TryGetIncapThreshold(ent, out var incapThreshold))
            return 0f;

        var incap = incapThreshold.Value.Float();
        var start = incap * ent.Comp.StartFraction;
        var range = incap - start;
        var damage = damageable.TotalDamage.Float();
        if (range <= 0f || damage <= start)
            return 0f;

        var progress = Math.Clamp((damage - start) / range, 0f, 1f);
        return progress * ent.Comp.MaxCorruptionChance;
    }

    private string Corrupt(string message, float chance)
    {
        var builder = new StringBuilder(message.Length * 2);

        foreach (var ch in message)
        {
            if (!char.IsLetterOrDigit(ch) || !_random.Prob(chance))
            {
                builder.Append(ch);
                continue;
            }

            switch (_random.Next(3))
            {
                case 0: // stutter: "с-с-сбой"
                    var repeats = _random.Next(1, 3);
                    for (var i = 0; i < repeats; i++)
                    {
                        builder.Append(ch).Append('-');
                    }
                    builder.Append(ch);
                    break;
                case 1:
                    builder.Append(_random.Pick(GlitchChars));
                    break;
                default:
                    builder.Append(ch).Append(ch);
                    break;
            }
        }

        return builder.ToString();
    }
}
