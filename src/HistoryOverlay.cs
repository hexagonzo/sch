using System;
using Godot;

namespace sch;

internal partial class HistoryOverlay : CanvasLayer
{
    internal static HistoryOverlay? Instance { get; private set; }
    private PanelContainer? panel;
    private VBoxContainer? rows;
    private Button? settingsButton;
    private PanelContainer? settingsPanel;
    private bool inCombat;
    private bool settingsOpen;
    private int lastVersion = -1;
    private string lastLayout = "";

    public override void _Ready()
    {
        Instance = this;
        Layer = 90;
        BuildUi();
        ProcessMode = ProcessModeEnum.Always;
    }

    public override void _ExitTree() { if (ReferenceEquals(Instance, this)) Instance = null; }

    internal void ShowHistory() { inCombat = true; Refresh(); }
    internal void HideHistory() { inCombat = false; settingsOpen = false; Refresh(); }

    public override void _Process(double delta)
    {
        // Constant-time dirty check; no per-frame node allocation.
        var config = ModEntry.Config;
        string layout = $"{config.Enabled}:{config.RightSide}:{config.VisibleCards}:{config.Width}:{config.FontSize}:{config.Opacity}:{settingsOpen}:{inCombat}";
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
        var title = new Label { Text = "CARD HISTORY", SizeFlagsHorizontal = Control.SizeFlags.ExpandFill };
        header.AddChild(title);
        settingsButton = new Button { Text = "⚙", TooltipText = "Card history settings" };
        settingsButton.Pressed += () => { settingsOpen = !settingsOpen; Refresh(); };
        header.AddChild(settingsButton);
        rows = new VBoxContainer();
        vertical.AddChild(rows);

        settingsPanel = new PanelContainer();
        vertical.AddChild(settingsPanel);
        var settings = new VBoxContainer();
        settingsPanel.AddChild(settings);
        AddToggle(settings, "Show history", () => ModEntry.Config.Enabled, v => ModEntry.Config.Enabled = v);
        AddToggle(settings, "Right side", () => ModEntry.Config.RightSide, v => ModEntry.Config.RightSide = v);
        AddNumber(settings, "Cards", 3, 35, () => ModEntry.Config.VisibleCards, v => ModEntry.Config.VisibleCards = v);
        AddNumber(settings, "Width", 160, 400, () => ModEntry.Config.Width, v => ModEntry.Config.Width = v);
        AddNumber(settings, "Font", 10, 24, () => ModEntry.Config.FontSize, v => ModEntry.Config.FontSize = v);
        AddNumber(settings, "Opacity %", 25, 100, () => (int)(ModEntry.Config.Opacity * 100), v => ModEntry.Config.Opacity = v / 100f);
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

    private void Refresh()
    {
        if (panel is null || rows is null || settingsPanel is null) return;
        var c = ModEntry.Config;
        c.Sanitize();
        panel.Visible = inCombat;
        if (!inCombat) return;
        settingsPanel.Visible = settingsOpen;
        panel.AnchorLeft = c.RightSide ? 1 : 0;
        panel.AnchorRight = c.RightSide ? 1 : 0;
        panel.AnchorTop = 0;
        panel.AnchorBottom = 0;
        panel.OffsetLeft = c.RightSide ? -c.Width - 14 : 14;
        panel.OffsetRight = c.RightSide ? -14 : c.Width + 14;
        panel.OffsetTop = 75;
        panel.Modulate = new Color(1, 1, 1, c.Opacity);
        // Always keep the gear reachable even when history is turned off.
        if (settingsButton is not null) settingsButton.TooltipText = c.Enabled ? "Settings" : "History hidden - settings";
        if (HistoryStore.Version != lastVersion || lastLayout != $"{c.Enabled}:{c.RightSide}:{c.VisibleCards}:{c.Width}:{c.FontSize}:{c.Opacity}:{settingsOpen}:{inCombat}")
        {
            foreach (Node item in rows.GetChildren()) { rows.RemoveChild(item); item.QueueFree(); }
            if (c.Enabled)
            {
                var entries = HistoryStore.Entries;
                for (int i = entries.Count - 1, shown = 0; i >= 0 && shown < c.VisibleCards; i--, shown++)
                {
                    var label = new Label { Text = $"{entries.Count - i}. {entries[i]}", AutowrapMode = TextServer.AutowrapMode.WordSmart, MouseFilter = Control.MouseFilterEnum.Ignore };
                    label.AddThemeFontSizeOverride("font_size", c.FontSize);
                    rows.AddChild(label);
                }
                if (entries.Count == 0) rows.AddChild(new Label { Text = "Play a card to begin…" });
            }
            else rows.AddChild(new Label { Text = "History disabled" });
        }
        lastVersion = HistoryStore.Version;
        lastLayout = $"{c.Enabled}:{c.RightSide}:{c.VisibleCards}:{c.Width}:{c.FontSize}:{c.Opacity}:{settingsOpen}:{inCombat}";
    }
}
