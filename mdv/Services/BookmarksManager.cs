using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.IO;
using System.Linq;
using CommunityToolkit.Mvvm.ComponentModel;

namespace Mdv.Services;

public sealed partial class Bookmark : ObservableObject
{
    public long Id { get; }
    public string Path { get; }
    [ObservableProperty] private string _title = "";
    [ObservableProperty] private int _sortOrder;
    public DateTimeOffset CreatedAt { get; }
    public int BlockIndex { get; }
    public string BlockFingerprint { get; }
    [ObservableProperty] private bool _fileExists;
    /// 1..MaxSlots if this bookmark sits in a hotkey slot (Ctrl+1..Ctrl+5),
    /// null otherwise. Updated by BookmarksManager whenever the list reorders.
    [ObservableProperty] private int? _slot;

    public string Filename => System.IO.Path.GetFileName(Path);

    public Bookmark(long id, string path, string title, int sortOrder, DateTimeOffset createdAt,
        int blockIndex, string blockFingerprint, bool fileExists, int? slot)
    {
        Id = id; Path = path; _title = title; _sortOrder = sortOrder;
        CreatedAt = createdAt; BlockIndex = blockIndex; BlockFingerprint = blockFingerprint;
        _fileExists = fileExists; _slot = slot;
    }
}

public sealed partial class BookmarksManager : ObservableObject
{
    public const int MaxSlots = 5;

    public ObservableCollection<Bookmark> Bookmarks { get; } = new();

    public BookmarksManager() => Reload();

    public void Reload()
    {
        Bookmarks.Clear();
        var rows = Database.Shared.LoadBookmarks();
        for (int i = 0; i < rows.Count; i++)
        {
            var row = rows[i];
            Bookmarks.Add(new Bookmark(
                row.Id, row.Path, row.Title, row.SortOrder, row.CreatedAt,
                row.BlockIndex, row.BlockFingerprint, File.Exists(row.Path),
                i < MaxSlots ? i + 1 : null));
        }
    }

    public void RefreshFileExistence()
    {
        foreach (var b in Bookmarks) b.FileExists = File.Exists(b.Path);
    }

    /// True if `path` has at least one bookmark anywhere — drives the
    /// toolbar bookmark indicator (filled vs hollow icon).
    public bool HasAnyBookmarkForPath(string? path) =>
        !string.IsNullOrEmpty(path) && Bookmarks.Any(b => b.Path == path);

    public Bookmark? Add(string path, string title, int blockIndex, string fingerprint)
    {
        var row = Database.Shared.AddBookmark(path, title, Bookmarks.Count, blockIndex, fingerprint);
        if (row == null) return null;
        var b = new Bookmark(row.Id, row.Path, row.Title, row.SortOrder, row.CreatedAt,
            row.BlockIndex, row.BlockFingerprint, File.Exists(row.Path),
            Bookmarks.Count < MaxSlots ? Bookmarks.Count + 1 : null);
        Bookmarks.Add(b);
        return b;
    }

    public void Remove(long id)
    {
        Database.Shared.RemoveBookmark(id);
        for (int i = Bookmarks.Count - 1; i >= 0; i--)
            if (Bookmarks[i].Id == id) Bookmarks.RemoveAt(i);
        RenormalizeSortOrders();
    }

    public void MoveBookmark(long id, int destination)
    {
        int from = -1;
        for (int i = 0; i < Bookmarks.Count; i++)
            if (Bookmarks[i].Id == id) { from = i; break; }
        if (from < 0) return;
        int clamped = Math.Max(0, Math.Min(destination, Bookmarks.Count - 1));
        if (from == clamped) return;
        var item = Bookmarks[from];
        Bookmarks.RemoveAt(from);
        Bookmarks.Insert(clamped, item);
        RenormalizeSortOrders();
    }

    public void MoveBookmarkToEnd(long id) => MoveBookmark(id, Bookmarks.Count - 1);
    public void MoveBookmarkToStart(long id) => MoveBookmark(id, 0);

    public void MoveBookmarkUp(long id)
    {
        int from = -1;
        for (int i = 0; i < Bookmarks.Count; i++)
            if (Bookmarks[i].Id == id) { from = i; break; }
        if (from > 0) MoveBookmark(id, from - 1);
    }

    public void MoveBookmarkDown(long id)
    {
        int from = -1;
        for (int i = 0; i < Bookmarks.Count; i++)
            if (Bookmarks[i].Id == id) { from = i; break; }
        if (from >= 0 && from < Bookmarks.Count - 1) MoveBookmark(id, from + 1);
    }

    /// 1-based slot number (1..MaxSlots) if the bookmark sits in a hotkey slot, else null.
    public int? SlotIndex(long bookmarkId)
    {
        for (int i = 0; i < Bookmarks.Count; i++)
            if (Bookmarks[i].Id == bookmarkId)
                return i < MaxSlots ? i + 1 : null;
        return null;
    }

    /// Look up the bookmark in slot `n` (1-based).
    public Bookmark? BookmarkForSlot(int n)
    {
        int i = n - 1;
        if (i < 0 || i >= Math.Min(MaxSlots, Bookmarks.Count)) return null;
        return Bookmarks[i];
    }

    private void RenormalizeSortOrders()
    {
        for (int i = 0; i < Bookmarks.Count; i++)
        {
            Bookmarks[i].SortOrder = i;
            Bookmarks[i].Slot = i < MaxSlots ? i + 1 : null;
        }
        Database.Shared.SetBookmarkOrder(Bookmarks.Select(b => b.Id).ToList());
    }

    /// Toggle a bookmark at the given anchor: if a bookmark exists at the
    /// same (path, blockIndex), remove it. Otherwise add. Mirrors the
    /// macOS app's Cmd+D behavior.
    public Bookmark? Toggle(string path, string title, int blockIndex, string fingerprint)
    {
        var existing = Bookmarks.FirstOrDefault(b => b.Path == path && b.BlockIndex == blockIndex);
        if (existing != null) { Remove(existing.Id); return null; }
        return Add(path, title, blockIndex, fingerprint);
    }
}
