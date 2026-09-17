# Windows Port — Plan

> Port of **Mac On-Screen Chat** (SwiftUI + AppKit `NSPanel`) to Windows as
> **WinUI 3 / .NET**. Same product: a floating, always-on-top chat panel summoned
> by a global hotkey, driven by editable prompt "skills," hidden from screen
> sharing, persisted in SQLite.
>
> **One deliberate difference: no local models.** Ollama is dropped entirely;
> the Windows build is Gemini-only.

---

## 1. Decisions taken

| Decision | Choice | Why |
|---|---|---|
| UI stack | **WinUI 3 (Windows App SDK)** | Only stack where all three hard features map 1:1 to first-class OS APIs; ~native footprint, matching the Mac app's "not Electron" stance in [workflow.md](../workflow.md#2-tech-stack). |
| Language / runtime | **C# / .NET 10** | `dotnet --list-sdks` on this machine reports only `10.0.400`. Nothing in the plan is version-specific — retargeting to `net8.0-windows10.0.19041.0` is a one-line TFM change if you need LTS-8. |
| Models | **Gemini only** (2.5 Flash, 2.5 Pro) | Per the brief: the Windows module ships no local models. |
| Packaging | **Unpackaged, self-contained** | Mirrors the Mac app's unsigned local `.app`. No MSIX identity, no store, no signing. Self-contained so users don't need the Windows App SDK runtime pre-installed. |
| Min OS | **Windows 10 2004 (build 19041)** | `WDA_EXCLUDEFROMCAPTURE` — the whole hide-from-screen-share feature — does not exist before it. |

### Why not the alternatives

Electron would have been fastest and partly developable on Linux, but a ~150MB
bundle is the exact thing [workflow.md](../workflow.md) rejected. Tauri is a
reasonable middle ground but pushes the capture-exclusion call into `unsafe`
Rust against a raw `HWND` anyway — the part WinUI gets for free — while adding a
WebView2 dependency.

---

## 2. Feature parity matrix

Every Mac behavior, and what carries it on Windows.

| Behavior | macOS today | Windows equivalent | Risk |
|---|---|---|---|
| Always on top, over fullscreen | `NSPanel` `level = .floating`, `.fullScreenAuxiliary` | `OverlappedPresenter.IsAlwaysOnTop = true` (`HWND_TOPMOST`) | Low |
| No Dock/taskbar icon | `LSUIElement`, `.accessory` policy | `AppWindow.IsShownInSwitchers = false` | Low |
| Borderless, rounded, translucent | `titlebarAppearsTransparent`, SwiftUI `.ultraThinMaterial` | `SetBorderAndTitleBar(false, false)` + `DesktopAcrylicBackdrop` | Low |
| Drag from anywhere | `isMovableByWindowBackground` | `InputNonClientPointerSource.SetRegionRects(NonClientRegionKind.Caption, …)` | Med — must leave holes over the input box and buttons |
| Global hotkey | Carbon `RegisterEventHotKey`, ⌘⇧Space | Win32 `RegisterHotKey` + `WM_HOTKEY`, **Ctrl+Shift+Space** | Low — `Win+Space` is reserved by the IME switcher, so Ctrl is the right ⌘ analog |
| **Hidden from screen share** | `NSWindow.sharingType = .none` | `SetWindowDisplayAffinity(hwnd, WDA_EXCLUDEFROMCAPTURE)` | **Med — see §7** |
| Menu-bar icon | `NSStatusItem` | Tray `NotifyIcon` (`H.NotifyIcon.WinUI`) | Low |
| Esc hides | `cancelOperation` | `KeyboardAccelerator` on `Escape` | Low |
| Enter sends, Shift+Enter newline | `.onKeyPress(.return)` | `TextBox.PreviewKeyDown` | Low |
| Stop generating | ⌘. → `Task.cancel()` | **Ctrl+.** → `CancellationTokenSource` | Low |
| Token streaming | `AsyncStream<String>` | `IAsyncEnumerable<string>` + `await foreach` | Low |
| Copy a reply | `NSPasteboard` | `Windows.ApplicationModel.DataTransfer.Clipboard` | Low |
| Skills / rules / history | GRDB + SQLite | `Microsoft.Data.Sqlite` + hand-rolled migrator | Low |
| Settings | `UserDefaults` | JSON file, `%LOCALAPPDATA%\OnScreenChat\settings.json` | Low |
| Secrets | `.env` → App Support | `.env` → `%LOCALAPPDATA%\OnScreenChat\.env` | Low |
| Icons | SF Symbols | **Segoe Fluent Icons** glyphs | Low — needs a one-time name mapping table |
| Local Ollama models | `OllamaClient` | **Dropped** | — |

---

## 3. Source map: what ports to what

The Mac app is 2,391 lines across 25 files. Roughly two thirds is pure logic
that translates almost mechanically; the rest is platform glue that gets
rewritten against Win32/WinUI.

### Pure logic — near-mechanical port, lands in `OnScreenChat.Core`

| Mac file | Windows file | Notes |
|---|---|---|
| `ResponseStyle.swift` | `Text/ResponseStyle.cs` | `Normalized`, `Paragraphs`, `StripAsterisks` — straight translation |
| `ResponseStyle.swift` (`AsteriskStripper`) | `Text/AsteriskStripper.cs` | Chunk-spanning `**` filter; `struct` → `class`, same state machine |
| `Models.swift` (`Skill.defaults`) | `Data/SkillDefaults.cs` | **Copy the six prompt strings verbatim** — they are the product |
| `Models.swift` (records) | `Data/Skill.cs`, `Rule.cs`, `ChatMessage.cs` | POCOs |
| `DatingHeat.swift` | `Prompts/DatingHeat.cs` | `enum` + `PromptDirective` |
| `ChatViewModel.swift` (statics) | `Prompts/PromptBuilder.cs` | `SystemPrompt`, `ExtractCommands`, `ExtractDirective` — the `/command {…}` and `WW:{…}` regexes port unchanged to .NET `Regex` |
| `RuleStore.swift` (`promptBlock`) | `Data/RuleStore.cs` | Same precedence wording |
| `GeminiClient.swift` | `Llm/GeminiClient.cs` | Same endpoint, same SSE parse, same HTTP-status error copy |
| `LLMClient.swift` | `Llm/ILlmClient.cs`, `EchoClient.cs` | `EchoClient` kept — it's how the UI is testable with no key |
| `EnvLoader.swift` | `Config/EnvLoader.cs` | Lookup order: process env → `%LOCALAPPDATA%` → `.\.env` |
| `AppDatabase.swift` | `Data/AppDatabase.cs` | Schema identical; migrations re-expressed (§5) |
| `SelfTest.swift` | `OnScreenChat.Tests` + `--selftest` | Becomes real xUnit tests **plus** the CLI flag for parity |

### Platform glue — rewritten

| Mac file | Windows file | Notes |
|---|---|---|
| `main.swift`, `AppDelegate.swift` | `App.xaml.cs` | Single-instance guard (named `Mutex`) is new — Windows has no `LSMultipleInstancesProhibited` |
| `FloatingPanel.swift` | `Windowing/FloatingPanel.cs` | Presenter config + `SetWindowDisplayAffinity` |
| `PanelController.swift` | `Windowing/PanelController.cs` | Show/hide/toggle, frame persistence, off-screen recovery |
| `GlobalHotKey.swift` | `Interop/GlobalHotKey.cs` | `RegisterHotKey` + a message-only window to receive `WM_HOTKEY` |
| `MenuBarController.swift` | `Tray/TrayIconController.cs` | Same three items: Show/Hide, Hide from Screen Sharing (checkable), Quit |
| `ContentView.swift` | `Views/ChatPage.xaml(.cs)` | Header, transcript, heat bar, input bar |
| `MessageBubble.swift` | `Views/MessageBubble.xaml` | Paragraph-per-`TextBlock` in an `ItemsRepeater` |
| `SkillEditor`, `RulesEditor`, `DatingStyleEditor` | `Views/*Dialog.xaml` | `ContentDialog` each |
| `ChatViewModel.swift` (stateful half) | `ViewModels/ChatViewModel.cs` | `ObservableObject` + `ObservableCollection`; the `generation` counter guarding late replies ports as-is |
| `Log.swift` | `Diagnostics/Log.cs` | Appends to `%LOCALAPPDATA%\OnScreenChat\debug.log` |

---

## 4. Project layout

```
windows/
  OnScreenChat.sln
  Directory.Build.props                  # shared TFM / LangVersion / nullable
  build-app.ps1                          # ≙ build-app.sh: publish + copy .env
  .env.example
  README.md                              # Windows-specific run instructions

  src/
    OnScreenChat.Core/                   # net10.0 — NO Windows deps
      OnScreenChat.Core.csproj           #   ⇒ builds and tests on Linux too
      Config/EnvLoader.cs
      Data/{AppDatabase,Skill,Rule,ChatMessage,SkillDefaults,SkillStore,RuleStore}.cs
      Llm/{ILlmClient,GeminiClient,EchoClient,ModelOption}.cs
      Prompts/{PromptBuilder,DatingHeat}.cs
      Text/{ResponseStyle,AsteriskStripper}.cs
      Diagnostics/Log.cs

    OnScreenChat/                        # net10.0-windows10.0.19041.0 — WinUI 3 head
      OnScreenChat.csproj
      App.xaml(.cs)
      app.manifest                       # DPI awareness, supportedOS
      Assets/{tray.ico,app.ico}
      Interop/{NativeMethods,GlobalHotKey,CaptureAffinity,MessageWindow}.cs
      Windowing/{FloatingPanel,PanelController}.cs
      Tray/TrayIconController.cs
      Settings/SettingsStore.cs
      ViewModels/ChatViewModel.cs
      Views/{ChatPage,MessageBubble,SkillEditorDialog,RulesEditorDialog,DatingStyleDialog}.xaml(.cs)

    OnScreenChat.SelfTest/               # net10.0 console — the `--selftest` parity harness
                                         #   --selftest | --markers "<text>" | --gemini

  tests/
    OnScreenChat.Core.Tests/             # xUnit over the ported pure logic
```

**Why the `Core` split matters:** everything in `OnScreenChat.Core` is
platform-free, so it compiles and its tests run **in this Linux codespace**. The
`AsteriskStripper` state machine, the `/command {…}` extraction, the prompt
precedence order and the Gemini SSE parse — the parts where a porting bug is
silent and expensive — get verified before anyone touches a Windows machine.
Only the WinUI head needs Windows to build.

---

## 5. Data layer

Same file, same schema, same location semantics:

| | macOS | Windows |
|---|---|---|
| DB | `~/Library/Application Support/MacOnScreenChat/app.sqlite` | `%LOCALAPPDATA%\OnScreenChat\app.sqlite` |
| Log | `…/MacOnScreenChat/debug.log` | `%LOCALAPPDATA%\OnScreenChat\debug.log` |
| Secrets | `…/MacOnScreenChat/.env` | `%LOCALAPPDATA%\OnScreenChat\.env` |
| Settings | `UserDefaults` | `%LOCALAPPDATA%\OnScreenChat\settings.json` |

> Note: `ApplicationData.Current.LocalFolder` throws in an unpackaged app —
> use `Environment.GetFolderPath(SpecialFolder.LocalApplicationData)` throughout.

**Migrations.** GRDB's named-migration ledger becomes a `PRAGMA user_version`
counter with an ordered list of steps. Mac migrations `v1`–`v5` collapse into a
single `v1` here — a fresh Windows install has no legacy rows to repair, so
`v3.seedDatingSkill` and `v4.tuneDatingPrompt` (both retrofits for existing Mac
users) are folded into the initial seed. Numbering starts at 1 and grows from
there independently of the Mac side.

```
v1: CREATE TABLE skill(id TEXT PRIMARY KEY, name TEXT NOT NULL,
                       inputHint TEXT NOT NULL, systemPrompt TEXT NOT NULL,
                       sortOrder INTEGER NOT NULL)
    CREATE TABLE message(id TEXT PRIMARY KEY, role TEXT NOT NULL,
                         text TEXT NOT NULL, sortOrder INTEGER NOT NULL)
    CREATE TABLE rule(id TEXT PRIMARY KEY, text TEXT NOT NULL,
                      sortOrder INTEGER NOT NULL)
    + seed all six skills from SkillDefaults
```

**Settings keys** (JSON, one-for-one with the Mac `UserDefaults` keys):
`panelFrame`, `hideFromScreenShare` (default `true`), `selectedModelID`,
`datingHeat`, `datingStyle`.

---

## 6. Build phases

Each phase ends at something runnable. Phases 1–2 are verifiable here on Linux;
3 onward need a Windows box.

### Phase 1 — Core logic + tests *(Linux-verifiable)* ✅ **DONE**
Scaffold the solution. Port `ResponseStyle`, `AsteriskStripper`, `PromptBuilder`,
`DatingHeat`, `EnvLoader`, POCOs, `SkillDefaults`. Write the xUnit suite:
`**` stripping across chunk boundaries, lone-`*` passthrough, paragraph
splitting, blank-line collapsing, `/command {…}` + `WW:{…}` extraction and
removal, and the four-part system-prompt precedence order.
**Done when:** `dotnet test` is green in this codespace.
**Result:** 66 tests passing, zero build warnings. The six system prompts, the
formatting contract and the heat calibrations are **generated** from the Swift
sources by `windows/tools/sync-prompts.py` rather than transcribed — see risk #6.

### Phase 2 — Data + Gemini *(Linux-verifiable)* ✅ **DONE**
`AppDatabase` with the migrator and seed. `SkillStore`, `RuleStore`.
`ILlmClient`, `EchoClient`, `GeminiClient` (SSE, cancellation, the full
429/400/401/404 error-copy table). `ModelOption` with the two Gemini entries.
The `OnScreenChat.SelfTest` console reproduces `--selftest` and `--markers`.
**Done when:** `dotnet run --project OnScreenChat.SelfTest -- --selftest` seeds
and round-trips a DB, and a real key streams a Gemini reply to stdout.
**Result:** 142 tests passing, zero build warnings. `--selftest` and `--markers`
reproduce the Mac output; `--gemini` streams a live reply when a key is present.
The prompt generator also now covers the standing-rules and directive blocks, so
no prompt text in the Windows build is hand-transcribed.

### Phase 3 — The floating panel *(Windows)*
WinUI head, borderless topmost window, acrylic backdrop, no taskbar entry,
caption drag regions, frame save/restore with off-screen recovery, Esc to hide.
**Done when:** the panel floats over a maximized app and survives a restart in place.

### Phase 4 — Hotkey, tray, single instance *(Windows)*
`RegisterHotKey` on Ctrl+Shift+Space via a message-only window; tray icon with
the three menu items; named-`Mutex` single-instance guard that signals the
running instance to toggle instead of launching a second copy.
**Done when:** Ctrl+Shift+Space toggles from any app; tray Quit exits cleanly and
unregisters the hotkey.

### Phase 5 — Hide from screen share *(Windows)*
`SetWindowDisplayAffinity` with `WDA_EXCLUDEFROMCAPTURE`, wired to the tray
checkbox and persisted. Re-apply after any window recreation.
**Done when:** verified against the matrix in §7.

### Phase 6 — Chat UI *(Windows)*
Header (skill menu, model menu, new chat), transcript with paragraph bubbles +
hover-copy, empty state, auto-growing input, Enter/Shift+Enter, streaming into
the bubble, Ctrl+. to stop, the dating heat bar, and the three dialogs.
**Done when:** full flow works end-to-end against Gemini.

### Phase 7 — Packaging + docs
`build-app.ps1` (publish self-contained win-x64 + copy `.env` to
`%LOCALAPPDATA%`), `windows/README.md`, `.gitignore` additions
(`bin/`, `obj/`, `publish/`). Code signing stays deferred, as on Mac.

---

## 7. Risks and open questions

**1. `WDA_EXCLUDEFROMCAPTURE` is not as absolute as `sharingType = .none`.**
This is the one place I would not promise parity up front. The flag is reliably
honored by the Windows Graphics Capture path (Teams, Zoom, Slack, modern
browsers' `getDisplayMedia`, Xbox Game Bar). Coverage of the older **DXGI
Desktop Duplication** path — which some OBS configurations and remote-desktop
tools still use — is inconsistent in the wild. **Phase 5 must verify against a
real matrix** (Teams, Zoom, Meet in Chrome, OBS on both its capture backends,
Win+Shift+S, PrintScreen) and `windows/README.md` must state honestly whatever
that matrix shows, rather than inheriting [visibility.md](../visibility.md)'s
macOS claims. A capture method that defeats it is a documentation problem, not
a bug to hide.

**2. No local-model fallback.** On Mac, a missing/failed Gemini key degrades to
a private local model. Windows has nowhere to fall back to, which makes the
"no API key" path a first-class UX concern rather than an edge case: the empty
state should say the app needs a key *before* the user types a message, not
after. `EchoClient` stays wired in so the UI is still exercisable with no key.

**3. Drag regions vs. interactive controls.** Making the whole background
draggable is one line on macOS and fiddly on Windows — every button and the
text box needs to be punched out of the caption region, and the regions must be
recomputed on resize and DPI change. Budget real time for Phase 3.

**4. Acrylic ≠ `.ultraThinMaterial`.** Close, not identical, and acrylic is
disabled when the user turns off transparency effects or is on battery saver.
Needs a solid-colour fallback.

**5. `.NET 10 vs 8.`** Planned on 10 because that is the only SDK installed
here. If the target machine standardises on .NET 8 LTS, say so and it becomes a
TFM edit in `Directory.Build.props`.

**6. Prompt drift between the two apps.** ~~`SkillDefaults.cs` duplicates the
six system prompts from `Models.swift`.~~ **Addressed in Phase 1.** The prompts
are no longer transcribed: `windows/tools/sync-prompts.py` parses the Swift
sources (resolving Swift's `\`-continuation rules) and emits the `.g.cs` files,
which carry a do-not-edit header. Swift stays the single source of truth.

```sh
python3 windows/tools/sync-prompts.py           # regenerate after editing Swift
python3 windows/tools/sync-prompts.py --check   # exit 1 if stale — for CI
```

Residual risk: nothing *runs* `--check` automatically yet. Wire it into CI, or a
pre-commit hook, when there is one.

**7. Tray-icon dependency.** WinUI 3 ships no `NotifyIcon`. Plan uses
`H.NotifyIcon.WinUI`; the fallback if that proves awkward unpackaged is a direct
`Shell_NotifyIcon` P/Invoke against the same message-only window that already
handles `WM_HOTKEY`.

---

## 8. Out of scope

Carried over unchanged from the Mac roadmap — not built here:
multi-conversation history, a configurable-hotkey UI, code signing and an
installer, auto-update, and any local/offline model support.
