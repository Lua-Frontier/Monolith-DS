namespace Content.Server.Speech.Components
{
    [RegisterComponent]
    public sealed partial class StutteringAccentComponent : Component
    {
        /// <summary>
        /// Percentage chance that a stutter will occur if it matches.
        /// </summary>
        [DataField("matchRandomProb")]
        [ViewVariables(VVAccess.ReadWrite)]
        public float MatchRandomProb = 0.25f; // LuaM: 0.8 > 0.25, only word starts

        /// <summary>
        /// Percentage chance that a stutter occurs f-f-f-f-four times.
        /// </summary>
        [DataField("fourRandomProb")]
        [ViewVariables(VVAccess.ReadWrite)]
        public float FourRandomProb = 0.03f; // LuaM: 0.1 > 0.03

        /// <summary>
        /// Percentage chance that a stutter occurs t-t-t-three times.
        /// </summary>
        [DataField("threeRandomProb")]
        [ViewVariables(VVAccess.ReadWrite)]
        public float ThreeRandomProb = 0.15f; // LuaM: 0.2 > 0.15

        /// <summary>
        /// Percentage chance that a stutter cut off.
        /// </summary>
        [DataField("cutRandomProb")]
        [ViewVariables(VVAccess.ReadWrite)]
        public float CutRandomProb = 0.01f; // LuaM: 0.05 > 0.01
    }
}
