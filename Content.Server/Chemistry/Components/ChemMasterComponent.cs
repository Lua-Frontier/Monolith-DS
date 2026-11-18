using Content.Server.Chemistry.EntitySystems;
using Content.Shared.Chemistry;
using Content.Shared.Chemistry.Reagent; // LuaM
using Robust.Shared.Audio;

namespace Content.Server.Chemistry.Components
{
    /// <summary>
    /// An industrial grade chemical manipulator with pill and bottle production included.
    /// <seealso cref="ChemMasterSystem"/>
    /// </summary>
    [RegisterComponent]
    [Access(typeof(ChemMasterSystem))]
    public sealed partial class ChemMasterComponent : Component
    {
        [DataField("pillType"), ViewVariables(VVAccess.ReadWrite)]
        public uint PillType = 0;

        [DataField("mode"), ViewVariables(VVAccess.ReadWrite)]
        public ChemMasterMode Mode = ChemMasterMode.Transfer;

        [DataField]
        public ChemMasterSortingType SortingType = ChemMasterSortingType.None;

        [DataField("pillDosageLimit", required: true), ViewVariables(VVAccess.ReadWrite)]
        public uint PillDosageLimit;

        [DataField("clickSound"), ViewVariables(VVAccess.ReadWrite)]
        public SoundSpecifier ClickSound = new SoundPathSpecifier("/Audio/Machines/machine_switch.ogg");
		
        /// <summary>
        /// Which source the chem master should draw from when making pills/bottles.
        /// </summary>
        [DataField]
        public ChemMasterDrawSource DrawSource = ChemMasterDrawSource.Internal;

        /// <summary>
        /// LuaM: buffer reagents marked with a star. A list because <see cref="ReagentId"/> hashes are not stable.
        /// </summary>
        [DataField]
        public List<ReagentId> FavoriteReagents = new();
    }
}
