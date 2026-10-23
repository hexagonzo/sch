using System;
using System.Collections.Generic;
using Godot;
using MegaCrit.Sts2.Core.Combat;

namespace sch;

internal partial class HistoryOverlay : CanvasLayer
{
    internal static HistoryOverlay? Instance { get; private set; }
    private PanelContainer? panel;
    private VBoxContainer? rows;
    private ScrollContainer? scroll;
    private Button? collapseButton;
    private readonly List<HistoryEntry> renderedEntries = new();
    private int lastRenderedHistoryVersion = -1;
    private string lastRowsLayout = "";
    private Button? settingsButton;
    private PanelContainer? settingsPanel;
    private bool inCombat;
    private bool signalsConnected;
    private bool activated;
    private SceneTree? subscribedTree;
    private bool settingsOpen;
    private bool dragging;
    private Vector2 dragOffset;
    private Vector2 previousViewportSize;
    private HBoxContainer? dragHeader;
    private int lastVersion = -1;
    private string lastLayout = "";

    // Godot's managed lifecycle callbacks are not guaranteed for dynamically
    // instantiated C# Node subclasses from a plain Microsoft.NET.Sdk mod DLL.
    // Bootstrap explicitly instead of relying on _Ready() or _Process().
    internal void Activate()
    {
        if (activated) return;
        if (!IsInsideTree()) throw new InvalidOperationException("Overlay must be in the tree before activation");
        activated = true;
        Instance = this;
        Layer = 90;
        BuildUi();
        subscribedTree = GetTree();
        subscribedTree.ProcessFrame += OnProcessFrame;
        TreeExiting += OnTreeExiting;
        GD.Print("[sch] Overlay activated explicitly; panel=" + (panel is null ? "MISSING" : "ready"));
        Refresh();
    }

    private void OnTreeExiting()
    {
        if (subscribedTree is not null)
        {
            subscribedTree.ProcessFrame -= OnProcessFrame;
            subscribedTree = null;
        }
        DisconnectCombatSignals();
        if (ReferenceEquals(Instance, this)) Instance = null;
        activated = false;
        GD.Print("[sch] Overlay detached");
    }

    internal void ConnectCombatSignals()
    {
        if (signalsConnected) return;
        try
        {
            CombatManager.Instance.CombatSetUp += ModEntry.CombatStarted;
            CombatManager.Instance.CombatEnded += ModEntry.CombatFinished;
            signalsConnected = true;
            GD.Print("[sch] Combat signals connected");
        }
        catch (Exception e) { GD.PrintErr("[sch] Combat signal registration FAILED: " + e); }
    }

    private void DisconnectCombatSignals()
    {
        if (!signalsConnected) return;
        try
        {
            CombatManager.Instance.CombatSetUp -= ModEntry.CombatStarted;
            CombatManager.Instance.CombatEnded -= ModEntry.CombatFinished;
        }
        catch (Exception e) { GD.PrintErr("[sch] Combat signal cleanup: " + e.Message); }
        signalsConnected = false;
    }

    internal void ShowHistory() { inCombat = true; Refresh(); GD.Print("[sch] Overlay shown; visible=" + (panel?.Visible ?? false)); }
    internal void HideHistory() { inCombat = false; settingsOpen = false; Refresh(); }

    private void OnProcessFrame()
    {
        if (!activated) return;
        if (dragging)
        {
            if (!Input.IsMouseButtonPressed(MouseButton.Left))
            {
                dragging = false;
                CommitDrag();
            }
            else MoveTo(GetViewport().GetMousePosition() - dragOffset);
        }
        var config = ModEntry.Config;
        string layout = $"{config.Enabled}:{config.RightSide}:{config.VisibleCards}:{config.Width}:{config.FontSize}:{config.Opacity}:{config.ClassColors}:{config.ShowPlayerLabels}:{config.PlayerFilter}:{config.VisibleCards}:{config.HistoryLimit}:{config.Collapsed}:{settingsOpen}:{inCombat}";
        // Resize/fullscreen changes can otherwise strand a saved window off-screen.
        Vector2 viewport = GetViewport().GetVisibleRect().Size;
        if (viewport != previousViewportSize)
        {
            previousViewportSize = viewport;
            if (inCombat) Refresh();
        }
        if (HistoryStore.Version != lastVersion || layout != lastLayout) Refresh();
    }

    private void BuildUi()
    {
        panel = new PanelContainer { MouseFilter = Control.MouseFilterEnum.Stop };
        AddChild(panel);
        var vertical = new VBoxContainer();
        panel.AddChild(vertical);
        var header = new HBoxContainer();
        vertical.AddChild(header);
        dragHeader = header;
        header.MouseFilter = Control.MouseFilterEnum.Stop;
        header.GuiInput += OnHeaderInput;
        var title = new Label { Text = "CARD HISTORY  [drag]", SizeFlagsHorizontal = Control.SizeFlags.ExpandFill, MouseFilter = Control.MouseFilterEnum.Ignore };
        header.AddChild(title);
        settingsButton = new Button { Text = "⚙", TooltipText = "Card history settings" };
        settingsButton.Pressed += () => { settingsOpen = !settingsOpen; Refresh(); };
        header.AddChild(settingsButton);
        collapseButton = new Button { Text = "−", TooltipText = "Collapse / expand history" };
        collapseButton.Pressed += () =>
        {
            ModEntry.Config.Collapsed = !ModEntry.Config.Collapsed;
            ModEntry.Config.Save();
            Refresh();
        };
        header.AddChild(collapseButton);
        scroll = new ScrollContainer { HorizontalScrollMode = ScrollContainer.ScrollMode.Disabled,
            VerticalScrollMode = ScrollContainer.ScrollMode.Auto, MouseFilter = Control.MouseFilterEnum.Stop };
        vertical.AddChild(scroll);
        rows = new VBoxContainer { SizeFlagsHorizontal = Control.SizeFlags.ExpandFill };
        scroll.AddChild(rows);

        settingsPanel = new PanelContainer();
        vertical.AddChild(settingsPanel);
        var settings = new VBoxContainer();
        settingsPanel.AddChild(settings);
        AddToggle(settings, "Show history", () => ModEntry.Config.Enabled, v => ModEntry.Config.Enabled = v);
        AddToggle(settings, "Class colors", () => ModEntry.Config.ClassColors, v => ModEntry.Config.ClassColors = v);
        AddToggle(settings, "Player labels", () => ModEntry.Config.ShowPlayerLabels, v => ModEntry.Config.ShowPlayerLabels = v);
        var filterRow = new HBoxContainer();
        settings.AddChild(filterRow);
        filterRow.AddChild(new Label { Text = "Players", CustomMinimumSize = new Vector2(85, 0) });
        var filter = new OptionButton { SizeFlagsHorizontal = Control.SizeFlags.ExpandFill };
        filter.AddItem("Everyone", 0);
        filter.AddItem("Only me", 1);
        filter.AddItem("Teammates", 2);
        filter.Selected = ModEntry.Config.PlayerFilter;
        filter.ItemSelected += index => { ModEntry.Config.PlayerFilter = (int)index; ModEntry.Config.Save(); Instance?.Refresh(); };
        filterRow.AddChild(filter);
        var reset = new Button { Text = "Reset window position", TooltipText = "Restore left/right docking" };
        reset.Pressed += () =>
        {
            dragging = false;
            ModEntry.Config.CustomPosition = false;
            ModEntry.Config.Save();
            Refresh();
        };
        settings.AddChild(reset);
        AddToggle(settings, "Right side (default)", () => ModEntry.Config.RightSide, v =>
        {
            ModEntry.Config.RightSide = v;
            ModEntry.Config.CustomPosition = false;
        });
        AddNumber(settings, "Visible rows", 3, 25, () => ModEntry.Config.VisibleCards, v => ModEntry.Config.VisibleCards = v);
        var limitRow = new HBoxContainer();
        settings.AddChild(limitRow);
        limitRow.AddChild(new Label { Text = "Remember", CustomMinimumSize = new Vector2(85, 0) });
        var limit = new OptionButton { SizeFlagsHorizontal = Control.SizeFlags.ExpandFill };
        limit.AddItem("100 cards", 100); limit.AddItem("250 cards", 250); limit.AddItem("500 cards", 500);
        limit.Select(ModEntry.Config.HistoryLimit == 100 ? 0 : ModEntry.Config.HistoryLimit == 500 ? 2 : 1);
        limit.ItemSelected += index => { ModEntry.Config.HistoryLimit = index == 0 ? 100 : index == 1 ? 250 : 500; ModEntry.Config.Save(); HistoryStore.TrimToLimit(); Refresh(); };
        limitRow.AddChild(limit);
        AddNumber(settings, "Width", 160, 400, () => ModEntry.Config.Width, v => ModEntry.Config.Width = v);
        AddNumber(settings, "Font", 10, 24, () => ModEntry.Config.FontSize, v => ModEntry.Config.FontSize = v);
        AddNumber(settings, "Opacity %", 25, 100, () => (int)(ModEntry.Config.Opacity * 100), v => ModEntry.Config.Opacity = v / 100f);
    }

    // Drag only by the title bar: settings controls and card rows remain usable.
    private void OnHeaderInput(InputEvent input)
    {
        if (!inCombat || panel is null) return;
        if (input is InputEventMouseButton click && click.ButtonIndex == MouseButton.Left)
        {
            if (click.Pressed)
            {
                dragging = true;
                // Window coordinates and viewport mouse coordinates share canvas units.
                dragOffset = GetViewport().GetMousePosition() - panel.Position;
                dragHeader?.AcceptEvent();
            }
            else if (dragging)
            {
                dragging = false;
                CommitDrag();
                dragHeader?.AcceptEvent();
            }
        }
        else if (dragging && input is InputEventMouseMotion)
        {
            MoveTo(GetViewport().GetMousePosition() - dragOffset);
            dragHeader?.AcceptEvent();
        }
    }

    private void MoveTo(Vector2 candidate)
    {
        if (panel is null) return;
        var config = ModEntry.Config;
        Vector2 viewport = GetViewport().GetVisibleRect().Size;
        // Keep the complete window on screen where possible. If taller than
        // the viewport, keep at least its header visible so it can be reset.
        float panelWidth = Math.Max(config.Width, panel.Size.X);
        float panelHeight = Math.Max(32f, panel.Size.Y);
        float x = Math.Clamp(candidate.X, 0f, Math.Max(0f, viewport.X - panelWidth));
        float y = Math.Clamp(candidate.Y, 0f, Math.Max(0f, viewport.Y - Math.Min(panelHeight, viewport.Y)));
        config.CustomPosition = true;
        config.PositionX = x;
        config.PositionY = y;
        ApplyCustomPosition(new Vector2(x, y));
    }

    private void ApplyCustomPosition(Vector2 position)
    {
        if (panel is null) return;
        panel.AnchorLeft = panel.AnchorRight = 0;
        panel.AnchorTop = panel.AnchorBottom = 0;
        panel.OffsetLeft = position.X;
        panel.OffsetRight = position.X + ModEntry.Config.Width;
        panel.OffsetTop = position.Y;
    }

    private void CommitDrag()
    {
        // Do not persist 60+ times per second during a drag.
        if (ModEntry.Config.CustomPosition)
        {
            ModEntry.Config.Save();
            GD.Print("[sch] Window position saved: " + ModEntry.Config.PositionX + "," + ModEntry.Config.PositionY);
        }
    }

    private static void AddToggle(VBoxContainer parent, string title, Func<bool> get, Action<bool> set)
    {
        var check = new CheckBox { Text = title, ButtonPressed = get() };
        check.Toggled += v => { set(v); ModEntry.Config.Save(); Instance?.Refresh(); };
        parent.AddChild(check);
    }

    private static void AddNumber(VBoxContainer parent, string title, int min, int max, Func<int> get, Action<int> set)
    {
        var row = new HBoxContainer(); parent.AddChild(row);
        row.AddChild(new Label { Text = title, CustomMinimumSize = new Vector2(85, 0) });
        var value = new SpinBox { MinValue = min, MaxValue = max, Step = 1, Value = get(), SizeFlagsHorizontal = Control.SizeFlags.ExpandFill };
        value.ValueChanged += v => { set((int)v); ModEntry.Config.Save(); Instance?.Refresh(); };
        row.AddChild(value);
    }

    private static bool ShouldShow(HistoryEntry entry, int filter) =>
        filter == 0 || (filter == 1 && entry.IsLocal) || (filter == 2 && !entry.IsLocal);

    private string RowsLayout(Settings c) =>
        $"{c.Enabled}:{c.ClassColors}:{c.ShowPlayerLabels}:{c.PlayerFilter}:{c.FontSize}:{c.Width}";

    private Control CreateRow(HistoryEntry entry, int sequence, Settings c)
    {
        var line = new HBoxContainer { MouseFilter = Control.MouseFilterEnum.Pass };
        if (c.ClassColors)
            line.AddChild(new ColorRect { Color = entry.ClassColor, CustomMinimumSize = new Vector2(5, 0), MouseFilter = Control.MouseFilterEnum.Ignore });
        var content = new VBoxContainer { SizeFlagsHorizontal = Control.SizeFlags.ExpandFill, MouseFilter = Control.MouseFilterEnum.Ignore };
        line.AddChild(content);
        var label = new Label { Text = $"{sequence}. {entry.CardName}", AutowrapMode = TextServer.AutowrapMode.WordSmart,
            MouseFilter = Control.MouseFilterEnum.Ignore };
        label.AddThemeFontSizeOverride("font_size", c.FontSize);
        content.AddChild(label);
        if (c.ShowPlayerLabels)
        {
            var who = new Label { Text = $"{entry.PlayerName} · {entry.ClassName}", MouseFilter = Control.MouseFilterEnum.Ignore };
            who.AddThemeFontSizeOverride("font_size", Math.Max(10, c.FontSize - 3));
            if (c.ClassColors) who.AddThemeColorOverride("font_color", entry.ClassColor);
            content.AddChild(who);
        }
        return line;
    }

    private void ClearRows()
    {
        if (rows is null) return;
        foreach (Node node in rows.GetChildren()) { rows.RemoveChild(node); node.QueueFree(); }
        renderedEntries.Clear();
    }

    private void RefreshRows(Settings c)
    {
        if (rows is null || scroll is null) return;
        string layout = RowsLayout(c);
        if (layout == lastRowsLayout && lastRenderedHistoryVersion == HistoryStore.Version) return;
        var entries = HistoryStore.Entries;
        var desired = new List<(HistoryEntry entry, int number)>(entries.Count);
        if (c.Enabled)
            for (int i = entries.Count - 1; i >= 0; i--)
                if (ShouldShow(entries[i], c.PlayerFilter)) desired.Add((entries[i], i + 1));

        // If we're not viewing older entries, preserve the newest card at the top.
        // Otherwise preserve our current scroll position when a new play arrives.
        int previousScroll = scroll.ScrollVertical;
        bool followNewest = previousScroll <= 3;
        bool incremental = layout == lastRowsLayout && desired.Count > 0 && renderedEntries.Count > 0;
        // A normal card play prepends exactly one item (or none for an active filter).
        // For those cases, reuse all existing Godot nodes instead of rebuilding 250+ rows.
        int added = desired.Count > 0 && (renderedEntries.Count == 0 || !ReferenceEquals(desired[0].entry, renderedEntries[0])) ? 1 : 0;
        if (incremental && added == 1 && (desired.Count == 1 || ReferenceEquals(desired[1].entry, renderedEntries[0])))
        {
            var newNode = CreateRow(desired[0].entry, desired[0].number, c);
            rows.AddChild(newNode);
            rows.MoveChild(newNode, 0);
            renderedEntries.Insert(0, desired[0].entry);
            while (renderedEntries.Count > desired.Count)
            {
                int last = renderedEntries.Count - 1;
                var oldNode = rows.GetChild(last);
                rows.RemoveChild(oldNode); oldNode.QueueFree();
                renderedEntries.RemoveAt(last);
            }
        }
        else if (!incremental || desired.Count != renderedEntries.Count || added != 0)
        {
            ClearRows();
            foreach (var (entry, number) in desired)
            {
                rows.AddChild(CreateRow(entry, number, c));
                renderedEntries.Add(entry);
            }
            if (desired.Count == 0)
                rows.AddChild(new Label { Text = !c.Enabled ? "History disabled" : entries.Count == 0 ? "Play a card to begin…" : "No cards match this filter" });
        }
        lastRowsLayout = layout;
        lastRenderedHistoryVersion = HistoryStore.Version;
        // Defer scrollbar changes to the next UI layout frame.
        // Never move the viewport while the user is deliberately reading older cards.
        if (followNewest) scroll.ScrollVertical = 0;
        else if (added == 0) scroll.ScrollVertical = previousScroll;
        else scroll.ScrollVertical = previousScroll + (c.ShowPlayerLabels ? c.FontSize * 2 + 10 : c.FontSize + 10);
    }

    private void Refresh()
    {
        if (panel is null || rows is null || settingsPanel is null || scroll is null) return;
        var c = ModEntry.Config;
        c.Sanitize();
        panel.Visible = inCombat;
        if (!inCombat) return;
        settingsPanel.Visible = settingsOpen;
        scroll.Visible = !c.Collapsed;
        if (collapseButton is not null) { collapseButton.Text = c.Collapsed ? "+" : "−"; collapseButton.TooltipText = c.Collapsed ? "Expand history" : "Collapse history"; }
        // Only the list scrolls: the title bar and settings remain accessible.
        // Fixed upper bound prevents the panel growing beyond the viewport.
        float viewportHeight = GetViewport().GetVisibleRect().Size.Y;
        float perRow = c.ShowPlayerLabels ? c.FontSize * 2 + 15 : c.FontSize + 12;
        scroll.CustomMinimumSize = new Vector2(0, Math.Max(70f, Math.Min(viewportHeight * 0.5f, c.VisibleCards * perRow)));
        if (c.CustomPosition) MoveTo(new Vector2(c.PositionX, c.PositionY));
        else
        {
            panel.AnchorLeft = c.RightSide ? 1 : 0;
            panel.AnchorRight = c.RightSide ? 1 : 0;
            panel.AnchorTop = panel.AnchorBottom = 0;
            panel.OffsetLeft = c.RightSide ? -c.Width - 14 : 14;
            panel.OffsetRight = c.RightSide ? -14 : c.Width + 14;
            panel.OffsetTop = 75;
        }
        panel.Modulate = new Color(1, 1, 1, c.Opacity);
        if (settingsButton is not null) settingsButton.TooltipText = c.Enabled ? "Settings" : "History hidden - settings";
        RefreshRows(c);
        lastVersion = HistoryStore.Version;
        lastLayout = $"{c.Enabled}:{c.RightSide}:{c.VisibleCards}:{c.Width}:{c.FontSize}:{c.Opacity}:{c.ClassColors}:{c.ShowPlayerLabels}:{c.PlayerFilter}:{c.VisibleCards}:{c.HistoryLimit}:{c.Collapsed}:{settingsOpen}:{inCombat}";
    }
}
