using Robust.Shared.GameStates;

namespace Content.Shared._LuaM.Throwing;

// -- Добавляется и сервером, и клиентом, снимается только сервером в конце полёта
[RegisterComponent, NetworkedComponent]
public sealed partial class PredictedThrownItemComponent : Component;
