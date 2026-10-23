using System;
using System.Collections.Generic;
using Godot;

namespace sch;

// Only render locally observed card-play events; no network messages and no game-state changes.
internal sealed record HistoryEntry(string CardName, string PlayerId, string PlayerName, string ClassName, bool IsLocal, Color ClassColor);

internal static class HistoryStore
{
    private static readonly List<HistoryEntry> Items = new(250);
    internal static int Version { get; private set; }
    internal static IReadOnlyList<HistoryEntry> Entries => Items;

    internal static void Add(HistoryEntry entry)
    {
        if (string.IsNullOrWhiteSpace(entry.CardName)) return;
        Items.Add(entry);
        int limit = ModEntry.Config.HistoryLimit;
        if (Items.Count > limit) Items.RemoveRange(0, Items.Count - limit);
        Version++;
    }

    internal static void TrimToLimit()
    {
        int overflow = Items.Count - ModEntry.Config.HistoryLimit;
        if (overflow > 0) { Items.RemoveRange(0, overflow); Version++; }
    }

    internal static void Clear() { Items.Clear(); Version++; }
}
