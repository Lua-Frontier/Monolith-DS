using Robust.Shared.Prototypes;

namespace Content.Shared._LuaM.Container.Components
{
    /// <summary>
    /// Grants the listed components to entity if it NOT inside any container
    /// for example: brain has Blip if he not in the containers (other words, brain in space)
    /// <summary>
    [RegisterComponent]
    public sealed partial class OutOfContainerGrantComponent : Component
    {
        [DataField(required: true)]
        [AlwaysPushInheritance]
        public ComponentRegistry Components { get; private set; } = new();

        [ViewVariables(VVAccess.ReadWrite)]
        public Dictionary<string, bool> Active = new();
    }
}
