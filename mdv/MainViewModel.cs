using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Mdv.Services;
using Windows.Storage.Pickers;
using WinRT.Interop;

namespace Mdv;

public sealed partial class MainViewModel : ObservableObject
{
    public HistoryManager History { get; } = new();
    public BookmarksManager Bookmarks { get; } = new();
    public ThemeManager Themes { get; } = new();

    public ObservableCollection<TocHeading> TocHeadings { get; } = new();
    public ObservableCollection<SearchHit> GlobalSearchHits { get; } = new();

    [ObservableProperty] private HistoryEntry? _selectedEntry;
    [ObservableProperty] private string _rawMarkdown = "";
    [ObservableProperty] private bool _sidebarCollapsed;
    [ObservableProperty] private bool _inspectorVisible;
    [ObservableProperty] private string _windowTitle = "mdv";

    [ObservableProperty] private bool _zoomHudVisible;
    [ObservableProperty] private string _zoomHudText = "100%";

    [ObservableProperty] private bool _globalSearchVisible;
    [ObservableProperty] private string _globalSearchQuery = "";
    private int _searchToken;

    partial void OnGlobalSearchQueryChanged(string value) => _ = RunGlobalSearchAsync(value);

    private async Task RunGlobalSearchAsync(string query)
    {
        var token = ++_searchToken;
        if (string.IsNullOrWhiteSpace(query))
        {
            GlobalSearchHits.Clear();
            return;
        }
        var hits = await Database.Shared.SearchAsync(query);
        if (token != _searchToken) return;
        GlobalSearchHits.Clear();
        foreach (var h in hits) GlobalSearchHits.Add(h);
    }

    public void OpenSearchHit(SearchHit hit)
    {
        var existing = History.Entries.FirstOrDefault(e => e.Path == hit.Path);
        if (existing != null) SelectedEntry = existing;
        else _ = LoadFileAsync(hit.Path);
    }

    [RelayCommand]
    private void FocusGlobalSearch()
    {
        if (SidebarCollapsed) SidebarCollapsed = false;
        GlobalSearchVisible = true;
    }

    [RelayCommand]
    private void CloseGlobalSearch()
    {
        GlobalSearchQuery = "";
        GlobalSearchHits.Clear();
        GlobalSearchVisible = false;
    }

    public IReadOnlyList<MdvTheme> AllThemes => MdvThemes.All;

    private List<string> _blocks = new();
    public IReadOnlyList<string> Blocks => _blocks;

    private readonly Stack<HistoryEntry> _backStack = new();
    private readonly Stack<HistoryEntry> _forwardStack = new();
    private bool _suppressNavStackPush;
    private HistoryEntry? _previousEntry;

    public bool CanGoBack => _backStack.Count > 0;
    public bool CanGoForward => _forwardStack.Count > 0;

    private DateTime _lastZoomChange;

    public MainViewModel()
    {
        _sidebarCollapsed = Settings.Get("mdv_sidebar_collapsed", false);
        _inspectorVisible = Settings.Get("mdv_inspector_visible", false);

        Themes.PropertyChanged += (_, e) =>
        {
            if (e.PropertyName == nameof(ThemeManager.FontScale)) ShowZoomHud();
        };

        if (History.Entries.Count > 0)
        {
            SelectedEntry = History.Entries[0];
            _ = LoadCurrentEntryAsync();
        }
    }

    partial void OnSelectedEntryChanged(HistoryEntry? value)
    {
        if (!_suppressNavStackPush
            && _previousEntry != null
            && _previousEntry.Path != value?.Path)
        {
            _backStack.Push(_previousEntry);
            _forwardStack.Clear();
            OnPropertyChanged(nameof(CanGoBack));
            OnPropertyChanged(nameof(CanGoForward));
        }
        _suppressNavStackPush = false;
        _previousEntry = value;
        _ = LoadCurrentEntryAsync();
    }

    [RelayCommand]
    private void GoBack()
    {
        if (_backStack.Count == 0 || SelectedEntry == null) return;
        _forwardStack.Push(SelectedEntry);
        var target = _backStack.Pop();
        _suppressNavStackPush = true;
        SelectedEntry = target;
        OnPropertyChanged(nameof(CanGoBack));
        OnPropertyChanged(nameof(CanGoForward));
    }

    [RelayCommand]
    private void GoForward()
    {
        if (_forwardStack.Count == 0 || SelectedEntry == null) return;
        _backStack.Push(SelectedEntry);
        var target = _forwardStack.Pop();
        _suppressNavStackPush = true;
        SelectedEntry = target;
        OnPropertyChanged(nameof(CanGoBack));
        OnPropertyChanged(nameof(CanGoForward));
    }

    partial void OnSidebarCollapsedChanged(bool value) => Settings.Set("mdv_sidebar_collapsed", value);
    partial void OnInspectorVisibleChanged(bool value) => Settings.Set("mdv_inspector_visible", value);

    private async Task LoadCurrentEntryAsync()
    {
        if (SelectedEntry == null)
        {
            RawMarkdown = "";
            _blocks.Clear();
            TocHeadings.Clear();
            WindowTitle = "mdv";
            return;
        }
        try
        {
            RawMarkdown = await File.ReadAllTextAsync(SelectedEntry.Path);
        }
        catch
        {
            RawMarkdown = $"# Couldn't read file\n\n`{SelectedEntry.Path}`";
        }
        _blocks = DocumentBlocks.Split(RawMarkdown);
        TocHeadings.Clear();
        foreach (var h in DocumentBlocks.ExtractToc(_blocks)) TocHeadings.Add(h);
        WindowTitle = SelectedEntry.Filename + " — mdv";
    }

    public async Task LoadFileAsync(string path)
    {
        if (!File.Exists(path)) return;
        var entry = History.Add(path);
        SelectedEntry = entry;
        await LoadCurrentEntryAsync();
    }

    public async Task LoadDirectoryAsync(string dirPath)
    {
        if (!Directory.Exists(dirPath)) return;
        var mdFiles = Directory.EnumerateFiles(dirPath, "*.md", SearchOption.TopDirectoryOnly)
            .Concat(Directory.EnumerateFiles(dirPath, "*.markdown", SearchOption.TopDirectoryOnly))
            .OrderBy(p => p, StringComparer.OrdinalIgnoreCase)
            .ToList();
        if (mdFiles.Count == 0) return;
        // Preserve directory order in history (Add inserts at top, so add in reverse).
        foreach (var p in mdFiles.AsEnumerable().Reverse()) History.Add(p);
        var landing = mdFiles.FirstOrDefault(p =>
            string.Equals(Path.GetFileName(p), "README.md", StringComparison.OrdinalIgnoreCase))
            ?? mdFiles[0];
        SelectedEntry = History.Entries.FirstOrDefault(e => e.Path == landing);
        await LoadCurrentEntryAsync();
    }

    [RelayCommand]
    private async Task OpenFile(IntPtr hwnd)
    {
        var picker = new FileOpenPicker { ViewMode = PickerViewMode.List };
        InitializeWithWindow.Initialize(picker, hwnd);
        picker.FileTypeFilter.Add(".md");
        picker.FileTypeFilter.Add(".markdown");
        picker.FileTypeFilter.Add(".mdown");
        picker.FileTypeFilter.Add(".mkd");
        picker.FileTypeFilter.Add(".txt");
        var file = await picker.PickSingleFileAsync();
        if (file != null) await LoadFileAsync(file.Path);
    }

    [RelayCommand]
    private void ToggleSidebar() => SidebarCollapsed = !SidebarCollapsed;

    [RelayCommand]
    private void ToggleInspector()
    {
        InspectorVisible = !InspectorVisible;
        if (InspectorVisible) Bookmarks.RefreshFileExistence();
    }

    [RelayCommand] private void ZoomIn() => Themes.ZoomIn();
    [RelayCommand] private void ZoomOut() => Themes.ZoomOut();
    [RelayCommand] private void ResetZoom() => Themes.ResetZoom();

    [RelayCommand]
    private void PickTheme(MdvTheme theme) => Themes.Set(theme);

    [RelayCommand]
    private void PickSystemTheme() => Themes.SetSelection(ThemeManager.SystemId);

    [RelayCommand]
    private void NavigateToBlock(int blockIndex)
    {
        // Wired up by MainWindow when scroll-into-view is implemented.
        BlockScrollRequested?.Invoke(blockIndex);
    }

    public event Action<int>? BlockScrollRequested;

    [RelayCommand]
    private void OpenBookmarkSlot(int n)
    {
        var b = Bookmarks.BookmarkForSlot(n);
        if (b != null) OpenBookmark(b);
    }

    [ObservableProperty] private long? _activeBookmarkId;

    public void OpenBookmark(Bookmark b)
    {
        _pendingScrollAnchor = (b.BlockIndex, b.BlockFingerprint);
        ActiveBookmarkId = b.Id;
        var existing = History.Entries.FirstOrDefault(e => e.Path == b.Path);
        if (existing != null && existing.Path == SelectedEntry?.Path)
        {
            BlockScrollRequested?.Invoke(b.BlockIndex);
            _pendingScrollAnchor = null;
        }
        else if (existing != null) SelectedEntry = existing;
        else _ = LoadFileAsync(b.Path);
    }

    public sealed record PlaceholderAnchor(string Path, string Title, int BlockIndex, string Fingerprint);
    private PlaceholderAnchor? _placeholder;
    public bool HasPlaceholder => _placeholder != null;
    private (int Index, string Fingerprint)? _pendingScrollAnchor;
    public (int Index, string Fingerprint)? ConsumePendingScrollAnchor()
    {
        var x = _pendingScrollAnchor;
        _pendingScrollAnchor = null;
        return x;
    }

    public void SetPlaceholder(HistoryEntry entry, int blockIndex, string blockSource, string title)
    {
        var fp = BookmarkAnchor.Fingerprint(blockSource);
        _placeholder = new PlaceholderAnchor(entry.Path, title, blockIndex, fp);
        OnPropertyChanged(nameof(HasPlaceholder));
    }

    public void JumpToPlaceholder()
    {
        if (_placeholder == null) return;
        _pendingScrollAnchor = (_placeholder.BlockIndex, _placeholder.Fingerprint);
        var entry = History.Entries.FirstOrDefault(e => e.Path == _placeholder.Path);
        if (entry != null) SelectedEntry = entry;
        else _ = LoadFileAsync(_placeholder.Path);
    }

    [RelayCommand]
    private void RemoveHistoryEntry(HistoryEntry entry)
    {
        var wasSelected = SelectedEntry?.Id == entry.Id;
        History.Remove(entry);
        if (wasSelected) SelectedEntry = History.Entries.FirstOrDefault();
    }

    private void ShowZoomHud()
    {
        var pct = (int)Math.Round(Themes.FontScale * 100);
        ZoomHudText = $"{pct}%";
        ZoomHudShowRequested?.Invoke();
        _lastZoomChange = DateTime.UtcNow;
        var stamp = _lastZoomChange;
        var dispatcher = Microsoft.UI.Dispatching.DispatcherQueue.GetForCurrentThread();
        _ = Task.Run(async () =>
        {
            await Task.Delay(900);
            if (stamp != _lastZoomChange) return;
            dispatcher?.TryEnqueue(() =>
            {
                if (stamp == _lastZoomChange) ZoomHudHideRequested?.Invoke();
            });
        });
    }

    public event Action? ZoomHudShowRequested;
    public event Action? ZoomHudHideRequested;
}
