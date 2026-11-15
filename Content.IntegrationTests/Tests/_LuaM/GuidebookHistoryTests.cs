/* LuaM - AGPLv3. See AGPLv3.txt. */
using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using Content.Client.Guidebook.Controls;
using Content.Client.Stylesheets;
using Content.Shared._LuaM.Guidebook;
using Content.Shared.Guidebook;
using Robust.Client.UserInterface.Controls;
using Robust.Shared.Prototypes;

namespace Content.IntegrationTests.Tests._LuaM;

[TestFixture]
public sealed class GuidebookHistoryTests
{
    private static void Click(Button button)
    {
        Assert.That(button.Disabled, Is.False);
        var handlers = (Delegate) typeof(BaseButton).GetField("OnPressed", BindingFlags.Instance | BindingFlags.NonPublic)!.GetValue(button)!;
        handlers.DynamicInvoke(new BaseButton.ButtonEventArgs(button, null!));
    }

    [Test]
    public async Task JoinedNavigationButtonsReturnThroughTreeLinksHomeAndContextualHelp()
    {
        await using var pair = await PoolManager.GetServerClient(new PoolSettings { Connected = true });
        await pair.Client.WaitAssertion(() =>
        {
            var prototypes = pair.Client.ResolveDependency<IPrototypeManager>();
            var guides = prototypes.EnumeratePrototypes<GuideEntryPrototype>()
                .ToDictionary(p => new ProtoId<GuideEntryPrototype>(p.ID), p => (GuideEntry) p);
            using var window = new GuidebookWindow();
            window.UpdateGuides(guides, GuidebookNavigation.GetBookRoots(guides));
            var back = window.FindControl<Button>("BackButton");
            Assert.That(back, Is.Not.Null, "The title bar needs a Back button beside Home.");
            var home = window.FindControl<Button>("HomeButton");
            Assert.That(back!.Disabled, Is.True);
            Assert.That(home.Disabled, Is.True);
            Assert.That(back.StyleClasses, Does.Contain(StyleBase.ButtonOpenRight));
            Assert.That(home.StyleClasses, Does.Contain(StyleBase.ButtonOpenLeft));
            Assert.That(back.Parent, Is.SameAs(home.Parent));
            Assert.That(((BoxContainer) back.Parent!).SeparationOverride, Is.EqualTo(2));
            Assert.That(back.Margin.Right + home.Margin.Left, Is.Zero);

            window.HandleClick("ColossusCatalogue");
            var species = window.Tree.Items.Single(i => i.Metadata is GuideEntry e && e.Id == "Species");
            window.Tree.SetSelectedIndex(species.Index);
            Click(back);
            Assert.That(window.LastEntry.ToString(), Is.EqualTo("ColossusCatalogue"));
            Click(home);
            Assert.That(window.LastEntry.ToString(), Is.EqualTo("ColossusHome"));
            Assert.That(back.VisibleInTree || back.Parent!.Visible, Is.True);
            Click(back);
            Assert.That(window.LastEntry.ToString(), Is.EqualTo("ColossusCatalogue"));
            Click(back);
            Assert.That(window.LastEntry.ToString(), Is.EqualTo("ColossusHome"));
            Assert.That(back.Disabled, Is.True);
            window.HandleClick("ColossusCatalogue");
            window.HandleClick("ColossusCatalogue");
            Click(back);
            Assert.That(window.LastEntry.ToString(), Is.EqualTo("ColossusHome"));
            Assert.That(back.Disabled, Is.True, "Selecting the current page must not create duplicate history entries.");

            window.UpdateGuides(new Dictionary<ProtoId<GuideEntryPrototype>, GuideEntry>
            {
                ["MedicalDoctor"] = prototypes.Index<GuideEntryPrototype>("MedicalDoctor"),
            });
            Assert.That(back.Disabled, Is.True, "A new contextual help request starts its own navigation history.");
            window.HandleClick("ColossusSurgery");
            Click(back);
            Assert.That(window.LastEntry.ToString(), Is.EqualTo("MedicalDoctor"));
            Assert.That(back.Disabled, Is.True);
        });
        await pair.CleanReturnAsync();
    }
}
