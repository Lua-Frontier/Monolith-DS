namespace Content.Shared._LuaM.Cargo;

/// <summary>
/// Gives a cargo bounty console its own texts: every bounty console string is first looked up
/// with this prefix (kitchen- for kitchen-bounty-console-menu-title), the common one is the fallback.
/// Used for the window and for the labels the console prints.
/// </summary>
[RegisterComponent]
public sealed partial class BountyConsoleLocaleComponent : Component
{
    [DataField(required: true)]
    public string Prefix = string.Empty;
}

public static class BountyConsoleLoc
{
    public static string Get(string? prefix, string key, params (string, object)[] args)
    {
        var loc = IoCManager.Resolve<ILocalizationManager>();
        if (!string.IsNullOrEmpty(prefix) && loc.TryGetString(prefix + key, out var text, args))
            return text;

        return loc.GetString(key, args);
    }
}
