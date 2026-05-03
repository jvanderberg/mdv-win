using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Mdv.Services;
using Microsoft.UI;
using Microsoft.UI.Windowing;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Input;
using Microsoft.UI.Xaml.Media;
using Windows.System;
using Windows.UI;
using WinRT.Interop;

namespace Mdv;

public sealed partial class MainWindow : Window
{
    public MainViewModel ViewModel { get; }

    private readonly IntPtr _hwnd;
    private readonly AppWindow _appWindow;

    public MainWindow()
    {
        ViewModel = new MainViewModel();
        InitializeComponent();
        _hwnd = WindowNative.GetWindowHandle(this);
        var windowId = Win32Interop.GetWindowIdFromWindow(_hwnd);
        _appWindow = AppWindow.GetFromWindowId(windowId);

        Title = "mdv";
        try { _appWindow.SetIcon(System.IO.Path.Combine(Windows.ApplicationModel.Package.Current.InstalledLocation.Path, "Assets", "mdv.ico")); }
        catch { try { _appWindow.SetIcon(System.IO.Path.Combine(System.AppContext.BaseDirectory, "Assets", "mdv.ico")); } catch { } }
        ExtendsContentIntoTitleBar = true;
        SetTitleBar(AppTitleBar);
        UpdateTitleBarInsets();
        _appWindow.Changed += (_, _) => UpdateTitleBarInsets();

        BuildThemeFlyout();
        BuildMenuThemeSubItem();
        ApplyLayoutFromViewModel();
        ApplyMenuVisibility();
        SyncMenuToggleStates();
        RenderDocument();
        ApplyThemeBackgrounds();
        RegisterOemAccelerators();
        ViewModel.Bookmarks.Bookmarks.CollectionChanged += (_, _) =>
        {
            RefreshMenuBookmarkSlots();
            UpdateBookmarksEmptyState();
            UpdateBookmarkButton();
        };
        RefreshMenuBookmarkSlots();
        UpdateBookmarksEmptyState();
        UpdateBookmarkButton();

        ViewModel.PropertyChanged += (_, e) =>
        {
            switch (e.PropertyName)
            {
                case nameof(MainViewModel.SidebarCollapsed):
                case nameof(MainViewModel.InspectorVisible):
                    ApplyLayoutFromViewModel();
                    break;
                case nameof(MainViewModel.WindowTitle):
                    Title = ViewModel.WindowTitle;
                    AppTitleText.Text = ViewModel.WindowTitle;
                    break;
                case nameof(MainViewModel.SelectedEntry):
                    UpdateBookmarkButton();
                    break;
                case nameof(MainViewModel.RawMarkdown):
                    RenderDocument();
                    var anchor = ViewModel.ConsumePendingScrollAnchor();
                    if (anchor.HasValue)
                    {
                        var resolved = BookmarkAnchor.Resolve(_blockSources, anchor.Value.Index, anchor.Value.Fingerprint);
                        DispatcherQueue.TryEnqueue(Microsoft.UI.Dispatching.DispatcherQueuePriority.Low,
                            () => ScrollToBlock(resolved));
                    }
                    break;
            }
        };
        ViewModel.Themes.PropertyChanged += (_, e) =>
        {
            if (e.PropertyName == nameof(ThemeManager.Current))
            {
                RenderDocument();
                ApplyThemeBackgrounds();
            }
            else if (e.PropertyName == nameof(ThemeManager.FontScale))
            {
                RenderDocument();
            }
        };
        ViewModel.BlockScrollRequested += ScrollToBlock;
    }

    private void RegisterOemAccelerators()
    {
        // VK_OEM_PLUS (187) = main-keyboard `=`/`+`; VK_OEM_MINUS (189) = main-keyboard `-`/`_`.
        // These aren't in the VirtualKey enum, so XAML can't reference them — register in code.
        var zoomIn = new KeyboardAccelerator
        {
            Key = (VirtualKey)187,
            Modifiers = VirtualKeyModifiers.Control,
        };
        zoomIn.Invoked += (_, e) => { ViewModel.ZoomInCommand.Execute(null); e.Handled = true; };
        RootGrid.KeyboardAccelerators.Add(zoomIn);

        var zoomOut = new KeyboardAccelerator
        {
            Key = (VirtualKey)189,
            Modifiers = VirtualKeyModifiers.Control,
        };
        zoomOut.Invoked += (_, e) => { ViewModel.ZoomOutCommand.Execute(null); e.Handled = true; };
        RootGrid.KeyboardAccelerators.Add(zoomOut);
    }

    private void UpdateTitleBarInsets()
    {
        // Reserve space at the right of the custom title bar for the system
        // caption buttons (close / min / max). Grows with display scale.
        var inset = _appWindow.TitleBar.RightInset;
        if (inset > 0) RightInsetColumn.Width = new GridLength(inset);
    }

    private void BuildThemeFlyout()
    {
        var system = new MenuFlyoutItem { Text = "System" };
        system.Click += (_, _) => ViewModel.PickSystemThemeCommand.Execute(null);
        ThemeFlyout.Items.Add(system);
        ThemeFlyout.Items.Add(new MenuFlyoutSeparator());
        foreach (var theme in MdvThemes.All)
        {
            var item = new MenuFlyoutItem { Text = theme.Name };
            item.Click += (_, _) => ViewModel.PickThemeCommand.Execute(theme);
            ThemeFlyout.Items.Add(item);
        }
    }

    private void BuildMenuThemeSubItem()
    {
        foreach (var theme in MdvThemes.All)
        {
            var item = new MenuFlyoutItem { Text = theme.Name };
            item.Click += (_, _) => ViewModel.PickThemeCommand.Execute(theme);
            MenuThemeSubItem.Items.Add(item);
        }
    }

    private void ApplyMenuVisibility()
    {
        var visible = Settings.Get("mdv_menu_visible", false);
        AppMenuBar.Visibility = visible ? Visibility.Visible : Visibility.Collapsed;
    }

    private void SyncMenuToggleStates()
    {
        MenuSmartTypoToggle.IsChecked = Settings.Get("mdv_smart_typography", true);
        MenuRemoteImagesToggle.IsChecked = Settings.Get("mdv_load_remote_images", false);
    }

    private MenuFlyoutItem[] _menuSlotItems = System.Array.Empty<MenuFlyoutItem>();

    private void RefreshMenuBookmarkSlots()
    {
        if (_menuSlotItems.Length == 0)
            _menuSlotItems = new[] { MenuSlot1, MenuSlot2, MenuSlot3, MenuSlot4, MenuSlot5 };
        for (int n = 1; n <= BookmarksManager.MaxSlots; n++)
        {
            var b = ViewModel.Bookmarks.BookmarkForSlot(n);
            _menuSlotItems[n - 1].Text = b == null ? $"{n}. Empty" : $"{n}.  {b.Title}";
            _menuSlotItems[n - 1].IsEnabled = b != null;
        }
    }

    private void ToggleMenu_Click(object sender, RoutedEventArgs e)
    {
        var visible = AppMenuBar.Visibility == Visibility.Visible;
        AppMenuBar.Visibility = visible ? Visibility.Collapsed : Visibility.Visible;
        Settings.Set("mdv_menu_visible", !visible);
    }

    // Menu-only handlers (toolbar buttons reuse the same handlers where they exist)
    private void MenuOpenInNewWindow_Click(object sender, RoutedEventArgs e) { var w = new MainWindow(); w.Activate(); }
    private void MenuExit_Click(object sender, RoutedEventArgs e) => Close();
    private void MenuFind_Click(object sender, RoutedEventArgs e) => OpenFindBar();
    private void MenuToggleSidebar_Click(object sender, RoutedEventArgs e) => ViewModel.ToggleSidebarCommand.Execute(null);
    private void MenuZoomIn_Click(object sender, RoutedEventArgs e) => ViewModel.ZoomInCommand.Execute(null);
    private void MenuZoomOut_Click(object sender, RoutedEventArgs e) => ViewModel.ZoomOutCommand.Execute(null);
    private void MenuResetZoom_Click(object sender, RoutedEventArgs e) => ViewModel.ResetZoomCommand.Execute(null);
    private void MenuSmartTypo_Click(object sender, RoutedEventArgs e)
    {
        Settings.Set("mdv_smart_typography", MenuSmartTypoToggle.IsChecked);
        RenderDocument();
    }
    private void MenuRemoteImages_Click(object sender, RoutedEventArgs e)
    {
        Settings.Set("mdv_load_remote_images", MenuRemoteImagesToggle.IsChecked);
        RenderDocument();
    }
    private void MenuPickSystemTheme_Click(object sender, RoutedEventArgs e) => ViewModel.PickSystemThemeCommand.Execute(null);
    private void MenuBack_Click(object sender, RoutedEventArgs e) => ViewModel.GoBackCommand.Execute(null);
    private void MenuForward_Click(object sender, RoutedEventArgs e) => ViewModel.GoForwardCommand.Execute(null);
    private async void MenuSetPlaceholder_Click(object sender, RoutedEventArgs e) => await SetPlaceholderInternalAsync();
    private void MenuJumpPlaceholder_Click(object sender, RoutedEventArgs e) => ViewModel.JumpToPlaceholder();
    private void MenuSlot1_Click(object sender, RoutedEventArgs e) => ViewModel.OpenBookmarkSlotCommand.Execute(1);
    private void MenuSlot2_Click(object sender, RoutedEventArgs e) => ViewModel.OpenBookmarkSlotCommand.Execute(2);
    private void MenuSlot3_Click(object sender, RoutedEventArgs e) => ViewModel.OpenBookmarkSlotCommand.Execute(3);
    private void MenuSlot4_Click(object sender, RoutedEventArgs e) => ViewModel.OpenBookmarkSlotCommand.Execute(4);
    private void MenuSlot5_Click(object sender, RoutedEventArgs e) => ViewModel.OpenBookmarkSlotCommand.Execute(5);

    // MARK: - Document rendering

    private bool _webViewReady;
    private string? _currentMappedHost;
    private List<string> _blockSources = new();
    private List<int> _findMatches = new();
    private int _findCurrent = -1;

    private async void RenderDocument()
    {
        try
        {
            await DocumentWebView.EnsureCoreWebView2Async();
            if (!_webViewReady)
            {
                DocumentWebView.CoreWebView2.WebMessageReceived += OnWebMessage;
                DocumentWebView.CoreWebView2.Settings.AreDevToolsEnabled = true;
                DocumentWebView.CoreWebView2.Settings.IsStatusBarEnabled = false;
                _webViewReady = true;
            }

            _findMatches.Clear();
            _findCurrent = -1;
            _blockSources.Clear();

            if (string.IsNullOrEmpty(ViewModel.RawMarkdown))
            {
                DocumentWebView.NavigateToString("<html><body style=\"display:flex;align-items:center;justify-content:center;height:100vh;color:#888;font-family:system-ui;\">Open a Markdown file (Ctrl+O)</body></html>");
                return;
            }

            var smartTypo = Settings.Get("mdv_smart_typography", true);
            var loadRemote = Settings.Get("mdv_load_remote_images", false);
            var baseDir = ViewModel.SelectedEntry != null
                ? Path.GetDirectoryName(ViewModel.SelectedEntry.Path)
                : null;

            if (_currentMappedHost != null)
                DocumentWebView.CoreWebView2.ClearVirtualHostNameToFolderMapping("mdv-doc.local");
            if (!string.IsNullOrEmpty(baseDir) && Directory.Exists(baseDir))
            {
                DocumentWebView.CoreWebView2.SetVirtualHostNameToFolderMapping(
                    "mdv-doc.local",
                    baseDir,
                    Microsoft.Web.WebView2.Core.CoreWebView2HostResourceAccessKind.Allow);
                _currentMappedHost = baseDir;
            }

            var renderer = new HtmlRenderer(
                ViewModel.Themes.Current,
                ViewModel.Themes.FontScale,
                smartTypo,
                loadRemote);

            _blockSources = DocumentBlocks.Split(ViewModel.RawMarkdown);
            var html = renderer.Render(ViewModel.RawMarkdown);
            DocumentWebView.NavigateToString(html);
        }
        catch { }
    }

    private async void OnWebMessage(Microsoft.Web.WebView2.Core.CoreWebView2 sender, Microsoft.Web.WebView2.Core.CoreWebView2WebMessageReceivedEventArgs e)
    {
        try
        {
            var json = e.WebMessageAsJson;
            using var doc = System.Text.Json.JsonDocument.Parse(json);
            var type = doc.RootElement.GetProperty("type").GetString();
            if (type == "link")
            {
                var href = doc.RootElement.GetProperty("href").GetString();
                if (!string.IsNullOrEmpty(href)) await HandleLinkAsync(href);
            }
            else if (type == "shortcut")
            {
                var action = doc.RootElement.GetProperty("action").GetString();
                DispatcherQueue.TryEnqueue(() => InvokeShortcut(action));
            }
        }
        catch { }
    }

    private async void InvokeShortcut(string? action)
    {
        switch (action)
        {
            case "find": OpenFindBar(); break;
            case "searchHistory": OpenSidebarSearch_Click(this, new RoutedEventArgs()); break;
            case "bookmarkHere": await BookmarkHereInternalAsync(); break;
            case "toggleSidebar": ViewModel.ToggleSidebarCommand.Execute(null); break;
            case "toggleInspector": ViewModel.ToggleInspectorCommand.Execute(null); break;
            case "zoomIn": ViewModel.ZoomInCommand.Execute(null); break;
            case "zoomOut": ViewModel.ZoomOutCommand.Execute(null); break;
            case "jumpPlaceholder": ViewModel.JumpToPlaceholder(); break;
            case "setPlaceholder": await SetPlaceholderInternalAsync(); break;
            case "slot1": ViewModel.OpenBookmarkSlotCommand.Execute(1); break;
            case "slot2": ViewModel.OpenBookmarkSlotCommand.Execute(2); break;
            case "slot3": ViewModel.OpenBookmarkSlotCommand.Execute(3); break;
            case "slot4": ViewModel.OpenBookmarkSlotCommand.Execute(4); break;
            case "slot5": ViewModel.OpenBookmarkSlotCommand.Execute(5); break;
            case "back": ViewModel.GoBackCommand.Execute(null); break;
            case "forward": ViewModel.GoForwardCommand.Execute(null); break;
            case "openFile": await ViewModel.OpenFileCommand.ExecuteAsync(_hwnd); break;
            case "openInNewWindow": var w = new MainWindow(); w.Activate(); break;
            case "editCurrent": await EditCurrentAsync(); break;
        }
    }

    private async System.Threading.Tasks.Task BookmarkHereInternalAsync()
    {
        if (ViewModel.SelectedEntry == null) return;
        int idx = await TopVisibleBlockIndexAsync();
        var src = idx >= 0 && idx < _blockSources.Count ? _blockSources[idx] : "";
        var title = BookmarkTitleFor(src);
        var fp = BookmarkAnchor.Fingerprint(src);
        // Toggle: if a bookmark already anchors this exact spot, Ctrl+D removes it.
        var added = ViewModel.Bookmarks.Toggle(ViewModel.SelectedEntry.Path, title, idx, fp);
        if (added != null && !ViewModel.InspectorVisible)
            ViewModel.ToggleInspectorCommand.Execute(null);
        UpdateBookmarkButton();
    }

    private async System.Threading.Tasks.Task SetPlaceholderInternalAsync()
    {
        if (ViewModel.SelectedEntry == null) return;
        int idx = await TopVisibleBlockIndexAsync();
        var src = idx >= 0 && idx < _blockSources.Count ? _blockSources[idx] : "";
        ViewModel.SetPlaceholder(ViewModel.SelectedEntry, idx, src, BookmarkTitleFor(src));
    }

    private async System.Threading.Tasks.Task HandleLinkAsync(string href)
    {
        if (Uri.TryCreate(href, UriKind.Absolute, out var uri))
        {
            if (uri.Scheme == "https" && uri.Host == "mdv-doc.local")
            {
                var rel = Uri.UnescapeDataString(uri.AbsolutePath.TrimStart('/'));
                var baseDir = ViewModel.SelectedEntry != null
                    ? Path.GetDirectoryName(ViewModel.SelectedEntry.Path)
                    : null;
                if (baseDir != null)
                {
                    var resolved = Path.GetFullPath(Path.Combine(baseDir, rel));
                    if (resolved.EndsWith(".md", StringComparison.OrdinalIgnoreCase) && File.Exists(resolved))
                    {
                        await ViewModel.LoadFileAsync(resolved);
                        return;
                    }
                }
            }
            if (uri.Scheme == "http" || uri.Scheme == "https" || uri.Scheme == "mailto")
                await Windows.System.Launcher.LaunchUriAsync(uri);
        }
    }

    private async void ScrollToBlock(int blockIndex)
    {
        if (!_webViewReady) return;
        await DocumentWebView.CoreWebView2.ExecuteScriptAsync($"window.mdvScrollToBlock({blockIndex});");
    }

    private void ApplyThemeBackgrounds()
    {
        var theme = ViewModel.Themes.Current;
        DocumentPanel.Background = new SolidColorBrush(theme.Background);
        SidebarPanel.Background = new SolidColorBrush(theme.SecondaryBackground);
        InspectorPanel.Background = new SolidColorBrush(theme.SecondaryBackground);
        AppTitleBar.Background = new SolidColorBrush(theme.Background);
        AppTitleText.Foreground = new SolidColorBrush(theme.Text);
        RootGrid.RequestedTheme = theme.IsDark ? ElementTheme.Dark : ElementTheme.Light;
    }

    private void ApplyLayoutFromViewModel()
    {
        SidebarColumn.Width = ViewModel.SidebarCollapsed ? new GridLength(0) : new GridLength(240);
        InspectorColumn.Width = ViewModel.InspectorVisible ? new GridLength(240) : new GridLength(0);
    }

    // MARK: - Toolbar button handlers

    private async void OpenFile_Click(object sender, RoutedEventArgs e) =>
        await ViewModel.OpenFileCommand.ExecuteAsync(_hwnd);

    private async void EditCurrent_Click(object sender, RoutedEventArgs e) => await EditCurrentAsync();

    private async System.Threading.Tasks.Task EditCurrentAsync()
    {
        if (ViewModel.SelectedEntry == null) return;
        var editorPath = Settings.Get<string>("mdv_editor_app_path", null);
        if (string.IsNullOrEmpty(editorPath) || !File.Exists(editorPath))
        {
            editorPath = await PickEditorAsync();
            if (string.IsNullOrEmpty(editorPath)) return;
            Settings.Set("mdv_editor_app_path", editorPath);
        }
        try
        {
            System.Diagnostics.Process.Start(new System.Diagnostics.ProcessStartInfo
            {
                FileName = editorPath,
                Arguments = $"\"{ViewModel.SelectedEntry.Path}\"",
                UseShellExecute = false,
            });
        }
        catch (System.Exception ex)
        {
            await ShowErrorAsync("Couldn't open in editor", ex.Message);
        }
    }

    private async System.Threading.Tasks.Task<string?> PickEditorAsync()
    {
        var picker = new Windows.Storage.Pickers.FileOpenPicker { ViewMode = Windows.Storage.Pickers.PickerViewMode.List };
        InitializeWithWindow.Initialize(picker, _hwnd);
        picker.FileTypeFilter.Add(".exe");
        picker.SuggestedStartLocation = Windows.Storage.Pickers.PickerLocationId.ComputerFolder;
        var file = await picker.PickSingleFileAsync();
        return file?.Path;
    }

    private async System.Threading.Tasks.Task ShowErrorAsync(string title, string message)
    {
        var dialog = new ContentDialog
        {
            Title = title, Content = message, CloseButtonText = "OK",
            XamlRoot = RootGrid.XamlRoot,
        };
        await dialog.ShowAsync();
    }

    private async void BookmarkHere_Click(object sender, RoutedEventArgs e) =>
        await BookmarkHereInternalAsync();

    private async void ChooseEditor_Click(object sender, RoutedEventArgs e)
    {
        var path = await PickEditorAsync();
        if (!string.IsNullOrEmpty(path)) Settings.Set("mdv_editor_app_path", path);
    }

    private void ForgetEditor_Click(object sender, RoutedEventArgs e) =>
        Settings.Set<string>("mdv_editor_app_path", "");

    private void ToggleInspector_Click(object sender, RoutedEventArgs e) =>
        ViewModel.ToggleInspectorCommand.Execute(null);

    private void OpenSidebarSearch_Click(object sender, RoutedEventArgs e)
    {
        ViewModel.FocusGlobalSearchCommand.Execute(null);
        DispatcherQueue.TryEnqueue(() => GlobalSearchBox.Focus(FocusState.Keyboard));
    }

    private void CloseSidebarSearch_Click(object sender, RoutedEventArgs e) =>
        ViewModel.CloseGlobalSearchCommand.Execute(null);

    private void GlobalSearchList_ItemClick(object sender, ItemClickEventArgs e)
    {
        if (e.ClickedItem is SearchHit hit) ViewModel.OpenSearchHit(hit);
    }

    private void BookmarksList_ItemClick(object sender, ItemClickEventArgs e)
    {
        if (e.ClickedItem is Bookmark b) ViewModel.OpenBookmark(b);
    }

    private void UpdateBookmarksEmptyState()
    {
        var empty = ViewModel.Bookmarks.Bookmarks.Count == 0;
        BookmarksEmpty.Visibility = empty ? Visibility.Visible : Visibility.Collapsed;
        BookmarksList.Visibility = empty ? Visibility.Collapsed : Visibility.Visible;
    }

    private void UpdateBookmarkButton()
    {
        var has = ViewModel.Bookmarks.HasAnyBookmarkForPath(ViewModel.SelectedEntry?.Path);
        // E8A4 = Bookmark, EB8F = SolidStar (filled bookmark not in standard set; use Pinned EB52)
        BookmarkBtnIcon.Glyph = has ? "" : "";
    }

    private static Bookmark? BookmarkOf(object sender) =>
        (sender as FrameworkElement)?.DataContext as Bookmark;

    private void BookmarkMenu_Open_Click(object sender, RoutedEventArgs e)
    {
        if (BookmarkOf(sender) is { } b) ViewModel.OpenBookmark(b);
    }

    private void BookmarkMenu_Reveal_Click(object sender, RoutedEventArgs e)
    {
        if (BookmarkOf(sender) is not { } b) return;
        if (!File.Exists(b.Path)) return;
        try
        {
            System.Diagnostics.Process.Start(new System.Diagnostics.ProcessStartInfo
            {
                FileName = "explorer.exe",
                Arguments = $"/select,\"{b.Path}\"",
                UseShellExecute = false,
            });
        }
        catch { }
    }

    private void BookmarkMenu_MoveUp_Click(object sender, RoutedEventArgs e)
    {
        if (BookmarkOf(sender) is { } b) ViewModel.Bookmarks.MoveBookmarkUp(b.Id);
    }

    private void BookmarkMenu_MoveDown_Click(object sender, RoutedEventArgs e)
    {
        if (BookmarkOf(sender) is { } b) ViewModel.Bookmarks.MoveBookmarkDown(b.Id);
    }

    private void BookmarkMenu_MoveTop_Click(object sender, RoutedEventArgs e)
    {
        if (BookmarkOf(sender) is { } b) ViewModel.Bookmarks.MoveBookmarkToStart(b.Id);
    }

    private void BookmarkMenu_MoveBottom_Click(object sender, RoutedEventArgs e)
    {
        if (BookmarkOf(sender) is { } b) ViewModel.Bookmarks.MoveBookmarkToEnd(b.Id);
    }

    private void BookmarkMenu_Remove_Click(object sender, RoutedEventArgs e)
    {
        if (BookmarkOf(sender) is { } b) ViewModel.Bookmarks.Remove(b.Id);
    }

    private void TocList_SelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        if (TocList.SelectedItem is TocHeading h) ViewModel.NavigateToBlockCommand.Execute(h.BlockIndex);
    }

    // MARK: - Find bar

    private void OpenFindBar()
    {
        FindBar.Visibility = Visibility.Visible;
        DispatcherQueue.TryEnqueue(() => FindBox.Focus(FocusState.Keyboard));
    }

    private void FindClose_Click(object sender, RoutedEventArgs e) => CloseFindBar();

    private void CloseFindBar()
    {
        FindBox.Text = "";
        ClearFindHighlights();
        FindBar.Visibility = Visibility.Collapsed;
    }

    private void FindBox_TextChanged(object sender, TextChangedEventArgs e) => UpdateFindMatches();

    private void FindBox_KeyDown(object sender, KeyRoutedEventArgs e)
    {
        if (e.Key == VirtualKey.Escape) { CloseFindBar(); e.Handled = true; }
        else if (e.Key == VirtualKey.Enter)
        {
            var shift = Microsoft.UI.Input.InputKeyboardSource.GetKeyStateForCurrentThread(VirtualKey.Shift);
            StepFind((shift & Windows.UI.Core.CoreVirtualKeyStates.Down) == Windows.UI.Core.CoreVirtualKeyStates.Down ? -1 : +1);
            e.Handled = true;
        }
    }

    private void FindNext_Click(object sender, RoutedEventArgs e) => StepFind(+1);
    private void FindPrev_Click(object sender, RoutedEventArgs e) => StepFind(-1);

    private async void UpdateFindMatches()
    {
        if (!_webViewReady) return;
        var query = FindBox.Text ?? "";
        var escaped = System.Text.Json.JsonSerializer.Serialize(query);
        var json = await DocumentWebView.CoreWebView2.ExecuteScriptAsync($"JSON.stringify(window.mdvFind({escaped}))");
        if (string.IsNullOrEmpty(json) || json == "null") { FindCounter.Text = ""; return; }
        var unwrapped = System.Text.Json.JsonSerializer.Deserialize<string>(json);
        using var doc = System.Text.Json.JsonDocument.Parse(unwrapped!);
        _findMatches.Clear();
        foreach (var m in doc.RootElement.GetProperty("matches").EnumerateArray())
            _findMatches.Add(m.GetInt32());
        if (_findMatches.Count == 0)
        {
            FindCounter.Text = string.IsNullOrEmpty(query) ? "" : "0 matches";
            _findCurrent = -1;
            return;
        }
        _findCurrent = 0;
        await DocumentWebView.CoreWebView2.ExecuteScriptAsync($"window.mdvFindCurrent({_findMatches[0]})");
        FindCounter.Text = $"1 of {_findMatches.Count}";
    }

    private async void StepFind(int delta)
    {
        if (_findMatches.Count == 0 || !_webViewReady) return;
        _findCurrent = ((_findCurrent + delta) % _findMatches.Count + _findMatches.Count) % _findMatches.Count;
        await DocumentWebView.CoreWebView2.ExecuteScriptAsync($"window.mdvFindCurrent({_findMatches[_findCurrent]})");
        FindCounter.Text = $"{_findCurrent + 1} of {_findMatches.Count}";
    }

    private async void ClearFindHighlights()
    {
        if (!_webViewReady) return;
        await DocumentWebView.CoreWebView2.ExecuteScriptAsync("window.mdvFindClear()");
        _findMatches.Clear();
        _findCurrent = -1;
        FindCounter.Text = "";
    }

    private async System.Threading.Tasks.Task<int> TopVisibleBlockIndexAsync()
    {
        if (!_webViewReady) return 0;
        const string script = @"
            (function() {
              var blocks = document.querySelectorAll('.block');
              for (var i = 0; i < blocks.length; i++) {
                var r = blocks[i].getBoundingClientRect();
                if (r.bottom > 8) return i;
              }
              return 0;
            })()";
        var json = await DocumentWebView.CoreWebView2.ExecuteScriptAsync(script);
        return int.TryParse(json, out var i) ? i : 0;
    }

    private string BookmarkTitleFor(string blockSource)
    {
        var trimmed = blockSource.TrimStart();
        if (trimmed.StartsWith("# ")) return DocumentBlocks.StripInlineMarkdown(trimmed[2..].Split('\n')[0]);
        if (trimmed.StartsWith("## ")) return DocumentBlocks.StripInlineMarkdown(trimmed[3..].Split('\n')[0]);
        if (trimmed.StartsWith("### ")) return DocumentBlocks.StripInlineMarkdown(trimmed[4..].Split('\n')[0]);
        var collapsed = string.Join(' ', trimmed.Split(new[] { ' ', '\t', '\n', '\r' }, StringSplitOptions.RemoveEmptyEntries));
        return collapsed.Length <= 60 ? collapsed : collapsed[..60] + "…";
    }

    // MARK: - Keyboard accelerator handlers

    private async void Accel_OpenFile(KeyboardAccelerator s, KeyboardAcceleratorInvokedEventArgs e)
        { await ViewModel.OpenFileCommand.ExecuteAsync(_hwnd); e.Handled = true; }
    private void Accel_OpenInNewWindow(KeyboardAccelerator s, KeyboardAcceleratorInvokedEventArgs e)
        { var w = new MainWindow(); w.Activate(); e.Handled = true; }
    private async void Accel_EditCurrent(KeyboardAccelerator s, KeyboardAcceleratorInvokedEventArgs e)
        { await EditCurrentAsync(); e.Handled = true; }
    private void Accel_Find(KeyboardAccelerator s, KeyboardAcceleratorInvokedEventArgs e) { OpenFindBar(); e.Handled = true; }
    private void Accel_SearchHistory(KeyboardAccelerator s, KeyboardAcceleratorInvokedEventArgs e)
        { OpenSidebarSearch_Click(this, new RoutedEventArgs()); e.Handled = true; }
    private void Accel_ToggleSidebar(KeyboardAccelerator s, KeyboardAcceleratorInvokedEventArgs e)
        { ViewModel.ToggleSidebarCommand.Execute(null); e.Handled = true; }
    private void Accel_ToggleInspector(KeyboardAccelerator s, KeyboardAcceleratorInvokedEventArgs e)
        { ViewModel.ToggleInspectorCommand.Execute(null); e.Handled = true; }
    private void Accel_ZoomIn(KeyboardAccelerator s, KeyboardAcceleratorInvokedEventArgs e)
        { ViewModel.ZoomInCommand.Execute(null); e.Handled = true; }
    private void Accel_ZoomOut(KeyboardAccelerator s, KeyboardAcceleratorInvokedEventArgs e)
        { ViewModel.ZoomOutCommand.Execute(null); e.Handled = true; }
    private void Accel_BookmarkHere(KeyboardAccelerator s, KeyboardAcceleratorInvokedEventArgs e)
        { BookmarkHere_Click(this, new RoutedEventArgs()); e.Handled = true; }
    private async void Accel_SetPlaceholder(KeyboardAccelerator s, KeyboardAcceleratorInvokedEventArgs e)
    {
        if (ViewModel.SelectedEntry != null)
        {
            int idx = await TopVisibleBlockIndexAsync();
            var src = idx >= 0 && idx < _blockSources.Count ? _blockSources[idx] : "";
            ViewModel.SetPlaceholder(ViewModel.SelectedEntry, idx, src, BookmarkTitleFor(src));
        }
        e.Handled = true;
    }
    private void Accel_JumpPlaceholder(KeyboardAccelerator s, KeyboardAcceleratorInvokedEventArgs e)
        { ViewModel.JumpToPlaceholder(); e.Handled = true; }
    private void Accel_Slot1(KeyboardAccelerator s, KeyboardAcceleratorInvokedEventArgs e) { ViewModel.OpenBookmarkSlotCommand.Execute(1); e.Handled = true; }
    private void Accel_Slot2(KeyboardAccelerator s, KeyboardAcceleratorInvokedEventArgs e) { ViewModel.OpenBookmarkSlotCommand.Execute(2); e.Handled = true; }
    private void Accel_Slot3(KeyboardAccelerator s, KeyboardAcceleratorInvokedEventArgs e) { ViewModel.OpenBookmarkSlotCommand.Execute(3); e.Handled = true; }
    private void Accel_Slot4(KeyboardAccelerator s, KeyboardAcceleratorInvokedEventArgs e) { ViewModel.OpenBookmarkSlotCommand.Execute(4); e.Handled = true; }
    private void Accel_Slot5(KeyboardAccelerator s, KeyboardAcceleratorInvokedEventArgs e) { ViewModel.OpenBookmarkSlotCommand.Execute(5); e.Handled = true; }
    private void Accel_Back(KeyboardAccelerator s, KeyboardAcceleratorInvokedEventArgs e) { ViewModel.GoBackCommand.Execute(null); e.Handled = true; }
    private void Accel_Forward(KeyboardAccelerator s, KeyboardAcceleratorInvokedEventArgs e) { ViewModel.GoForwardCommand.Execute(null); e.Handled = true; }
}
