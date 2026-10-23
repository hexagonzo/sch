using System;
using System.IO;
using System.Text.Json;
using Godot;

namespace sch;

internal sealed class Settings
{
    public bool Enabled { get; set; } = true;
    public bool RightSide { get; set; } = true;
    public int VisibleCards { get; set; } = 10;
    public int HistoryLimit { get; set; } = 250;
    public bool Collapsed { get; set; } = false;
    public int Width { get; set; } = 230;
    public int FontSize { get; set; } = 15;
    public float Opacity { get; set; } = 0.82f;
    // Stored in logical UI pixels. When false, the original left/right dock setting applies.
    public bool CustomPosition { get; set; } = false;
    public float PositionX { get; set; } = 14f;
    public float PositionY { get; set; } = 75f;
    public bool ClassColors { get; set; } = true;
    public bool ShowPlayerLabels { get; set; } = true;
    // 0 = all cards, 1 = own cards only, 2 = teammates only.
    public int PlayerFilter { get; set; } = 0;

    internal void Sanitize()
    {
        VisibleCards = Math.Clamp(VisibleCards, 3, 25);
        HistoryLimit = HistoryLimit <= 100 ? 100 : HistoryLimit <= 250 ? 250 : 500;
        Width = Math.Clamp(Width, 160, 400);
        FontSize = Math.Clamp(FontSize, 10, 24);
        Opacity = Math.Clamp(Opacity, 0.25f, 1f);
        PlayerFilter = Math.Clamp(PlayerFilter, 0, 2);
        if (!float.IsFinite(PositionX)) PositionX = 14f;
        if (!float.IsFinite(PositionY)) PositionY = 75f;
    }

    private static string ConfigPath => ProjectSettings.GlobalizePath("user://sch/settings.json");
    internal static Settings Load()
    {
        try
        {
            if (!File.Exists(ConfigPath)) return new();
            var result = JsonSerializer.Deserialize<Settings>(File.ReadAllText(ConfigPath)) ?? new();
            result.Sanitize();
            return result;
        }
        catch (Exception e) { GD.PrintErr("[sch] Invalid settings: " + e.Message); return new(); }
    }
    internal void Save()
    {
        try
        {
            Sanitize();
            Directory.CreateDirectory(Path.GetDirectoryName(ConfigPath)!);
            string temp = ConfigPath + ".tmp";
            File.WriteAllText(temp, JsonSerializer.Serialize(this, new JsonSerializerOptions { WriteIndented = true }));
            File.Move(temp, ConfigPath, true);
        }
        catch (Exception e) { GD.PrintErr("[sch] Could not save settings: " + e.Message); }
    }
}
