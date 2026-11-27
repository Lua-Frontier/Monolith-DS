using System.Linq;
using Content.Client._LuaM.UserInterface;
using Content.Client._LuaM.UserInterface.Hud;
using Content.Client.Stylesheets;
using Content.Client.UserInterface.Controls;
using Content.Shared.Traits;
using Robust.Client.Graphics;
using Robust.Client.UserInterface;
using Robust.Client.UserInterface.Controls;
using Robust.Shared.Prototypes;
using Robust.Shared.Utility;

namespace Content.Client._LuaM.Lobby.Traits;

public enum TraitState : byte
{
    Available,
    Selected,
    Unaffordable,
    Blocked,
}

public sealed record TraitView(TraitPrototype Proto, TraitState State, string? Reason);

public sealed record TraitCategoryView(string Id, string Name, int Spent, int? Max, List<TraitView> Traits);

public sealed class TraitsEditor : BoxContainer
{
    public const string SelectedCategory = "#selected";

    private readonly BoxContainer _categories;
    private readonly Label _title;
    private readonly Label _subtitle;
    private readonly Label _points;
    private readonly LineEdit _search;
    private const string SplitCategory = "Physical";

    private readonly GridContainer _cards;
    private readonly BoxContainer _split;
    private readonly BoxContainer _positive;
    private readonly BoxContainer _negative;
    private readonly Label _empty;
    private readonly BoxContainer _details;
    private List<TraitCategoryView> _data = new();
    private string _current = SelectedCategory;
    private ProtoId<TraitPrototype>? _focused;

    public event Action<ProtoId<TraitPrototype>>? OnTraitToggled;
    public event Action? OnClearAll;

    public TraitsEditor()
    {
        Orientation = LayoutOrientation.Horizontal;
        HorizontalExpand = true;
        VerticalExpand = true;
        SeparationOverride = 12;
        Margin = new Thickness(0, 4, 0, 0);

        _categories = new BoxContainer { Orientation = LayoutOrientation.Vertical, SeparationOverride = 6 };
        AddChild(new BoxContainer
        {
            Orientation = LayoutOrientation.Vertical,
            MinWidth = 250,
            SeparationOverride = 6,
            Children =
            {
                new Label { Text = Loc.GetString("traits-editor-categories"), StyleClasses = { LobbyHudStyles.MonoDim } },
                new ScrollContainer { VerticalExpand = true, HScrollEnabled = false, Children = { _categories } },
            },
        });

        _title = new Label { StyleClasses = { LobbyHudStyles.Title }, FontColorOverride = HudPalette.Text, VerticalAlignment = VAlignment.Center };
        _subtitle = new Label { StyleClasses = { LobbyHudStyles.Mono }, VerticalAlignment = VAlignment.Center };
        _points = new Label { StyleClasses = { LobbyHudStyles.Big }, FontColorOverride = HudPalette.Fg, VerticalAlignment = VAlignment.Center };

        var banner = new PanelContainer
        {
            PanelOverride = new StyleBoxFlat { BackgroundColor = HudPalette.PanelRaised, BorderColor = HudPalette.LineDim, BorderThickness = new Thickness(0, 0, 0, 1) },
            Children =
            {
                new BoxContainer
                {
                    Orientation = LayoutOrientation.Vertical,
                    Children =
                    {
                        new BoxContainer
                        {
                            Orientation = LayoutOrientation.Horizontal,
                            SeparationOverride = 12,
                            Margin = new Thickness(12, 6),
                            Children = { _title, _subtitle, new Control { HorizontalExpand = true }, _points },
                        },
                    },
                },
            },
        };

        _search = new LineEdit
        {
            HorizontalExpand = true,
            PlaceHolder = Loc.GetString("traits-editor-search"),
            StyleClasses = { LobbyHudStyles.Input },
        };
        _search.OnTextChanged += _ => RebuildCards();

        var clear = new ConfirmButton
        {
            Text = Loc.GetString("humanoid-profile-editor-clear-all-traits-button"),
            ConfirmationText = Loc.GetString("humanoid-profile-editor-clear-all-traits-confirm"),
            StyleClasses = { LobbyHudStyles.Danger },
        };
        clear.OnPressed += _ => OnClearAll?.Invoke();

        var searchRow = new PanelContainer
        {
            PanelOverride = new StyleBoxFlat { BackgroundColor = Color.FromHex("#121618"), BorderColor = HudPalette.Border, BorderThickness = new Thickness(1) },
            Children =
            {
                new BoxContainer
                {
                    Orientation = LayoutOrientation.Horizontal,
                    SeparationOverride = 8,
                    Margin = new Thickness(10, 4),
                    Children =
                    {
                        new Label { Text = ">", StyleClasses = { LobbyHudStyles.Mono }, FontColorOverride = HudPalette.Orange, VerticalAlignment = VAlignment.Center },
                        _search,
                        clear,
                    },
                },
            },
        };

        _cards = new GridContainer { Columns = 2, HorizontalExpand = true, HSeparationOverride = 4, VSeparationOverride = 4 };
        _positive = new BoxContainer { Orientation = LayoutOrientation.Vertical, HorizontalExpand = true, SeparationOverride = 4 };
        _negative = new BoxContainer { Orientation = LayoutOrientation.Vertical, HorizontalExpand = true, SeparationOverride = 4 };
        _split = new BoxContainer
        {
            Orientation = LayoutOrientation.Horizontal,
            HorizontalExpand = true,
            SeparationOverride = 4,
            Visible = false,
            Children = { _positive, _negative },
        };
        _empty = new Label
        {
            Text = Loc.GetString("traits-editor-empty"),
            StyleClasses = { LobbyHudStyles.MonoDim },
            HorizontalAlignment = HAlignment.Center,
            Margin = new Thickness(0, 20),
            Visible = false,
        };

        AddChild(new BoxContainer
        {
            Orientation = LayoutOrientation.Vertical,
            HorizontalExpand = true,
            VerticalExpand = true,
            SeparationOverride = 8,
            Children =
            {
                banner,
                searchRow,
                new ScrollContainer
                {
                    VerticalExpand = true,
                    HScrollEnabled = false,
                    Children =
                    {
                        new BoxContainer
                        {
                            Orientation = LayoutOrientation.Vertical,
                            HorizontalExpand = true,
                            Margin = new Thickness(0, 0, 6, 0),
                            Children = { _cards, _split, _empty },
                        },
                    },
                },
            },
        });

        _details = new BoxContainer { Orientation = LayoutOrientation.Vertical, SeparationOverride = 10, Margin = new Thickness(14) };
        AddChild(new PanelContainer
        {
            MinWidth = 460,
            MaxWidth = 460,
            VerticalAlignment = VAlignment.Top,
            PanelOverride = new StyleBoxGrid { Step = 0 },
            Children = { _details },
        });
    }

    public void SetData(List<TraitCategoryView> data)
    {
        _data = data;
        if (_current != SelectedCategory && data.All(c => c.Id != _current))
            _current = SelectedCategory;

        RebuildCategories();
        RebuildCards();
    }

    private IEnumerable<(TraitCategoryView Category, TraitView Trait)> AllTraits()
    {
        return _data.SelectMany(c => c.Traits.Select(t => (c, t)));
    }

    private void RebuildCategories()
    {
        _categories.RemoveAllChildren();

        var selectedCount = AllTraits().Count(x => x.Trait.State == TraitState.Selected);
        AddCategoryCard(SelectedCategory, Loc.GetString("traits-editor-selected"), Loc.GetString("traits-editor-selected-hint"), selectedCount.ToString(), 1);

        foreach (var category in _data)
        {
            var count = category.Traits.Count(t => t.State == TraitState.Selected);
            if (category.Max is { } max and > 0)
            {
                var left = Math.Max(0, max - category.Spent);
                AddCategoryCard(category.Id, category.Name, Loc.GetString("traits-editor-chosen", ("count", count)), $"{left}/{max}", (float) left / max);
            }
            else
            {
                AddCategoryCard(category.Id, category.Name, Loc.GetString("traits-editor-no-limit-short"), count.ToString(), count > 0 ? 1 : 0);
            }
        }
    }

    private void AddCategoryCard(string id, string name, string hint, string ringText, float fill)
    {
        var active = id == _current;
        var accent = active ? HudPalette.Orange : HudPalette.BorderBright;

        var ring = new HudRing { Text = ringText, Fill = fill, SetSize = new(52, 52) };
        ring.SetColor(accent);

        var button = new ContainerButton
        {
            StyleBoxOverride = new StyleBoxHud(
                active ? HudPalette.OrangeDark : HudPalette.Panel,
                active ? HudPalette.Orange : HudPalette.Border,
                active ? HudPalette.Orange : null) ,
        };
        button.AddChild(new BoxContainer
        {
            Orientation = LayoutOrientation.Horizontal,
            SeparationOverride = 10,
            Margin = new Thickness(12, 8, 10, 8),
            Children =
            {
                new BoxContainer
                {
                    Orientation = LayoutOrientation.Vertical,
                    HorizontalExpand = true,
                    VerticalAlignment = VAlignment.Center,
                    Children =
                    {
                        new Label { Text = name, ClipText = true, StyleClasses = { LobbyHudStyles.BodyBold }, FontColorOverride = active ? HudPalette.Orange : HudPalette.Text },
                        new Label { Text = hint, StyleClasses = { LobbyHudStyles.MonoDim } },
                    },
                },
                ring,
            },
        });

        button.OnPressed += _ =>
        {
            _current = id;
            _search.SetText(string.Empty, false);
            RebuildCategories();
            RebuildCards();
        };

        _categories.AddChild(button);
    }

    private void RebuildCards()
    {
        _cards.RemoveAllChildren();
        _positive.RemoveAllChildren();
        _negative.RemoveAllChildren();
        var search = _search.Text.Trim();
        var category = _data.FirstOrDefault(c => c.Id == _current);

        List<(TraitCategoryView Category, TraitView Trait)> traits;
        if (search.Length > 0)
        {
            traits = AllTraits()
                .Where(x => Loc.GetString(x.Trait.Proto.Name).Contains(search, StringComparison.CurrentCultureIgnoreCase)
                            || x.Trait.Proto.Description is { } desc && Loc.GetString(desc).Contains(search, StringComparison.CurrentCultureIgnoreCase))
                .ToList();
            _title.Text = Loc.GetString("traits-editor-search-title").ToUpperInvariant();
            _subtitle.Text = "// " + search.ToUpperInvariant();
            _points.Text = string.Empty;
        }
        else if (category == null)
        {
            traits = AllTraits().Where(x => x.Trait.State == TraitState.Selected).ToList();
            _title.Text = Loc.GetString("traits-editor-selected").ToUpperInvariant();
            _subtitle.Text = Loc.GetString("traits-editor-selected-hint").ToUpperInvariant();
            _points.Text = traits.Count.ToString();
        }
        else
        {
            traits = category.Traits.Select(t => (category, t)).ToList();
            _title.Text = category.Name.ToUpperInvariant();
            if (category.Max is { } max and > 0)
            {
                _subtitle.Text = Loc.GetString("traits-editor-points-left");
                _points.Text = $"{Math.Max(0, max - category.Spent)} / {max}";
            }
            else
            {
                _subtitle.Text = Loc.GetString("traits-editor-no-limit").ToUpperInvariant();
                _points.Text = string.Empty;
            }
        }

        var split = search.Length == 0 && category?.Id == SplitCategory && traits.Count > 0;
        _cards.Visible = !split;
        _split.Visible = split;

        if (split)
        {
            traits = traits.Where(x => x.Trait.Proto.Cost >= 0)
                .OrderByDescending(x => x.Trait.Proto.Cost)
                .Concat(traits.Where(x => x.Trait.Proto.Cost < 0).OrderBy(x => x.Trait.Proto.Cost))
                .ToList();

            _positive.AddChild(CreateColumnHeader(Loc.GetString("traits-editor-positive"), HudPalette.Fg));
            _negative.AddChild(CreateColumnHeader(Loc.GetString("traits-editor-negative"), HudPalette.Good));

            foreach (var (_, trait) in traits)
            {
                (trait.Proto.Cost >= 0 ? _positive : _negative).AddChild(CreateCard(trait, null));
            }
        }
        else
        {
            foreach (var (cat, trait) in traits)
            {
                _cards.AddChild(CreateCard(trait, category == null || search.Length > 0 ? cat.Name : null));
            }
        }

        _empty.Visible = traits.Count == 0;

        var focus = traits.FirstOrDefault(x => x.Trait.Proto.ID == _focused);
        if (focus.Trait == null)
            focus = traits.FirstOrDefault(x => x.Trait.State == TraitState.Selected);
        if (focus.Trait == null && traits.Count > 0)
            focus = traits[0];

        ShowDetails(focus.Trait, focus.Category);
    }

    private void ShowDetails(TraitView? trait, TraitCategoryView? category)
    {
        _details.RemoveAllChildren();

        if (trait == null)
        {
            var empty = new RichTextLabel { HorizontalExpand = true };
            empty.SetMessage(FormattedMessage.FromUnformatted(Loc.GetString("traits-editor-details-empty")), defaultColor: HudPalette.TextDim);
            _details.AddChild(empty);
            return;
        }

        var selected = trait.State == TraitState.Selected;
        var blocked = trait.State is TraitState.Blocked or TraitState.Unaffordable;
        var stateText = Loc.GetString(selected ? "traits-editor-state-selected" : blocked ? "traits-editor-state-blocked" : "traits-editor-state-available");
        var status = selected ? HudStatus.Nominal : blocked ? HudStatus.Warning : HudStatus.Advisory;
        var stateColor = HudStatusColors.TextOf(status);

        _details.AddChild(new BoxContainer
        {
            Orientation = LayoutOrientation.Horizontal,
            SeparationOverride = 8,
            Children =
            {
                new HudShape { Status = status },
                new Label { Text = stateText, StyleClasses = { LobbyHudStyles.Heading }, FontColorOverride = stateColor },
            },
        });

        _details.AddChild(new Label { Text = Loc.GetString(trait.Proto.Name).ToUpperInvariant(), StyleClasses = { LobbyHudStyles.TraitTitle } });
        _details.AddChild(new Label
        {
            Text = Loc.GetString("traits-editor-details-meta", ("category", category?.Name ?? string.Empty), ("cost", trait.Proto.Cost)),
            StyleClasses = { LobbyHudStyles.MonoDim },
        });

        if (trait.Proto.Description is { } desc)
        {
            var text = new RichTextLabel { HorizontalExpand = true };
            text.SetMessage(FormattedMessage.FromMarkupPermissive(Loc.GetString(desc)), defaultColor: HudPalette.Text);
            _details.AddChild(text);
        }

        if (trait.Reason != null)
        {
            var reason = new RichTextLabel { HorizontalExpand = true };
            reason.SetMessage(FormattedMessage.FromMarkupPermissive(trait.Reason), defaultColor: HudPalette.Text);
            var strip = new HudCallout { Status = HudStatus.Warning };
            var reasonRow = new BoxContainer { Orientation = LayoutOrientation.Vertical, HorizontalExpand = true, SeparationOverride = 2 };
            reasonRow.AddChild(new Label
            {
                Text = Loc.GetString(trait.State == TraitState.Unaffordable ? "traits-editor-lead-points" : "traits-editor-lead-exclusive").ToUpperInvariant(),
                StyleClasses = { LobbyHudStyles.NoteLead },
                FontColorOverride = HudPalette.Bad,
            });
            reasonRow.AddChild(reason);
            strip.SetContent(reasonRow);
            _details.AddChild(strip);
        }

        var action = new Button
        {
            Text = Loc.GetString(selected ? "traits-editor-remove" : "traits-editor-add"),
            Disabled = blocked,
            StyleClasses = { selected ? LobbyHudStyles.Danger : LobbyHudStyles.Primary },
            Margin = new Thickness(0, 6, 0, 0),
        };
        action.OnPressed += _ => OnTraitToggled?.Invoke(trait.Proto.ID);
        _details.AddChild(action);
    }

    private static Control CreateColumnHeader(string text, Color color)
    {
        return new Label
        {
            Text = text.ToUpperInvariant(),
            StyleClasses = { LobbyHudStyles.Mono },
            FontColorOverride = color,
            Margin = new Thickness(4, 0, 0, 2),
        };
    }

    private Control CreateCard(TraitView trait, string? categoryName)
    {
        var selected = trait.State == TraitState.Selected;
        var blocked = trait.State is TraitState.Blocked or TraitState.Unaffordable;

        var normal = blocked
            ? new StyleBoxHud(StyleBoxLamp.Mix(HudPalette.Bad, HudPalette.Panel, 0.08f), StyleBoxLamp.Mix(HudPalette.Bad, HudPalette.Border, 0.55f))
            : new StyleBoxHud(
                selected ? HudPalette.OrangeDark : HudPalette.Panel,
                selected ? HudPalette.Orange : HudPalette.Border,
                selected ? HudPalette.Orange : null);
        var hover = blocked
            ? new StyleBoxHud(StyleBoxLamp.Mix(HudPalette.Bad, HudPalette.Panel, 0.14f), HudPalette.Bad)
            : new StyleBoxHud(
                selected ? HudPalette.OrangeDark : HudPalette.PanelHover,
                selected ? HudPalette.Orange : HudPalette.BorderBright,
                selected ? HudPalette.Orange : null);
        normal.SetContentMarginOverride(StyleBox.Margin.All, 3);
        hover.SetContentMarginOverride(StyleBox.Margin.All, 3);

        var card = new ContainerButton
        {
            HorizontalExpand = true,
            StyleBoxOverride = normal,
            ModulateSelfOverride = Color.White,
        };
        card.OnMouseEntered += _ =>
        {
            card.StyleBoxOverride = hover;
            _focused = trait.Proto.ID;
            ShowDetails(trait, _data.FirstOrDefault(c => c.Traits.Contains(trait)));
        };
        card.OnMouseExited += _ => card.StyleBoxOverride = normal;
        card.OnPressed += _ =>
        {
            if (!blocked)
                OnTraitToggled?.Invoke(trait.Proto.ID);
        };

        var (costText, costColor) = trait.Proto.Cost switch
        {
            > 0 => ($"-{trait.Proto.Cost}", HudPalette.Fg),
            < 0 => ($"+{-trait.Proto.Cost}", HudPalette.Good),
            _ => ("0", HudPalette.TextDim),
        };

        var body = new BoxContainer { Orientation = LayoutOrientation.Vertical, Margin = new Thickness(8, 3, 6, 3), SeparationOverride = 2 };
        body.AddChild(new BoxContainer
        {
            Orientation = LayoutOrientation.Horizontal,
            SeparationOverride = 8,
            Children =
            {
                new HudShape { Status = HudStatus.Warning, Visible = blocked },
                new Label
                {
                    Text = Loc.GetString(trait.Proto.Name),
                    HorizontalExpand = true,
                    ClipText = true,
                    StyleClasses = { LobbyHudStyles.TraitCard },
                    FontColorOverride = selected ? HudPalette.Orange : blocked ? StyleBoxLamp.Mix(HudPalette.Bad, HudPalette.Text, 0.6f) : HudPalette.Text,
                },
                new PanelContainer
                {
                    VerticalAlignment = VAlignment.Center,
                    PanelOverride = new StyleBoxFlat { BackgroundColor = HudPalette.Background, BorderColor = costColor, BorderThickness = new Thickness(1) },
                    Children = { new Label { Text = costText, StyleClasses = { LobbyHudStyles.Mono }, FontColorOverride = costColor, Margin = new Thickness(5, 0) } },
                },
            },
        });

        card.AddChild(body);
        return card;
    }
}
