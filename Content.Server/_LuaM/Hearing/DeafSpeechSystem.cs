using System.Text;
using Content.Shared._LuaM.Hearing;
using Content.Shared.Chat;
using Robust.Shared.Random;

namespace Content.Server._LuaM.Hearing;

/// <summary>
/// What a deaf creature gets of speech and radio. A fully deaf one gets nothing.
/// One that still makes out part of the speech (<see cref="DeafComponent.PartialSpeech"/>, like after a concussion)
/// gets the phrase with part of its words scrambled into gibberish.
/// </summary>
public sealed partial class DeafSpeechSystem : EntitySystem
{
    [Dependency] private IRobustRandom _random = default!;

    private const string Cyrillic = "абвгдежзийклмнопрстуфхцчшщъыьэюя";
    private const string Latin = "abcdefghijklmnopqrstuvwxyz";

    // -- Доля слов, которые доходят целыми при частичной глухоте
    private const float HeardShare = 0.4f;

    /// <summary>
    /// Whether the listener is deaf, and if so, whether they still make out part of the speech.
    /// </summary>
    public bool IsDeaf(EntityUid listener, out bool partialSpeech)
    {
        partialSpeech = false;
        if (!TryComp<DeafComponent>(listener, out var deaf))
            return false;

        partialSpeech = deaf.PartialSpeech;
        return true;
    }

    /// <summary>
    /// Scrambles part of the words of the text. Punctuation, spaces and escaped markup stay as they are.
    /// </summary>
    public string Garble(string text)
    {
        var result = new StringBuilder(text.Length);
        var wordStart = 0;
        for (var i = 0; i <= text.Length; i++)
        {
            if (i < text.Length && !char.IsWhiteSpace(text[i]))
                continue;

            var word = text.AsSpan(wordStart, i - wordStart);
            if (_random.Prob(HeardShare))
                result.Append(word);
            else
                Scramble(word, result);

            if (i < text.Length)
                result.Append(text[i]);

            wordStart = i + 1;
        }

        return result.ToString();
    }

    /// <summary>
    /// A copy of the chat message with part of its words scrambled, or null if the text can't be found in the wrapped message.
    /// </summary>
    public ChatMessage? Garble(ChatMessage message)
    {
        var index = message.WrappedMessage.LastIndexOf(message.Message, StringComparison.Ordinal);
        if (message.Message.Length == 0 || index < 0)
            return null;

        var garbled = Garble(message.Message);
        var wrapped = message.WrappedMessage.Remove(index, message.Message.Length).Insert(index, garbled);

        return new ChatMessage(message.Channel, garbled, wrapped, message.SenderEntity, message.SenderKey,
            message.HideChat, message.MessageColorOverride, message.AudioPath, message.AudioVolume);
    }

    private void Scramble(ReadOnlySpan<char> word, StringBuilder result)
    {
        var escaped = false;
        foreach (var c in word)
        {
            // -- Экранированный символ разметки оставляем как есть, иначе сломается обёртка сообщения
            if (escaped || c == '\\')
            {
                escaped = c == '\\' && !escaped;
                result.Append(c);
                continue;
            }

            result.Append(ScrambleChar(c));
        }
    }

    private char ScrambleChar(char c)
    {
        if (char.IsDigit(c))
            return (char) ('0' + _random.Next(10));

        if (!char.IsLetter(c))
            return c;

        var lower = char.ToLowerInvariant(c);
        var alphabet = lower is >= 'а' and <= 'я' or 'ё' ? Cyrillic : Latin;
        var scrambled = alphabet[_random.Next(alphabet.Length)];
        return char.IsUpper(c) ? char.ToUpperInvariant(scrambled) : scrambled;
    }
}
