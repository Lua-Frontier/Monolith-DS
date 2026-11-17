namespace Content.Shared._LuaM.Performance;

public static class CollectionExtensions
{
    public static void ClearAndTrim<T>(this HashSet<T> set, int trimAbove = 256)
    {
        var small = set.Count <= trimAbove;
        set.Clear();
        if (small && set.Capacity > trimAbove * 4)
            set.TrimExcess();
    }
}
