using System;
using System.Collections.Generic;

namespace sch;

internal static class HistoryStore
{
    private static readonly List<string> Items = new(64);
    internal static int Version { get; private set; }
    internal static IReadOnlyList<string> Entries => Items;

    internal static void Add(string name)
    {
        if (string.IsNullOrWhiteSpace(name)) return;
        if (name.Length > 90) name = name[..90] + "…";
        Items.Add(name);
        // The buffer must be bounded even when UI isn't visible.
        if (Items.Count > 100) Items.RemoveRange(0, Items.Count - 100);
        Version++;
    }

    internal static void Clear() { Items.Clear(); Version++; }
}
