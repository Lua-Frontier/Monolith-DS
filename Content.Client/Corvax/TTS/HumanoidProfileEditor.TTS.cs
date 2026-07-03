using System.Linq;
using Content.Client.Corvax.TTS;
using Content.Shared.Corvax.TTS;
using Content.Shared.Preferences;
using Robust.Shared.Timing;

namespace Content.Client.Lobby.UI;

public sealed partial class HumanoidProfileEditor
{
    private List<TTSVoicePrototype> _voiceList = new();

    private void InitializeVoice()
    {
        _voiceList = _prototypeManager
            .EnumeratePrototypes<TTSVoicePrototype>()
            .Where(o => o.RoundStart)
            .OrderBy(o => Loc.GetString(o.Name))
            .ToList();

        VoiceButton.OnItemSelected += args =>
        {
            VoiceButton.SelectId(args.Id);
            SetVoice(_voiceList[args.Id].ID);
        };

        VoicePlayButton.OnPressed += _ => PlayPreviewTTS();
    }

    private void UpdateTTSVoicesControls()
    {
        if (Profile is null)
            return;

        VoiceButton.Clear();

        int? firstVoiceChoiceId = null;
        for (var i = 0; i < _voiceList.Count; i++)
        {
            var voice = _voiceList[i];
            if (!HumanoidCharacterProfile.CanHaveVoice(voice, Profile.Sex))
                continue;

            var name = Loc.GetString(voice.Name);
            VoiceButton.AddItem(name, i);

            if (firstVoiceChoiceId == null)
                firstVoiceChoiceId = i;

            // Не спонсоры могут прослушивать голоса в лобби
            // if (voice.SponsorOnly)
            // {
            //     if (!IoCManager.Resolve<SponsorsManager>().TryGetInfo(out var sponsor))
            //     {
            //         VoiceButton.SetItemDisabled(VoiceButton.GetIdx(i), true);
            //     }
            //     else if (!sponsor.AllowedMarkings.Contains(voice.ID))
            //     {
            //         VoiceButton.SetItemDisabled(VoiceButton.GetIdx(i), true);
            //     }
            // }
        }

        var voiceChoiceId = _voiceList.FindIndex(x => x.ID == Profile.Voice);
        if (!VoiceButton.TrySelectId(voiceChoiceId) &&
            firstVoiceChoiceId is { } firstId &&
            VoiceButton.TrySelectId(firstId))
        {
            SetVoice(_voiceList[firstId].ID);
        }
    }

    private void SetVoice(string newVoice)
    {
        Profile = Profile?.WithVoice(newVoice);
        SetDirty();
        ReloadPreview();
    }

    private void PlayPreviewTTS()
    {
        if (Profile is null)
            return;

        _entManager.System<TTSSystem>().RequestPreviewTTS(Profile.Voice);
    }
}
