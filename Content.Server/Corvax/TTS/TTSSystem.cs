using System.Threading.Tasks;
using Content.Server._EinsteinEngines.Language;
using Content.Server.Chat.Systems;
using Content.Shared._EinsteinEngines.Language;
using Content.Shared.CCVar;
using Content.Shared.Chat;
using Content.Shared.Corvax.TTS;
using Content.Shared.GameTicking;
using Content.Shared.Players.RateLimiting;
using Robust.Shared.Configuration;
using Robust.Shared.Player;
using Robust.Shared.Prototypes;
using Robust.Shared.Random;
using Robust.Shared.Timing;

namespace Content.Server.Corvax.TTS;

// ReSharper disable once InconsistentNaming
public sealed partial class TTSSystem : EntitySystem
{
    [Dependency] private readonly IConfigurationManager _cfg = default!;
    [Dependency] private readonly IPrototypeManager _prototypeManager = default!;
    [Dependency] private readonly TTSManager _ttsManager = default!;
    [Dependency] private readonly IRobustRandom _rng = default!;
    [Dependency] private readonly LanguageSystem _language = default!;

    private readonly List<string> _sampleText =
    [
        "Съешь же ещё этих мягких французских булок, да выпей чаю.",
        "Клоун, прекрати разбрасывать банановые кожурки офицерам под ноги!",
        "Капитан, вы уверены что хотите назначить клоуна на должность главы персонала?",
        "СБ! Тут человек в сером костюме, с тулбоксом и в маске! Помогите!",
        "Я надеюсь что инженеры внимательно следят за сингулярностью...",
        "Вы слышали эти странные крики в техах? Мне кажется туда ходить небезопасно.",
        "Вы не видели Гамлета? Мне кажется он забегал к вам на кухню.",
        "Здесь есть доктор? Человек умирает от отравленного пончика! Нужна помощь!",
        "Возле эвакуационного шаттла разгерметизация! Инженеры, нам срочно нужна ваша помощь!",
        "Бармен, налей мне самого крепкого вина, которое есть в твоих запасах!"
    ];

    private const int MaxMessageChars = 100 * 3; // same as SingleBubbleCharLimit * 3
    private bool _isEnabled;

    public override void Initialize()
    {
        _cfg.OnValueChanged(CCVars.TTSEnabled, v => _isEnabled = v, true);

        SubscribeLocalEvent<TransformSpeechEvent>(OnTransformSpeech);
        SubscribeLocalEvent<TTSComponent, EntitySpokeEvent>(OnEntitySpoke);
        SubscribeLocalEvent<TTSComponent, EntitySpokeToEntityEvent>(OnEntitySpokeToEntity);
        SubscribeLocalEvent<RadioSpokeEvent>(OnRadioSpokeEvent);
        SubscribeLocalEvent<AnnounceSpokeEvent>(OnAnnounceSpokeEvent);
        SubscribeLocalEvent<RoundRestartCleanupEvent>(OnRoundRestartCleanup);
        SubscribeNetworkEvent<RequestPreviewTTSEvent>(OnRequestPreviewTTS);

        RegisterRateLimits();
    }

    private void OnRoundRestartCleanup(RoundRestartCleanupEvent ev)
    {
        _ttsManager.ResetCache();
    }

    private async void OnRequestPreviewTTS(RequestPreviewTTSEvent ev, EntitySessionEventArgs args)
    {
        if (!_isEnabled ||
            !_prototypeManager.TryIndex<TTSVoicePrototype>(ev.VoiceId, out var protoVoice))
            return;

        if (HandleRateLimit(args.SenderSession) != RateLimitStatus.Allowed)
            return;

        var previewText = _rng.Pick(_sampleText);
        var soundData = await GenerateTTS(previewText, protoVoice.Speaker);
        if (soundData is null)
            return;

        RaiseNetworkEvent(new PlayTTSEvent(soundData), Filter.SinglePlayer(args.SenderSession));
    }

    private void OnEntitySpoke(EntityUid uid, TTSComponent component, EntitySpokeEvent args)
    {
        var voiceId = component.VoicePrototypeId;
        if (!_isEnabled ||
            args.Message.Length > MaxMessageChars ||
            voiceId == null)
            return;

        var voiceEv = new TransformSpeakerVoiceEvent(uid, voiceId);
        RaiseLocalEvent(uid, voiceEv);
        voiceId = voiceEv.VoiceId;

        if (!_prototypeManager.TryIndex<TTSVoicePrototype>(voiceId, out var protoVoice))
            return;

        if (args.IsWhisper)
        {
            HandleSay(uid, args.Message, args.Language, protoVoice.Speaker, true);
            return;
        }

        HandleSay(uid, args.Message, args.Language, protoVoice.Speaker, false);
    }

    private void OnEntitySpokeToEntity(EntityUid uid, TTSComponent component, EntitySpokeToEntityEvent args)
    {
        var voiceId = component.VoicePrototypeId;
        if (!_isEnabled ||
            args.Message.Length > MaxMessageChars ||
            voiceId == null)
            return;

        var voiceEv = new TransformSpeakerVoiceEvent(uid, voiceId);
        RaiseLocalEvent(uid, voiceEv);
        voiceId = voiceEv.VoiceId;

        if (!_prototypeManager.TryIndex<TTSVoicePrototype>(voiceId, out var protoVoice))
            return;

        HandleDirectSay(args.Targets, args.Message, args.ObfuscatedMessage, args.Language, protoVoice.Speaker);
    }

    private void OnRadioSpokeEvent(RadioSpokeEvent args)
    {
        if (!_isEnabled ||
            args.Message.Length > MaxMessageChars ||
            !TryComp(args.Source, out TTSComponent? component))
            return;

        var voiceId = component.VoicePrototypeId;
        if (voiceId == null)
            return;

        var voiceEv = new TransformSpeakerVoiceEvent(args.Source, voiceId);
        RaiseLocalEvent(args.Source, voiceEv);
        voiceId = voiceEv.VoiceId;

        if (!_prototypeManager.TryIndex<TTSVoicePrototype>(voiceId, out var protoVoice))
            return;

        HandleRadio(args.Receivers, args.Message, args.ObfuscatedMessage, args.Language, protoVoice.Speaker);
    }

    private void OnAnnounceSpokeEvent(AnnounceSpokeEvent args)
    {
        var voiceId = args.Voice;
        if (!_isEnabled ||
            args.Message.Length > _cfg.GetCVar(CCVars.ChatMaxAnnouncementLength) ||
            string.IsNullOrEmpty(voiceId))
            return;

        if (args.Source is { Valid: true } source)
        {
            var voiceEv = new TransformSpeakerVoiceEvent(source, voiceId);
            RaiseLocalEvent(source, voiceEv);
            voiceId = voiceEv.VoiceId;
        }

        if (!_prototypeManager.TryIndex<TTSVoicePrototype>(voiceId, out var protoVoice))
            return;

        Timer.Spawn(6000, () => HandleAnnounce(args.Message, args.Language, protoVoice.Speaker, args.Filter));
    }

    private async void HandleSay(EntityUid uid, string message, LanguagePrototype language, string speaker, bool isWhisper)
    {
        var voiceRange = isWhisper ? ChatSystem.WhisperMuffledRange : ChatSystem.VoiceRange;
        var recipients = GetExpandedVoiceRecipients(uid, voiceRange);
        var soundData = await GenerateTTS(message, speaker, isWhisper);

        if (soundData is null)
            return;

        byte[]? obfuscatedSoundData = null;
        var obfuscatedMessage = string.Empty;

        foreach (var session in recipients)
        {
            if (session.AttachedEntity is not { Valid: true } listener)
                continue;

            if (_language.CanUnderstand(listener, language.ID))
            {
                RaiseNetworkEvent(new PlayTTSEvent(soundData, GetNetEntity(uid), isWhisper), session);
                continue;
            }

            if (obfuscatedSoundData is null)
            {
                obfuscatedMessage = _language.ObfuscateSpeech(message, language);
                obfuscatedSoundData = await GenerateTTS(obfuscatedMessage, speaker, isWhisper);
            }

            if (obfuscatedSoundData is not null)
                RaiseNetworkEvent(new PlayTTSEvent(obfuscatedSoundData, GetNetEntity(uid), isWhisper), session);
        }
    }

    private async void HandleDirectSay(
        IReadOnlyList<EntityUid> targets,
        string message,
        string obfuscatedMessage,
        LanguagePrototype language,
        string speaker)
    {
        var soundData = await GenerateTTS(message, speaker);
        if (soundData is null)
            return;

        byte[]? obfuscatedSoundData = null;
        var sent = new HashSet<EntityUid>();

        foreach (var target in targets)
        {
            if (!sent.Add(target))
                continue;

            if (_language.CanUnderstand(target, language.ID))
            {
                RaiseNetworkEvent(new PlayTTSEvent(soundData, GetNetEntity(target)), Filter.Entities(target));
                continue;
            }

            obfuscatedSoundData ??= await GenerateTTS(obfuscatedMessage, speaker);
            if (obfuscatedSoundData is not null)
                RaiseNetworkEvent(new PlayTTSEvent(obfuscatedSoundData, GetNetEntity(target)), Filter.Entities(target));
        }
    }

    private async void HandleRadio(
        EntityUid[] receivers,
        string message,
        string obfuscatedMessage,
        LanguagePrototype language,
        string speaker)
    {
        var soundData = await GenerateTTS(message, speaker);
        if (soundData is null)
            return;

        byte[]? obfuscatedSoundData = null;

        foreach (var receiver in receivers)
        {
            if (_language.CanUnderstand(receiver, language.ID))
            {
                RaiseNetworkEvent(new PlayTTSEvent(soundData, GetNetEntity(receiver), isRadio: true), Filter.Entities(receiver));
                continue;
            }

            obfuscatedSoundData ??= await GenerateTTS(obfuscatedMessage, speaker);
            if (obfuscatedSoundData is not null)
                RaiseNetworkEvent(new PlayTTSEvent(obfuscatedSoundData, GetNetEntity(receiver), isRadio: true), Filter.Entities(receiver));
        }
    }

    private async void HandleAnnounce(string message, LanguagePrototype? language, string speaker, Filter filter)
    {
        var soundData = await GenerateTTS(message, speaker);
        if (soundData is null)
            return;

        if (language == null)
        {
            RaiseNetworkEvent(new PlayTTSEvent(soundData), filter);
            return;
        }

        byte[]? obfuscatedSoundData = null;
        var obfuscatedMessage = string.Empty;

        foreach (var session in filter.Recipients)
        {
            if (session.AttachedEntity is not { Valid: true } listener ||
                _language.CanUnderstand(listener, language.ID))
            {
                RaiseNetworkEvent(new PlayTTSEvent(soundData), session);
                continue;
            }

            if (obfuscatedSoundData is null)
            {
                obfuscatedMessage = _language.ObfuscateSpeech(message, language);
                obfuscatedSoundData = await GenerateTTS(obfuscatedMessage, speaker);
            }

            if (obfuscatedSoundData is not null)
                RaiseNetworkEvent(new PlayTTSEvent(obfuscatedSoundData), session);
        }
    }

    private IReadOnlyCollection<ICommonSession> GetExpandedVoiceRecipients(EntityUid source, float voiceRange)
    {
        var recipients = new Dictionary<ICommonSession, ChatSystem.ICChatRecipientData>();

        foreach (var session in Filter.Pvs(source).Recipients)
        {
            recipients.TryAdd(session, new ChatSystem.ICChatRecipientData(0f, false));
        }

        RaiseLocalEvent(new ExpandICChatRecipientsEvent(source, voiceRange, recipients));

        return recipients.Keys;
    }

    // ReSharper disable once InconsistentNaming
    private async Task<byte[]?> GenerateTTS(string text, string speaker, bool isWhisper = false)
    {
        var textSanitized = Sanitize(text);
        if (textSanitized == "")
            return null;

        if (char.IsLetter(textSanitized[^1]))
            textSanitized += ".";

        var ssmlTraits = isWhisper ? SoundTraits.PitchVerylow : SoundTraits.RateFast;
        var textSsml = ToSsmlText(textSanitized, ssmlTraits);

        return await _ttsManager.ConvertTextToSpeech(speaker, textSsml);
    }
}
