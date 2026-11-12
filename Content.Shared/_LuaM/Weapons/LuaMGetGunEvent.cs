/*
 * LuaCorp - This file is licensed under AGPLv3
 * Copyright (c) 2026 LuaCorp Contributors
 * See AGPLv3.txt for details.
 */

namespace Content.Shared._LuaM.Weapons;

// -- Позволяет подменить оружие стрелка (например, турель машины) до проверки рук
[ByRefEvent]
public record struct LuaMGetGunEvent(EntityUid? Gun = null);
