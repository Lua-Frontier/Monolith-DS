using Content.Server.GameTicking;
using Content.Server.GameTicking.Events;
using Content.Shared.Dataset;
using Content.Shared.Parallax;
using Robust.Server.GameObjects;
using Robust.Shared.Prototypes;
using Robust.Shared.Random;

namespace Content.Server._LuaM.Parallax;

public sealed partial class RoundParallaxSystem : EntitySystem
{
    [Dependency] private GameTicker _gameTicker = default!;
    [Dependency] private IPrototypeManager _prototype = default!;
    [Dependency] private IRobustRandom _random = default!;
    [Dependency] private MapSystem _map = default!;

    private static readonly ProtoId<DatasetPrototype> Parallaxes = "LuaMRoundParallaxes";

    public override void Initialize()
    {
        base.Initialize();

        SubscribeLocalEvent<RoundStartingEvent>(OnRoundStarting);
    }

    private void OnRoundStarting(RoundStartingEvent args)
    {
        if (!_map.TryGetMap(_gameTicker.DefaultMap, out var map) ||
            !_prototype.TryIndex(Parallaxes, out var parallaxes) ||
            parallaxes.Values.Count == 0)
        {
            return;
        }

        var parallax = EnsureComp<ParallaxComponent>(map.Value);
        parallax.Parallax = _random.Pick(parallaxes.Values);
        Dirty(map.Value, parallax);
    }
}
