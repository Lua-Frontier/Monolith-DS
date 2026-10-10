namespace Content.Server._LuaM.Research;

/// <summary>
/// Pulls research point disks lying near the R&amp;D server and adds their points to it.
/// </summary>
[RegisterComponent]
public sealed partial class ResearchServerMagnetComponent : Component
{
    [ViewVariables(VVAccess.ReadWrite), DataField]
    public float Range = 1.5f;

    [ViewVariables(VVAccess.ReadWrite), DataField]
    public bool MagnetEnabled = true;

    [ViewVariables(VVAccess.ReadWrite), DataField]
    public TimeSpan ScanDelay = TimeSpan.FromSeconds(1);

    [ViewVariables]
    public TimeSpan NextScan = TimeSpan.Zero;
}
