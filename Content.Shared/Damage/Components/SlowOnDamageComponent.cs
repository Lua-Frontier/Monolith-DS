using Content.Shared.FixedPoint;
using Robust.Shared.GameStates;

namespace Content.Shared.Damage.Components
{
    // TODO It'd be nice if this could be a destructible threshold, but on the other hand,
    // that doesn't really work with events at all, and
    [RegisterComponent, NetworkedComponent, AutoGenerateComponentState] // LuaM
    public sealed partial class SlowOnDamageComponent : Component
    {
        /// <summary>
        ///     Damage -> movespeed dictionary. This is -damage-, not -health-.
        /// </summary>
        // LuaM: networked, components added at runtime (cloning, polymorph) reached the client with a null dictionary
        [DataField("speedModifierThresholds", required: true), AutoNetworkedField]
        public Dictionary<FixedPoint2, float> SpeedModifierThresholds = new(); // LuaM: default! > new()
    }
}
