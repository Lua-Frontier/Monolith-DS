using Content.Shared._EinsteinEngines.Language;
using Content.Shared.Speech;
using Robust.Shared.Prototypes;
using Content.Shared.Inventory;
using Robust.Shared.Player;

namespace Content.Shared.Chat;

/// <summary>
///     This event should be sent everytime an entity talks (Radio, local chat, etc...).
///     The event is sent to both the entity itself, and all clothing (For stuff like voice masks).
/// </summary>
public sealed class TransformSpeakerNameEvent : EntityEventArgs, IInventoryRelayEvent
{
    public SlotFlags TargetSlots { get; } = SlotFlags.WITHOUT_POCKET;
    public EntityUid Sender;
    public string VoiceName;
    public ProtoId<SpeechVerbPrototype>? SpeechVerb;

    public TransformSpeakerNameEvent(EntityUid sender, string name)
    {
        Sender = sender;
        VoiceName = name;
        SpeechVerb = null;
    }
}

/// <summary>
/// Raised after a radio message is sent, with the entities that actually received it.
/// </summary>
public sealed class RadioSpokeEvent : EntityEventArgs
{
    public readonly EntityUid Source;
    public readonly string Message;
    public readonly string ObfuscatedMessage;
    public readonly LanguagePrototype Language;
    public readonly EntityUid[] Receivers;

    public RadioSpokeEvent(
        EntityUid source,
        string message,
        string obfuscatedMessage,
        LanguagePrototype language,
        EntityUid[] receivers)
    {
        Source = source;
        Message = message;
        ObfuscatedMessage = obfuscatedMessage;
        Language = language;
        Receivers = receivers;
    }
}

/// <summary>
/// Raised when an announcement should also be voiced by TTS.
/// </summary>
public sealed class AnnounceSpokeEvent : EntityEventArgs
{
    public readonly string Voice;
    public readonly string Message;
    public readonly EntityUid? Source;
    public readonly Filter Filter;
    public readonly LanguagePrototype? Language;

    public AnnounceSpokeEvent(string voice, string message, Filter filter, EntityUid? source, LanguagePrototype? language = null)
    {
        Voice = voice;
        Message = message;
        Filter = filter;
        Source = source;
        Language = language;
    }
}
