using Content.Server.Speech;
using Content.Server.Speech.EntitySystems;

namespace Content.Server._LuaM.Speech;

public sealed partial class ArcanAccentSystem : EntitySystem
{
    [Dependency] private ReplacementAccentSystem _replacement = default!;

    public override void Initialize()
    {
        base.Initialize();

        SubscribeLocalEvent<ArcanAccentComponent, AccentGetEvent>(OnAccentGet);
    }

    private void OnAccentGet(Entity<ArcanAccentComponent> ent, ref AccentGetEvent args)
    {
        args.Message = _replacement.ApplyReplacements(args.Message, "luamArcan");
    }
}
