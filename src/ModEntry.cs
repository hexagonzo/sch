using System;
using System.Reflection;
using Godot;
using HarmonyLib;
using MegaCrit.Sts2.Core.Combat;
using MegaCrit.Sts2.Core.Entities.Creatures;
using MegaCrit.Sts2.Core.Models;
using MegaCrit.Sts2.Core.Modding;
using MegaCrit.Sts2.Core.Rooms;

namespace sch;

[ModInitializer(nameof(Initialize))]
public static class ModEntry
{
    internal static Settings Config = new();
    private static bool initialized;

    public static void Initialize()
    {
        if (initialized) return;
        initialized = true;
        Config = Settings.Load();
        try
        {
            new Harmony("community.sch").PatchAll(typeof(ModEntry).Assembly);
            Callable.From(Attach).CallDeferred();
            GD.Print("[sch] v0.1.0 initialized");
        }
        catch (Exception e) { GD.PrintErr("[sch] Init: " + e); }
    }

    private static void Attach()
    {
        try
        {
            CombatManager.Instance.CombatSetUp += OnCombatStart;
            CombatManager.Instance.CombatEnded += OnCombatEnd;
            var tree = Engine.GetMainLoop() as SceneTree;
            if (tree is null) return;
            var overlay = new HistoryOverlay { Name = "schOverlay" };
            tree.Root.AddChild(overlay);
            overlay.HideHistory();
        }
        catch (Exception e) { GD.PrintErr("[sch] Attach: " + e); }
    }

    private static void OnCombatStart(CombatState state)
    {
        HistoryStore.Clear();
        HistoryOverlay.Instance?.ShowHistory();
    }
    private static void OnCombatEnd(CombatRoom room)
    {
        HistoryOverlay.Instance?.HideHistory();
        HistoryStore.Clear();
    }

    [HarmonyPatch(typeof(CardModel), nameof(CardModel.OnPlayWrapper))]
    private static class PlayedCardPatch
    {
        // One prefix invocation per actual card play, including autoplay.
        // We avoid modifying the game's returned Task or card behavior.
        private static void Prefix(CardModel __instance, Creature? target)
        {
            if (!Config.Enabled || __instance is null) return;
            try { HistoryStore.Add(GetCardName(__instance)); }
            catch (Exception e) { GD.PrintErr("[sch] Card hook: " + e.Message); }
        }
    }

    private static string GetCardName(CardModel card)
    {
        // Early Access builds change model properties; only resolve display metadata
        // after a card has been played, never on the render loop.
        var type = card.GetType();
        foreach (string property in new[] { "Title", "Name", "DisplayName", "Id" })
        {
            try
            {
                var info = type.GetProperty(property, BindingFlags.Public | BindingFlags.Instance);
                var value = info?.GetValue(card)?.ToString();
                if (!string.IsNullOrWhiteSpace(value)) return value;
            }
            catch { /* fallback to stable type name */ }
        }
        return type.Name.Replace("Model", "");
    }
}
