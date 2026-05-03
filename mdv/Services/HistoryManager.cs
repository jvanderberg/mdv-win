using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.IO;
using System.Linq;
using System.Text.Json;
using CommunityToolkit.Mvvm.ComponentModel;

namespace Mdv.Services;

public sealed record HistoryEntry(Guid Id, string Path, DateTimeOffset AddedAt)
{
    public string Filename => System.IO.Path.GetFileName(Path);
}

public sealed partial class HistoryManager : ObservableObject
{
    private const string StorageKey = "mdv_history";
    private const int MaxEntries = 100;

    public ObservableCollection<HistoryEntry> Entries { get; } = new();

    public HistoryManager()
    {
        Load();
        _ = Database.Shared.ReindexAsync(Entries.Select(e => e.Path).ToList());
    }

    public HistoryEntry Add(string path)
    {
        for (int i = Entries.Count - 1; i >= 0; i--)
            if (Entries[i].Path == path) Entries.RemoveAt(i);

        var entry = new HistoryEntry(Guid.NewGuid(), path, DateTimeOffset.UtcNow);
        Entries.Insert(0, entry);

        while (Entries.Count > MaxEntries) Entries.RemoveAt(Entries.Count - 1);

        Save();
        _ = Database.Shared.IndexFileAsync(path);
        return entry;
    }

    public void Remove(HistoryEntry entry)
    {
        Entries.Remove(entry);
        Save();
        _ = Database.Shared.RemoveFileAsync(entry.Path);
    }

    public void Clear()
    {
        var paths = Entries.Select(e => e.Path).ToList();
        Entries.Clear();
        Save();
        foreach (var p in paths) _ = Database.Shared.RemoveFileAsync(p);
    }

    private void Save()
    {
        Settings.Set(StorageKey, Entries.ToList());
    }

    private void Load()
    {
        var decoded = Settings.Get<List<HistoryEntry>>(StorageKey);
        if (decoded == null) return;
        Entries.Clear();
        foreach (var e in decoded) Entries.Add(e);
    }
}
