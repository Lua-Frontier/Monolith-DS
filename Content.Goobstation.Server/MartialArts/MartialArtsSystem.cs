// SPDX-License-Identifier: AGPL-3.0-or-later

using Content.Goobstation.Shared.MartialArts;
using Content.Server.Chat.Systems;
using Content.Shared.Chat;

namespace Content.Goobstation.Server.MartialArts;

public sealed class MartialArtsSystem : SharedMartialArtsSystem
{
    [Dependency] private readonly ChatSystem _chat = default!;

    protected override void Say(EntityUid uid, LocId saying)
    {
        _chat.TrySendInGameICMessage(uid, Loc.GetString(saying), InGameICChatType.Speak, false);
    }
}
