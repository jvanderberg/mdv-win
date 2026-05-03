# mdv-win

A Windows-native Markdown viewer, building toward feature parity with the macOS [`mdv`](../mdv).

Built on **WinUI 3 + .NET 8**, packaged as MSIX. The macOS app is SwiftUI/AppKit; this is a separate codebase, not a port — only the SQLite schema and the theme palette values cross over verbatim.

## Status

Scaffold only. Data layer, theme catalog, and smart-typography rules are ported. UI shell, markdown rendering, search, bookmarks UI, and zoom are stubs.

## Prerequisites

```powershell
winget install Microsoft.DotNet.SDK.8
winget install Microsoft.VisualStudio.2022.Community --override "--add Microsoft.VisualStudio.Workload.NativeDesktop Microsoft.VisualStudio.Workload.ManagedDesktop Microsoft.VisualStudio.ComponentGroup.WindowsAppSDK.Cs"
```

Visual Studio 2022 isn't strictly required — `dotnet build` works once the SDK + WindowsAppSDK templates are installed — but the XAML designer and MSIX packaging UI are much smoother in VS.

## Build

```powershell
dotnet restore mdv-win.sln
dotnet build mdv-win.sln -c Debug
```

To run unpackaged for fast iteration during development, set `WindowsPackageType` to `None` in `mdv/mdv.csproj` (currently `MSIX`). The `.md` file association and `mdv.exe` PATH alias only work when installed as MSIX.

## Feature parity checklist

Tracking against macOS `mdv` (commit `7c77ab2` and earlier):

### Done
- [x] SQLite schema (articles + FTS5 + bookmarks + scroll_positions, schema_version 4)
- [x] HistoryManager (in-memory list, JSON persistence, mtime-aware reindex)
- [x] BookmarksManager (5 hotkey slots, drag-reorder, fingerprint anchor)
- [x] Theme catalog (9 themes + System sentinel, full color + typography port)
- [x] Code palettes (10 syntax-highlight palettes)
- [x] Smart typography rules (curly quotes, em/en-dash, ellipsis, code-aware)
- [x] Zoom engine (step 0.10, [0.60, 2.50], snap to 0.1)

### Shell + nav
- [ ] MainWindow with `NavigationView` (history left sidebar)
- [ ] Right inspector pane (TOC + bookmarks)
- [ ] MenuBar with all keyboard accelerators (mac `⌘` → Windows `Ctrl`)
- [ ] Sidebar collapse (`Ctrl+Alt+S`)
- [ ] Browser-style back/forward (`Alt+←`, `Alt+→`) tracking file + scroll position
- [ ] Drag-drop file open
- [ ] Open file (`Ctrl+O`), open in new window (`Ctrl+Shift+O`)
- [ ] FileWatcher live-reload on external edit
- [ ] Per-file scroll-position persistence

### Markdown rendering
- [ ] Markdig pipeline → WinUI render (start with `CommunityToolkit.Labs.WinUI.MarkdownTextBlock`, custom renderer if needed)
- [ ] Per-theme typography applied (bundled fonts: Alegreya, Besley, OpenDyslexic)
- [ ] Code-block chrome: language label, hover Copy / Wrap, "Copy Without Prompts" for shell
- [ ] Tree-sitter equivalent → `TextMateSharp` for syntax highlighting
- [ ] Local image rendering, remote-image blocking by default
- [ ] Smart typography toggle (View menu)
- [ ] Block-hover bookmark anchor stripe

### Search
- [ ] In-document find (`Ctrl+F`) with match navigation + highlight
- [ ] Global FTS5 history search (`Ctrl+Shift+F`) with snippets

### Bookmarks UI
- [ ] Bookmark current spot (`Ctrl+D`)
- [ ] Set placeholder (`Ctrl+Shift+0`), jump to placeholder (`Ctrl+0`)
- [ ] Slot hotkeys (`Ctrl+1` … `Ctrl+5`)
- [ ] Right-click reorder + drag-reorder

### Theme + zoom
- [ ] Theme picker (toolbar pop-up)
- [ ] System theme follows OS appearance (KVO equivalent: `UISettings.ColorValuesChanged`)
- [ ] Zoom in/out/reset (`Ctrl+=`, `Ctrl+-`) with HUD overlay
- [ ] Bundled `.otf` fonts loaded as `FontFamily` resources

### External editor
- [ ] Edit current file (`Ctrl+E`)
- [ ] Choose / forget editor (file picker)

### Packaging
- [ ] `.md` / `.markdown` / `.mdown` / `.mkd` file-type association
- [ ] `mdv.exe` App Execution Alias (PATH shim)
- [ ] App icon + Store tiles
- [ ] In-app help (`Ctrl+?` opens bundled `Help.md`)

## Project layout

```
mdv-win/
├── mdv-win.sln
├── mdv/
│   ├── mdv.csproj
│   ├── app.manifest
│   ├── Package.appxmanifest
│   ├── App.xaml(.cs)
│   ├── MainWindow.xaml(.cs)
│   ├── Assets/                  (icons, tiles — TODO)
│   ├── Fonts/                   (Alegreya / Besley / OpenDyslexic .otf — TODO copy from ../mdv/mdv/Fonts)
│   ├── Themes/Generic.xaml      (XAML resource dictionary — TODO)
│   ├── Views/                   (sidebars, document view — TODO)
│   └── Services/
│       ├── Database.cs          (SQLite, FTS5, bookmarks, scroll positions)
│       ├── HistoryManager.cs
│       ├── BookmarksManager.cs
│       ├── Themes.cs            (MdvTheme, CodePalette, ThemeManager, 9 themes)
│       └── SmartTypography.cs   (port of smartenMarkdown)
```

## Reference

The macOS implementation lives at [`../mdv`](../mdv). When in doubt about behavior, that codebase is authoritative. Commit history for the porting target is `git log` in that repo.
