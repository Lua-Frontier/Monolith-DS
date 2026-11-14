/* LuaCorp - AGPLv3. See AGPLv3.txt. */
using Robust.Shared.Utility;

namespace Content.Client._LuaM.Guidebook;

/// <summary>Readable parameter/value styling, without interpreting prototype text as markup.</summary>
public static class GuideStatText
{
    public static FormattedMessage Format(string name, string value, string color = "lightblue")
        => FormattedMessage.FromMarkupOrThrow("[color=yellow][bold]" + FormattedMessage.EscapeText(name)
            + "[/bold][/color]: [color=" + color + "]" + FormattedMessage.EscapeText(value) + "[/color]");

    public static FormattedMessage FormatLine(string line)
    {
        var split = line.IndexOf(": ", StringComparison.Ordinal);
        if (split < 0) return FormattedMessage.FromUnformatted(line);
        var color = "lightblue";
        var value = line[(split + 2)..];
        if (line.StartsWith("Получаемый урон,", StringComparison.Ordinal))
            color = value.StartsWith('+') ? "orange" : "lightgreen";
        else if (line.StartsWith("Ходьба:", StringComparison.Ordinal) || line.StartsWith("Бег:", StringComparison.Ordinal))
            color = value.Contains("(-", StringComparison.Ordinal) ? "orange" : value.Contains("(+", StringComparison.Ordinal) ? "lightgreen" : "lightblue";
        return Format(line[..split], value, color);
    }
}
