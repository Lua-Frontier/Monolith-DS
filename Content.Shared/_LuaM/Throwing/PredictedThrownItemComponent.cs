using Robust.Shared.GameStates;

namespace Content.Shared._LuaM.Throwing;

// -- Добавляется и сервером, и клиентом, снимается только сервером в конце полёта
[RegisterComponent, NetworkedComponent, AutoGenerateComponentState(true)]
public sealed partial class PredictedThrownItemComponent : Component
{
    // -- Физику полёта предсказывает только клиент бросившего: остальные получают и полёт, и звуки попадания от сервера
    [DataField, AutoNetworkedField]
    public EntityUid? Thrower;
}
