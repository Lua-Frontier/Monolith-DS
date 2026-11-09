using System.Diagnostics.CodeAnalysis;
using System.Linq;
using System.Numerics;
using Content.Client.DisplacementMap;
using Content.Client.Inventory;
using Content.Shared.Clothing;
using Content.Shared.Clothing.Components;
using Content.Shared.Clothing.EntitySystems;
using Content.Shared.DisplacementMap;
using Content.Shared.Humanoid;
using Content.Shared.Inventory;
using Content.Shared.Inventory.Events;
using Content.Shared.Item;
using Robust.Client.GameObjects;
using Robust.Client.Graphics;
using Robust.Client.ResourceManagement;
using Robust.Shared.Serialization.Manager;
using Robust.Shared.Serialization.TypeSerializers.Implementations;
using Robust.Shared.Utility;
using static Robust.Client.GameObjects.SpriteComponent;

namespace Content.Client.Clothing;

public sealed partial class ClientClothingSystem : ClothingSystem
{
    public const string Jumpsuit = "jumpsuit";

    /// <summary>
    /// This is a shitty hotfix written by me (Paul) to save me from renaming all files.
    /// For some context, im currently refactoring inventory. Part of that is slots not being indexed by a massive enum anymore, but by strings.
    /// Problem here: Every rsi-state is using the old enum-names in their state. I already used the new inventoryslots ALOT. tldr: its this or another week of renaming files.
    /// </summary>
    private static readonly Dictionary<string, string> TemporarySlotMap = new()
    {
        {"head", "HELMET"},
        {"eyes", "EYES"},
        {"ears", "EARS"},
        {"mask", "MASK"},
        {"outerClothing", "OUTERCLOTHING"},
        {Jumpsuit, "INNERCLOTHING"},
        {"neck", "NECK"},
        {"neck2", "NECK"},
        {"neck3", "NECK"},
        {"back", "BACKPACK"},
        {"belt", "BELT"},
        {"gloves", "HAND"},
        {"shoes", "FEET"},
        {"id", "IDCARD"},
        {"pocket1", "POCKET1"},
        {"pocket2", "POCKET2"},
        {"suitstorage", "SUITSTORAGE"},
    };

    [Dependency] private IResourceCache _cache = default!;
    [Dependency] private InventorySystem _inventorySystem = default!;
    [Dependency] private DisplacementMapSystem _displacement = default!;
    [Dependency] private SpriteSystem _sprite = default!;

    public override void Initialize()
    {
        base.Initialize();

        SubscribeLocalEvent<ClothingComponent, GetEquipmentVisualsEvent>(OnGetVisuals);
        SubscribeLocalEvent<ClothingComponent, InventoryTemplateUpdated>(OnInventoryTemplateUpdated);

        SubscribeLocalEvent<InventoryComponent, VisualsChangedEvent>(OnVisualsChanged);
        SubscribeLocalEvent<SpriteComponent, DidUnequipEvent>(OnDidUnequip);
        SubscribeLocalEvent<InventoryComponent, AppearanceChangeEvent>(OnAppearanceUpdate);
    }

    private void OnAppearanceUpdate(EntityUid uid, InventoryComponent component, ref AppearanceChangeEvent args)
    {
        // May need to update displacement maps if the sex changed. Also required to properly set the stencil on init
        if (args.Sprite == null)
            return;

        UpdateAllSlots(uid, component);

        // No clothing equipped -> make sure the layer is hidden, though this should already be handled by on-unequip.
        if (_sprite.LayerMapTryGet((uid, args.Sprite), HumanoidVisualLayers.StencilMask, out var layer, false))
        {
            DebugTools.Assert(!args.Sprite[layer].Visible);
            _sprite.LayerSetVisible((uid, args.Sprite), layer, false);
        }
    }

    private void OnInventoryTemplateUpdated(Entity<ClothingComponent> ent, ref InventoryTemplateUpdated args)
    {
        UpdateAllSlots(ent.Owner, clothing: ent.Comp);
    }

    private void UpdateAllSlots(
        EntityUid uid,
        InventoryComponent? inventoryComponent = null,
        ClothingComponent? clothing = null)
    {
        var enumerator = _inventorySystem.GetSlotEnumerator((uid, inventoryComponent));
        while (enumerator.NextItem(out var item, out var slot))
        {
            RenderEquipment(uid, item, slot.Name, inventoryComponent, clothingComponent: clothing);
        }
    }

    private void OnGetVisuals(EntityUid uid, ClothingComponent item, GetEquipmentVisualsEvent args)
    {
        if (!TryComp(args.Equipee, out InventoryComponent? inventory))
            return;

        List<PrototypeLayerData>? layers = null;
        var speciesSpecific = false; // LuaM

        // first attempt to get species specific data.
        if (inventory.SpeciesId != null
            && item.ClothingVisuals.TryGetValue($"{args.Slot}-{inventory.SpeciesId}", out var speciesLayers)
            && HasLayerStates(uid, speciesLayers)) // LuaM
        {
            layers = speciesLayers;
            speciesSpecific = true; // LuaM
        }

        // if that returned nothing, attempt to find generic data
        if (layers == null && !item.ClothingVisuals.TryGetValue(args.Slot, out layers))
        {
            // No generic data either. Attempt to generate defaults from the item's RSI & item-prefixes
            if (!TryGetDefaultVisuals(uid, item, args.Slot, inventory.SpeciesId, out layers))
                return;
        }
        else if (!speciesSpecific && inventory.SpeciesId != null) // LuaM
            layers = GetSpeciesStateLayers(item, layers, inventory.SpeciesId); // LuaM

        // add each layer to the visuals
        var i = 0;
        foreach (var layer in layers)
        {
            var key = layer.MapKeys?.FirstOrDefault();
            if (key == null)
            {
                // using the $"{args.Slot}" layer key as the "bookmark" for layer ordering until layer draw depths get added
                key = $"{args.Slot}-{i}";
                i++;
            }

            item.MappedLayer = key;
            args.Layers.Add((key, layer));
        }
    }

    // LuaM-start
    // -- явные clothingVisuals раньше никогда не брали состояния вида "equipped-BALACLAVA-vulpkanin", и одежду мяла карта смещения
    private List<PrototypeLayerData> GetSpeciesStateLayers(ClothingComponent clothing, List<PrototypeLayerData> layers, string speciesId)
    {
        List<PrototypeLayerData>? result = null;
        for (var i = 0; i < layers.Count; i++)
        {
            var layer = layers[i];
            var rsiPath = layer.RsiPath ?? clothing.RsiPath;
            if (layer.State == null || layer.TexturePath != null || rsiPath == null)
                continue;

            var speciesState = $"{layer.State}-{speciesId}";
            if (!_cache.GetResource<RSIResource>(SpriteSpecifierSerializer.TextureRoot / rsiPath).RSI.TryGetState(speciesState, out _))
                continue;

            result ??= new List<PrototypeLayerData>(layers);
            result[i] = new PrototypeLayerData
            {
                Shader = layer.Shader,
                RsiPath = layer.RsiPath,
                State = speciesState,
                Scale = layer.Scale,
                Rotation = layer.Rotation,
                Offset = layer.Offset,
                Visible = layer.Visible,
                Color = layer.Color,
                MapKeys = layer.MapKeys,
                RenderingStrategy = layer.RenderingStrategy,
                CopyToShaderParameters = layer.CopyToShaderParameters,
                Cycle = layer.Cycle,
                Loop = layer.Loop,
            };
        }

        return result ?? layers;
    }

    /// <summary>
    ///     Checks that every state of species specific layers exists, so a suit without that species' sprites falls back to generic ones.
    /// </summary>
    public bool HasLayerStates(EntityUid uid, List<PrototypeLayerData> layers)
    {
        if (!TryComp(uid, out SpriteComponent? sprite))
            return true;

        foreach (var layer in layers)
        {
            if (layer.State == null || layer.TexturePath != null)
                continue;

            var rsi = layer.RsiPath != null
                ? _cache.GetResource<RSIResource>(SpriteSpecifierSerializer.TextureRoot / layer.RsiPath).RSI
                : sprite.BaseRSI;

            if (rsi != null && !rsi.TryGetState(layer.State, out _))
                return false;
        }

        return true;
    }
    // LuaM-end

    /// <summary>
    ///     If no explicit clothing visuals were specified, this attempts to populate with default values.
    /// </summary>
    /// <remarks>
    ///     Useful for lazily adding clothing sprites without modifying yaml. And for backwards compatibility.
    /// </remarks>
    private bool TryGetDefaultVisuals(EntityUid uid, ClothingComponent clothing, string slot, string? speciesId,
        [NotNullWhen(true)] out List<PrototypeLayerData>? layers)
    {
        layers = null;

        RSI? rsi = null;

        if (clothing.RsiPath != null)
            rsi = _cache.GetResource<RSIResource>(SpriteSpecifierSerializer.TextureRoot / clothing.RsiPath).RSI;
        else if (TryComp(uid, out SpriteComponent? sprite))
            rsi = sprite.BaseRSI;

        if (rsi == null)
            return false;

        var correctedSlot = slot;
        TemporarySlotMap.TryGetValue(correctedSlot, out correctedSlot);



        var state = $"equipped-{correctedSlot}";

        if (!string.IsNullOrEmpty(clothing.EquippedPrefix))
            state = $"{clothing.EquippedPrefix}-equipped-{correctedSlot}";

        if (clothing.EquippedState != null)
            state = $"{clothing.EquippedState}";

        // species specific
        if (speciesId != null && rsi.TryGetState($"{state}-{speciesId}", out _))
            state = $"{state}-{speciesId}";
        else if (!rsi.TryGetState(state, out _))
            return false;

        var layer = new PrototypeLayerData();
        layer.RsiPath = rsi.Path.ToString();
        layer.State = state;
        layers = new() { layer };

        return true;
    }

    private void OnVisualsChanged(EntityUid uid, InventoryComponent component, VisualsChangedEvent args)
    {
        var item = GetEntity(args.Item);

        if (!TryComp(item, out ClothingComponent? clothing) || clothing.InSlot == null)
            return;

        RenderEquipment(uid, item, clothing.InSlot, component, null, clothing);
    }

    private void OnDidUnequip(EntityUid uid, SpriteComponent component, DidUnequipEvent args)
    {
        if (!TryComp(uid, out InventorySlotsComponent? inventorySlots))
            return;

        if (!inventorySlots.VisualLayerKeys.TryGetValue(args.Slot, out var revealedLayers))
            return;

        // Remove old layers. We could also just set them to invisible, but as items may add arbitrary layers, this
        // may eventually bloat the player with lots of invisible layers.
        foreach (var layer in revealedLayers)
        {
            _sprite.RemoveLayer((uid, component), layer);
        }
        revealedLayers.Clear();
    }

    public void InitClothing(EntityUid uid, InventoryComponent component)
    {
        if (!TryComp(uid, out SpriteComponent? sprite))
            return;

        var enumerator = _inventorySystem.GetSlotEnumerator((uid, component));
        while (enumerator.NextItem(out var item, out var slot))
        {
            RenderEquipment(uid, item, slot.Name, component, sprite);
        }
    }

    protected override void OnGotEquipped(EntityUid uid, ClothingComponent component, GotEquippedEvent args)
    {
        base.OnGotEquipped(uid, component, args);

        RenderEquipment(args.Equipee, uid, args.Slot, clothingComponent: component);
    }

    private void RenderEquipment(EntityUid equipee, EntityUid equipment, string slot,
        InventoryComponent? inventory = null, SpriteComponent? sprite = null, ClothingComponent? clothingComponent = null,
        InventorySlotsComponent? inventorySlots = null)
    {
        if (!Resolve(equipee, ref inventory, ref sprite, ref inventorySlots) ||
           !Resolve(equipment, ref clothingComponent, false))
        {
            return;
        }

        if (!_inventorySystem.TryGetSlot(equipee, slot, out var slotDef, inventory))
            return;

        // Remove old layers. We could also just set them to invisible, but as items may add arbitrary layers, this
        // may eventually bloat the player with lots of invisible layers.
        if (inventorySlots.VisualLayerKeys.TryGetValue(slot, out var revealedLayers))
        {
            foreach (var key in revealedLayers)
            {
                _sprite.RemoveLayer((equipee, sprite), key);
            }
            revealedLayers.Clear();
        }
        else
        {
            revealedLayers = new();
            inventorySlots.VisualLayerKeys[slot] = revealedLayers;
        }

        var ev = new GetEquipmentVisualsEvent(equipee, slot);
        RaiseLocalEvent(equipment, ev);

        if (ev.Layers.Count == 0)
        {
            RaiseLocalEvent(equipment, new EquipmentVisualsUpdatedEvent(equipee, slot, revealedLayers), true);
            return;
        }

        // temporary, until layer draw depths get added. Basically: a layer with the key "slot" is being used as a
        // bookmark to determine where in the list of layers we should insert the clothing layers.
        bool slotLayerExists = _sprite.LayerMapTryGet((equipee, sprite), slot, out var index, false);
        if (clothingComponent.RenderLayer is { } renderLayer && _sprite.LayerMapTryGet((equipee, sprite), renderLayer, out var renderIndex, false)) // LuaM
        {
            index = renderIndex; // LuaM
            slotLayerExists = true; // LuaM
        }

        // Select displacement maps
        var displacementData = inventory.Displacements.GetValueOrDefault(slot); //Default unsexed map

        var equipeeSex = CompOrNull<HumanoidAppearanceComponent>(equipee)?.Sex;
        if (equipeeSex != null)
        {
            switch (equipeeSex)
            {
                case Sex.Male:
                    if (inventory.MaleDisplacements.Count > 0)
                        displacementData = inventory.MaleDisplacements.GetValueOrDefault(slot);
                    break;
                case Sex.Female:
                    if (inventory.FemaleDisplacements.Count > 0)
                        displacementData = inventory.FemaleDisplacements.GetValueOrDefault(slot);
                    break;
            }
        }

        // add the new layers
        foreach (var (key, layerData) in ev.Layers)
        {
            if (!revealedLayers.Add(key))
            {
                Log.Warning($"Duplicate key for clothing visuals: {key}. Are multiple components attempting to modify the same layer? Equipment: {ToPrettyString(equipment)}");
                continue;
            }

            if (slotLayerExists)
            {
                index++;
                // note that every insertion requires reshuffling & remapping all the existing layers.
                _sprite.AddBlankLayer((equipee, sprite), index);
                _sprite.LayerMapSet((equipee, sprite), key, index);

                if (layerData.Color != null)
                    _sprite.LayerSetColor((equipee, sprite), key, layerData.Color.Value);
                if (layerData.Scale != null)
                    _sprite.LayerSetScale((equipee, sprite), key, layerData.Scale.Value);
            }
            else
                index = _sprite.LayerMapReserve((equipee, sprite), key);

            if (sprite[index] is not Layer layer)
                continue;

            // In case no RSI is given, use the item's base RSI as a default. This cuts down on a lot of unnecessary yaml entries.
            if (layerData.RsiPath == null
                && layerData.TexturePath == null
                && layer.RSI == null
                && TryComp(equipment, out SpriteComponent? clothingSprite))
            {
                _sprite.LayerSetRsi(layer, clothingSprite.BaseRSI);
            }

            _sprite.LayerSetData((equipee, sprite), index, layerData);
            _sprite.LayerSetOffset(layer, layer.Offset + slotDef.Offset);

            if (displacementData is not null)
            {
                //Checking that the state is not tied to the current race. In this case we don't need to use the displacement maps.
                if (layerData.State is not null && inventory.SpeciesId is not null && layerData.State.EndsWith(inventory.SpeciesId))
                    continue;

                if (_displacement.TryAddDisplacement(displacementData, (equipee, sprite), index, key, revealedLayers))
                    index++;
            }
        }

        RaiseLocalEvent(equipment, new EquipmentVisualsUpdatedEvent(equipee, slot, revealedLayers), true);
    }
}
