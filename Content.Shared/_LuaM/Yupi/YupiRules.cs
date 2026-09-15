using Content.Shared._NF.Bank;

namespace Content.Shared._LuaM.Yupi;

public static class YupiRules
{
    public const int LowRateLimit = 250_000;

    public const int LowRatePercent = 3;

    public const int HighRatePercent = 10;

    public static readonly TimeSpan LowRateWindow = TimeSpan.FromMinutes(30);

    public static readonly TimeSpan Cooldown = TimeSpan.FromMinutes(1);

    public const int CodeLength = 6;

    public const string CodeAlphabet = "ABCDEFGHJKLMNPQRSTUVWXYZ123456789";

    public static long GetCommission(int amount, int lowRateRemaining)
    {
        if (amount <= 0)
            return 0;

        long lowPart = Math.Clamp(lowRateRemaining, 0, amount);
        long highPart = amount - lowPart;
        return CeilPercent(lowPart, LowRatePercent) + CeilPercent(highPart, HighRatePercent);
    }

    public static string FormatCredits(long amount)
    {
        return BankSystemExtensions.ToCurrencyString(amount,
            symbolOverride: string.Empty,
            separatorOverride: ".",
            symbolLocation: BankSystemExtensions.CurrencySymbolLocation.Prefix);
    }

    public static string NormalizeCode(string code)
    {
        return code.Trim().ToUpperInvariant();
    }

    public static bool IsValidCode(string code)
    {
        if (code.Length != CodeLength)
            return false;

        foreach (var ch in code)
        {
            if (!CodeAlphabet.Contains(ch))
                return false;
        }

        return true;
    }

    private static long CeilPercent(long value, int percent)
    {
        return (value * percent + 99) / 100;
    }
}
