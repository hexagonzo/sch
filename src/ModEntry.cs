using System;
using System.Reflection;
using Godot;
using HarmonyLib;
using MegaCrit.Sts2.Core.Combat;
using MegaCrit.Sts2.Core.Context;
using MegaCrit.Sts2.Core.Entities.Creatures;
using MegaCrit.Sts2.Core.Models;
using MegaCrit.Sts2.Core.Modding;
using MegaCrit.Sts2.Core.Nodes;
using MegaCrit.Sts2.Core.Rooms;

namespace sch;

[ModInitializer(nameof(Initialize))]
public static class ModEntry
{
    internal static Settings Config = new();
    private static bool initialized;
    private static int loggedPlays;

    public static void Initialize()
    {
        if (initialized) return;
        Config = Settings.Load();
        try
        {
            new Harmony("community.sch").PatchAll(typeof(ModEntry).Assembly);
            initialized = true;
            GD.Print("[sch] v0.3.2 initialized; Harmony patches registered; waiting for NRun._Ready");
        }
        catch (Exception e) { GD.PrintErr("[sch] Init FAILED: " + e); }
    }

    // Attach after a run's scene is ready rather than scheduling a deferred call
    // from the mod loader (which runs before the scene has been constructed).
    [HarmonyPatch(typeof(NRun), nameof(NRun._Ready))]
    private static class RunReadyPatch
    {
        private static void Postfix(NRun __instance)
        {
            try
            {
                if (__instance.GetNodeOrNull("schOverlay") is not null) return;
                var overlay = new HistoryOverlay { Name = "schOverlay" };
                __instance.AddChild(overlay);
                GD.Print("[sch] Overlay attached to NRun; insideTree=" + overlay.IsInsideTree());
                overlay.Activate();
                // Signal subscriptions are tied to this overlay's lifetime, so a new
                // run does not accumulate orphaned handlers.
                overlay.ConnectCombatSignals();
            }
            catch (Exception e) { GD.PrintErr("[sch] RunReady FAILED: " + e); }
        }
    }

    internal static void CombatStarted(CombatState state)
    {
        GD.Print("[sch] Combat started; overlay=" + (HistoryOverlay.Instance is null ? "MISSING" : "ready"));
        loggedPlays = 0;
        HistoryStore.Clear();
        HistoryOverlay.Instance?.ShowHistory();
    }

    internal static void CombatFinished(CombatRoom room)
    {
        GD.Print("[sch] Combat ended; recorded=" + HistoryStore.Entries.Count);
        HistoryOverlay.Instance?.HideHistory();
        HistoryStore.Clear();
    }

    [HarmonyPatch(typeof(CardModel), nameof(CardModel.OnPlayWrapper))]
    private static class PlayedCardPatch
    {
        private static void Prefix(CardModel __instance, Creature? target)
        {
            if (!Config.Enabled || __instance is null) return;
            try
            {
                var name = GetCardName(__instance);
                HistoryStore.Add(CreateEntry(__instance, name));
                if (loggedPlays++ < 5)
                    GD.Print("[sch] Card recorded: " + name + "; overlay=" + (HistoryOverlay.Instance is null ? "MISSING" : "ready"));
            }
            catch (Exception e) { GD.PrintErr("[sch] Card hook FAILED: " + e); }
        }
    }

    private static HistoryEntry CreateEntry(CardModel card, string name)
    {
        // Card.Owner is the owner of this particular played card, not the local player.
        // On co-op clients OnPlayWrapper may execute for remote players as well.
        var owner = card.Owner;
        string id = owner?.NetId.ToString() ?? "unknown";
        bool local = owner is not null && LocalContext.IsMe(owner);
        string character = owner?.Character?.GetType().Name ?? "Unknown";
        // Model names normally include the character type. Avoid relying on localized UI text.
        try
        {
            var model = owner?.Character;
            var idProperty = model?.GetType().GetProperty("Id", BindingFlags.Public | BindingFlags.Instance);
            string? modelId = idProperty?.GetValue(model)?.ToString();
            if (!string.IsNullOrWhiteSpace(modelId)) character = modelId;
        }
        catch { /* best effort */ }
        string key = character.ToUpperInvariant();
        string className;
        Color color;
        if (key.Contains("IRONCLAD")) { className = "Ironclad"; color = new Color(218/255f, 91/255f, 86/255f); }
        else if (key.Contains("SILENT")) { className = "Silent"; color = new Color(81/255f, 185/255f, 119/255f); }
        else if (key.Contains("DEFECT")) { className = "Defect"; color = new Color(87/255f, 158/255f, 224/255f); }
        else if (key.Contains("NECROBINDER")) { className = "Necrobinder"; color = new Color(169/255f, 120/255f, 211/255f); }
        else if (key.Contains("REGENT")) { className = "Regent"; color = new Color(234/255f, 188/255f, 91/255f); }
        else if (key.Contains("WATCHER")) { className = "Watcher"; color = new Color(175/255f, 137/255f, 216/255f); }
        else { className = "Unknown"; color = new Color(180/255f, 180/255f, 180/255f); }
        // A stable abbreviated player identifier differentiates duplicate classes without
        // depending on public profile names or access to a network service.
        string playerLabel = local ? "You" : "Player " + (id.Length > 6 ? id[^6..] : id);
        return new HistoryEntry(name, id, playerLabel, className, local, color);
    }

    private static string GetCardName(CardModel card)
    {
        var type = card.GetType();
        foreach (string property in new[] { "Title", "Name", "DisplayName", "Id" })
        {
            try
            {
                var info = type.GetProperty(property, BindingFlags.Public | BindingFlags.Instance);
                var value = info?.GetValue(card)?.ToString();
                if (!string.IsNullOrWhiteSpace(value)) return value;
            }
            catch { }
        }
        return type.Name.Replace("Model", "");
    }
}
