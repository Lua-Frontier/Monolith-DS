using Content.Client._Lua.VendingMachines; // LuaM
using Content.Shared.VendingMachines;
using Robust.Client.UserInterface;
using Robust.Client.GameObjects;
using Content.Shared._NF.Bank.Components; // Frontier
using Content.Shared.Containers.ItemSlots; // Frontier
using Content.Shared.Stacks; // Frontier

namespace Content.Client.VendingMachines
{
    public sealed class VendingMachineBoundUserInterface : BoundUserInterface
    {
        [ViewVariables]
        private LuaVendingMachineWindow? _menu; // LuaM: VendingMachineMenu > LuaVendingMachineWindow

        [ViewVariables]
        private List<VendingMachineInventoryEntry> _cachedInventory = new();

        // Frontier: market price modifier & balance
        private UserInterfaceSystem _uiSystem = default!;
        private ItemSlotsSystem _itemSlots = default!;

        [ViewVariables]
        private float _mod = 1f;
        [ViewVariables]
        private int _balance = 0;
        [ViewVariables]
        private int _cashSlotBalance = 0;
        // End Frontier
        [ViewVariables]
        private bool _requiresCash; // mono

        public VendingMachineBoundUserInterface(EntityUid owner, Enum uiKey) : base(owner, uiKey)
        {
        }

        protected override void Open()
        {
            base.Open();

            // Frontier: state, market modifier, balance status
            _uiSystem = EntMan.System<UserInterfaceSystem>();
            _itemSlots = EntMan.System<ItemSlotsSystem>();

            if (EntMan.TryGetComponent<MarketModifierComponent>(Owner, out var market))
                _mod = market.Mod;
            // End Frontier

            _menu = this.CreateWindowCenteredLeft<LuaVendingMachineWindow>(); // LuaM: VendingMachineMenu > LuaVendingMachineWindow
            // Frontier: no exceptions
            if (EntMan.TryGetComponent(Owner, out MetaDataComponent? meta))
                _menu.Title = meta.EntityName;
            else
                _menu.Title = Loc.GetString("vending-machine-nf-fallback-title");
            // End Frontier: no exceptions
            _menu.OnItemSelected += OnItemSelected;
            Refresh();
        }

        public void Refresh()
        {
            if (_menu == null || !EntMan.HasComponent<VendingMachineComponent>(Owner)) // LuaM
                return; // LuaM

            var system = EntMan.System<VendingMachineSystem>();
            _cachedInventory = system.GetAllInventory(Owner);

            // Frontier: state, market modifier, balance status
            var uiUsers = _uiSystem.GetActors(Owner, UiKey);
            foreach (var uiUser in uiUsers)
            {
                if (EntMan.TryGetComponent<BankAccountComponent>(uiUser, out var bank))
                    _balance = bank.Balance;
            }
            int? cashSlotValue = null;
            if (EntMan.TryGetComponent<VendingMachineComponent>(Owner, out var vendingMachine))
            {
                _cashSlotBalance = vendingMachine.CashSlotBalance;
                _requiresCash = vendingMachine.RequiresCash; // mono
                if (vendingMachine.CashSlotName != null)
                    cashSlotValue = _cashSlotBalance;
            }
            else
            {
                _cashSlotBalance = 0;
            }
            // End Frontier

            var enabled = vendingMachine is { Ejecting: false }; // LuaM
            _menu?.Populate(_cachedInventory, enabled, _mod, _balance, cashSlotValue, _requiresCash); // Frontier: add _balance, mono: add _requiresCash // LuaM: add enabled
        }

        private void OnItemSelected(InventoryType type, string id) // LuaM: GUIBoundKeyEventArgs, ListData > InventoryType, string
        {
            SendMessage(new VendingMachineEjectMessage(type, id));
        }

        protected override void Dispose(bool disposing)
        {
            base.Dispose(disposing);
            if (!disposing)
                return;

            if (_menu == null)
                return;

            _menu.OnItemSelected -= OnItemSelected;
            _menu.OnClose -= Close;
            _menu.Dispose();
        }
    }
}
