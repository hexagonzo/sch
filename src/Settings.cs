using System;
using System.IO;
using System.Text.Json;
using Godot;

namespace sch;

internal sealed class Settings
{
    public bool Enabled { get; set; } = true;
    public bool RightSide { get; set; } = true;
    public int VisibleCards { get; set; } = 12;
    public int Width { get; set; } = 230;
    public int FontSize { get; set; } = 15;
    public float Opacity { get; set; } = 0.82f;

    internal void Sanitize()
    {
        VisibleCards = Math.Clamp(VisibleCards, 3, 35);
        Width = Math.Clamp(Width, 160, 400);
        FontSize = Math.Clamp(FontSize, 10, 24);
        Opacity = Math.Clamp(Opacity, 0.25f, 1f);
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
