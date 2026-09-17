# On-Screen Chat — Windows

A floating Windows assistant that lives above every other app. Summon it with a
global hotkey, pick a "skill," paste in a message, and get a ready-to-send reply.

Port of the macOS app in the repository root. Same skills, same prompts, same
SQLite-backed rules and history — rebuilt on **WinUI 3 / .NET**.

> **One deliberate difference: no local models.** The Mac app can run entirely
> offline through Ollama. This build is **Gemini only**, so a `GEMINI_API_KEY` is
> required — there is nothing to fall back to without one.

---

## Build and run

Needs **Windows 10 2004 (build 19041) or newer** and the **.NET SDK** with the
Windows workload. From this folder, in PowerShell:

```powershell
cp .env.example .env      # then put your Gemini key in it
.\build-app.ps1
.\publish\OnScreenChat.exe
```

That produces a self-contained folder — the target machine does not need the
.NET runtime or the Windows App SDK installed. Copy `publish\` anywhere.

For day-to-day development:

```powershell
dotnet run --project .\src\OnScreenChat -r win-x64
```

The app is **unsigned**, so SmartScreen will warn on first launch
("More info" → "Run anyway"). Code signing is deferred, exactly as on macOS.

---

## Using it

| | |
|---|---|
| **Ctrl+Shift+Space** | Show / hide the panel, from anywhere |
| **Esc** | Hide |
| **Enter** | Send |
| **Shift+Enter** | Newline |
| **Ctrl+.** | Stop a reply that's streaming (keeps what arrived) |
| **Tray icon** | Left-click toggles; right-click for the menu |

There is no taskbar button and no Alt+Tab entry — the tray icon is the only
persistent way to reach the app, so **quit from there**.

Drag the panel by its background; resize from the edges. It remembers where you
left it.

### Skills

Pick one from the header. Each is a reusable system prompt:

- **Plain chat** — normal conversation.
- **Connection writer** — paste a message or profile → a short connection note.
- **Reply analyzer** — paste an incoming message → warm / concise / formal replies.
- **Tone rewriter** — paste your draft → rewritten more warmly.
- **Comment writer** — paste a LinkedIn post → one thoughtful comment.
- **Dating reply** — paste the last few messages → playful / flirty / spicy options,
  with a heat dial and an editable "my texting style" block.

Skills are stored in SQLite and editable in-app (skill menu → "Edit …").

### Steering replies

```
/command {always answer in under 5 lines}    a standing rule — applies to every
                                             reply, across skills and restarts
WW:{one line, no labels}                     steers this one reply, then gone
```

Both markers are stripped from the message before it reaches the model. Manage
standing rules from the skill menu → "Rules …".

---

## Hidden from screen sharing

The panel is excluded from screen capture by default, staying fully visible on
your own display. Toggle it from the tray menu.

This uses `SetWindowDisplayAffinity(WDA_EXCLUDEFROMCAPTURE)`, which is honoured
by the modern Windows capture path — Teams, Zoom, Meet in a browser, Slack, Xbox
Game Bar, Win+Shift+S, PrintScreen.

**It is not a guarantee against every capture method.** Older DXGI desktop
duplication, which some OBS configurations and remote-desktop tools still use,
does not reliably respect it, and nothing stops a phone camera. Verify it against
whatever you actually screen-share with before relying on it, and check
`debug.log` — a failure to apply the flag is logged loudly.

This differs from macOS, where `NSWindow.sharingType = .none` is honoured
uniformly at the window-server level. Do not assume the two are equivalent.

---

## Where things live

```
%LOCALAPPDATA%\OnScreenChat\
  app.sqlite       skills, standing rules, conversation history
  settings.json    panel position, model choice, heat dial, texting style
  debug.log        launch / hotkey / window diagnostics
  .env             your Gemini key (copied here by build-app.ps1)
```

Your key is read at runtime from `.env`. It is **never** bundled into the
published output and never committed.

---

## Checking it without the GUI

```powershell
dotnet run --project .\src\OnScreenChat.SelfTest -- --selftest
dotnet run --project .\src\OnScreenChat.SelfTest -- --markers "summarize this /command {no emojis} WW:{one line}"
dotnet run --project .\src\OnScreenChat.SelfTest -- --gemini      # streams a real reply
```

The full test suite (191 tests) covers the whole non-UI half and runs on any
platform:

```powershell
dotnet test .\OnScreenChat.Core.slnf
```

---

## How it's put together

| Piece | Where |
|---|---|
| Floating always-on-top window | `Windowing/FloatingPanel.cs`, `PanelController.cs` |
| Global hotkey (`RegisterHotKey`) | `Interop/GlobalHotKey.cs`, `MessageWindow.cs` |
| Hidden from capture | `Interop/NativeMethods.cs`, `FloatingPanel.IsHiddenFromScreenShare` |
| Tray icon (`Shell_NotifyIcon`) | `Tray/TrayIconController.cs` |
| Single instance | `Interop/SingleInstance.cs`, `Program.cs` |
| Drag-anywhere regions | `Windowing/DragRegionManager.cs` |
| Chat UI | `Views/ChatPage.xaml`, `MessageBubble.xaml` |
| Send / stop / transcript rules | `Core/Chat/ChatSession.cs` |
| Gemini streaming | `Core/Llm/GeminiClient.cs`, `GeminiProtocol.cs` |
| Skills, rules, history | `Core/Data/` |
| Formatting + `**` stripping | `Core/Text/` |

Everything in `OnScreenChat.Core` is platform-free and covered by tests. Only
`src/OnScreenChat/` needs Windows to build — that split is deliberate, so the
losable logic stays verifiable anywhere.

### The prompts are generated, not copied

The six system prompts, the formatting contract and the heat calibrations are
generated from the macOS Swift sources, so the two apps cannot drift:

```sh
python3 tools/sync-prompts.py           # regenerate after editing the Swift
python3 tools/sync-prompts.py --check    # exit 1 if stale
```

Never edit a `.g.cs` file by hand. Edit the Swift source and re-run.

---

## Known gaps

- **Paragraph rendering is simpler than macOS.** The Mac app lays out each
  paragraph as its own view for real spacing between them; here a reply is one
  wrapped text block, so blank lines separate paragraphs but the gap is tighter.
- **No code signing**, no installer, no auto-update.
- **Acrylic isn't `.ultraThinMaterial`.** Close, and it degrades to a solid
  background when transparency effects are off or battery saver is on.
- Multi-conversation history and a configurable-hotkey UI are unbuilt on both
  platforms.
