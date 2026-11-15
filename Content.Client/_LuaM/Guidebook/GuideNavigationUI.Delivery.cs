/* LuaCorp - AGPLv3. See AGPLv3.txt for details. */
using System.Linq;
using System.Numerics;
using Content.Client._LuaM.Delivery;
using Content.Client.UserInterface.Controls;
using Content.Shared._LuaM.Delivery;
using Robust.Client.UserInterface.Controls;

namespace Content.Client._LuaM.Guidebook;

public sealed partial class GuideNavigationUI
{
    private void CreateDelivery()
    {
        var window = new FancyWindow
        {
            Title = "ColossDelivery — учебное приложение курьера",
            SetSize = new Vector2(700, 700),
            MinSize = new Vector2(480, 400),
        };
        _window = window;
        var body = new BoxContainer
        {
            Orientation = LayoutOrientation.Vertical,
            HorizontalExpand = true,
            VerticalExpand = true,
            SeparationOverride = 6,
        };
        window.ContentsContainer.AddChild(body);
        var hint = new RichTextLabel { HorizontalExpand = true };
        hint.SetMessage("Пример приложения из КПК. Нажмите «Взять заказ» и сравните статусы. " +
                        "Стоимость — цена для покупателя, «Вам» — выплата курьеру, координаты — положение посылки.");
        body.AddChild(hint);
        var result = new Label { Text = "У вас один заказ. Можно принять ещё один." };
        body.AddChild(result);
        var app = new DeliveryUiFragment();
        body.AddChild(app);
        const string courier = "Курьер (пример)";
        var now = _timing.CurTime;
        var orders = new List<DeliveryOrderInfo>
        {
            new()
            {
                Id = 101, Customer = "Заказчик №1", Courier = courier, TakenByViewer = true,
                Total = 100_000, Payout = DeliveryRules.GetCourierPayout(100_000),
                ExpiresAt = now + TimeSpan.FromMinutes(12), InTransit = true, Position = new Vector2(125, -40),
                Items = new() { new("MedkitFilled", 1), new("DrinkWaterBottleFull", 2) },
            },
            new()
            {
                Id = 102, Customer = "Заказчик №2", Total = 50_000, Payout = DeliveryRules.GetCourierPayout(50_000),
                ExpiresAt = now + DeliveryRules.DeliveryTime,
                Items = new() { new("MedkitFilled", 1) },
            },
            new()
            {
                Id = 103, Customer = "Заказчик №3", Total = 5_000, Payout = DeliveryRules.GetCourierPayout(5_000),
                ExpiresAt = now + DeliveryRules.DeliveryTime,
                Items = new() { new("DrinkWaterBottleFull", 1) },
            },
        };
        void Refresh() => app.UpdateState(new DeliveryUiState(DeliveryUiMode.Courier, 20_000, orders,
            courierName: courier, delivered: 1, earned: 15_000));
        // Subscribe only to the local example. No DeliveryUi/BUI or cartridge messages are created.
        app.OnTake += id =>
        {
            if (orders.Count(o => o.TakenByViewer) >= DeliveryRules.MaxCourierOrders)
            {
                result.Text = "Одновременно можно вести только два заказа.";
                return;
            }
            var order = orders.FirstOrDefault(o => o.Id == id);
            if (order == null || order.Courier != null) return;
            order.Courier = courier;
            order.TakenByViewer = true;
            order.InTransit = true;
            order.Position = new Vector2(10, 20);
            result.Text = $"Взят учебный заказ №{id}. Таймер идёт с оформления: принятие его не обновляет.";
            Refresh();
        };
        Refresh();
    }
}
