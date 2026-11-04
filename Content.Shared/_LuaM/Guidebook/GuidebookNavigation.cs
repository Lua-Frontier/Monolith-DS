/*
 * LuaCorp - This file is licensed under AGPLv3
 * Copyright (c) 2026 LuaCorp Contributors
 * See AGPLv3.txt for details.
 */
using System.Linq;
using Content.Shared.Guidebook;
using Robust.Shared.Prototypes;

namespace Content.Shared._LuaM.Guidebook;

public static class GuidebookNavigation
{
    public static List<ProtoId<GuideEntryPrototype>>? GetBookRoots(
        IReadOnlyDictionary<ProtoId<GuideEntryPrototype>, GuideEntry> guides)
    {
        var roots = guides.Where(p => p.Value.BookRoot).Select(p => p.Key).ToList();
        return roots.Count == 0 ? null : roots;
    }
}
