using Content.Shared.Humanoid;
using Content.Shared.Humanoid.Markings;
using Robust.Shared.Enums;
using Robust.Shared.Serialization;

namespace Content.Shared._LuaM.Announcements;

/// <summary>Appearance-only data: no live entity reference, location or carried storage contents.</summary>
[Serializable, NetSerializable]
public sealed class AnnouncementPortraitSnapshot
{
    public string Name = string.Empty;
    public string Prototype = string.Empty;
    public string? Species;
    public Sex Sex;
    public Gender Gender;
    public int Age = 18;
    public Color SkinColor = Color.White;
    public Color EyeColor = Color.Brown;
    public float Height = 1;
    public float Width = 1;
    public MarkingSet Markings = new();
    public Dictionary<HumanoidVisualLayers, CustomBaseLayerInfo> CustomBaseLayers = new();
    public HashSet<HumanoidVisualLayers> PermanentlyHidden = new();
    public List<AnnouncementPortraitItem> Equipment = new();
}

[Serializable, NetSerializable]
public sealed class AnnouncementPortraitItem
{
    public string Slot = string.Empty;
    public string Prototype = string.Empty;
    public string? EquippedPrefix;
}
