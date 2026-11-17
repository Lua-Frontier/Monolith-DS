using System;
using System.Reflection;
using Content.Client.Administration.UI;
using Content.Shared.Administration;
using Content.Shared._LuaM.Announcements;
using Robust.Client.UserInterface.Controls;

namespace Content.IntegrationTests.Tests._LuaM;

[TestFixture]
public sealed class AnnouncementAdminUiTests
{
    [Test]
    public async Task AdminPanelPreservesExplicitStyleAndSeparatesPortraitSourceFromCaption()
    {
        await using var pair = await PoolManager.GetServerClient(new PoolSettings());
        await pair.Client.WaitAssertion(() =>
        {
            using var window = new AdminAnnounceWindow();
            Assert.That(window.ScreenStyle.ItemCount, Is.EqualTo(Enum.GetValues<ScreenAnnouncementStyle>().Length));
            foreach (var style in Enum.GetValues<ScreenAnnouncementStyle>())
            {
                window.ScreenStyle.SelectId((int) style);
                Assert.That(window.ScreenStyle.SelectedMetadata, Is.EqualTo(style));
            }
            Assert.That(window.AnnounceMethod.ItemCount, Is.EqualTo(Enum.GetValues<AdminAnnounceType>().Length));
            Assert.That(window.Announcer.Text, Is.Empty, "A blank heading uses the selected faction default.");
            Select(window.AnnounceMethod, (int) AdminAnnounceType.Overwatch);
            Assert.That(window.AnnounceMethod.SelectedMetadata, Is.EqualTo(AdminAnnounceType.Overwatch));
            Assert.That(window.ScreenStyle.Disabled, Is.False);
            Assert.That(window.ScreenStyle.SelectedMetadata, Is.EqualTo(ScreenAnnouncementStyle.Tsf), "Invalid previous designs fall back to a real company.");
            foreach (var style in Enum.GetValues<ScreenAnnouncementStyle>())
                Assert.That(window.ScreenStyle.IsItemDisabled(window.ScreenStyle.GetIdx((int) style)),
                    Is.EqualTo(!AdminAnnounceStyles.IsFaction(style)), style.ToString());
            Select(window.AnnounceMethod, (int) AdminAnnounceType.Server);
            Assert.That(window.ScreenStyle.Disabled, Is.True, "Server channel stays vanilla chat-only.");
            Assert.That(window.PlaySound.Disabled, Is.True);
            Select(window.AnnounceMethod, (int) AdminAnnounceType.Overwatch);
            Assert.That(window.PlaySound.Pressed, Is.True);
            Assert.That(window.PortraitEntity.Editable, Is.False);
            Assert.That(window.PortraitName.Editable, Is.False);
            Select(window.PortraitMode, (int) AdminAnnouncePortraitMode.Entity);
            Assert.That(window.PortraitEntity.Editable, Is.True);
            Assert.That(window.PortraitName.Editable, Is.True);
            window.PortraitEntity.Text = "123";
            window.PortraitName.Text = "Имя-Из-Нескольких-Слов";
            Select(window.PortraitMode, (int) AdminAnnouncePortraitMode.Self);
            Assert.That(window.PortraitEntity.Editable, Is.False);
            Assert.That(window.PortraitName.Editable, Is.True);
            Assert.That(window.PortraitName.Text, Is.EqualTo("Имя-Из-Нескольких-Слов"));
            Select(window.PortraitMode, (int) AdminAnnouncePortraitMode.None);
            Assert.That(window.PortraitName.Editable, Is.False);
            Assert.That(window.Announcer.Editable, Is.True, "A custom heading is available on every channel.");
        });
        await pair.CleanReturnAsync();
    }

    private static void Select(OptionButton options, int id)
    {
        var handler = (Action<OptionButton.ItemSelectedEventArgs>) typeof(OptionButton)
            .GetField("OnItemSelected", BindingFlags.Instance | BindingFlags.NonPublic)!.GetValue(options)!;
        handler(new OptionButton.ItemSelectedEventArgs(id, options));
    }
}
