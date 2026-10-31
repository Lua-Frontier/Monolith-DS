using Content.Shared.Interaction;
using Content.Shared.Verbs;
using Robust.Shared.Audio;
using Robust.Shared.Audio.Systems;
using Robust.Shared.Network;
using Robust.Shared.Utility;

namespace Content.Shared.Showers;

public abstract partial class SharedShowerSystem : EntitySystem
{
    [Dependency] private INetManager _net = default!;
    [Dependency] private SharedAppearanceSystem _appearance = default!;
    [Dependency] private SharedAudioSystem _audio = default!;

    public override void Initialize()
    {
        base.Initialize();

        SubscribeLocalEvent<ShowerComponent, MapInitEvent>(OnMapInit);
        SubscribeLocalEvent<ShowerComponent, GetVerbsEvent<AlternativeVerb>>(OnToggleShowerVerb);
        SubscribeLocalEvent<ShowerComponent, ActivateInWorldEvent>(OnActivateInWorld);
    }

    private void OnMapInit(Entity<ShowerComponent> ent, ref MapInitEvent args)
    {
        UpdateAppearance(ent);
    }

    private void OnToggleShowerVerb(Entity<ShowerComponent> ent, ref GetVerbsEvent<AlternativeVerb> args)
    {
        if (!args.CanInteract || !args.CanAccess || args.Hands == null)
            return;

        var user = args.User;
        args.Verbs.Add(new AlternativeVerb
        {
            Act = () => ToggleShowerHead(ent, user),
            Text = Loc.GetString(ent.Comp.ToggleShower ? "shower-turn-off" : "shower-turn-on"),
            Icon = new SpriteSpecifier.Texture(new ResPath(ent.Comp.ToggleShower
                ? "/Textures/Interface/VerbIcons/close.svg.192dpi.png"
                : "/Textures/Interface/VerbIcons/open.svg.192dpi.png")),
        });
    }

    private void OnActivateInWorld(Entity<ShowerComponent> ent, ref ActivateInWorldEvent args)
    {
        if (args.Handled)
            return;

        args.Handled = true;
        ToggleShowerHead(ent, args.User);
    }

    public void ToggleShowerHead(EntityUid uid, EntityUid? user = null, ShowerComponent? component = null)
    {
        if (!Resolve(uid, ref component))
            return;

        component.ToggleShower = !component.ToggleShower;
        Dirty(uid, component);

        _audio.PlayPredicted(component.EnableShowerSound, uid, user);
        UpdateAppearance((uid, component));
    }

    private void UpdateAppearance(Entity<ShowerComponent> ent)
    {
        _appearance.SetData(ent, ShowerVisuals.ShowerVisualState, ent.Comp.ToggleShower ? ShowerVisualState.On : ShowerVisualState.Off);

        if (_net.IsClient)
            return;

        if (ent.Comp.ToggleShower)
        {
            ent.Comp.PlayingStream ??= _audio.PlayPvs(ent.Comp.LoopingSound, ent, AudioParams.Default.WithLoop(true).WithMaxDistance(5))?.Entity;
            return;
        }

        ent.Comp.PlayingStream = _audio.Stop(ent.Comp.PlayingStream);
    }
}
