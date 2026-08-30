# Mac On-Screen Chat

A floating macOS assistant that lives above every other app. Summon it with a
global hotkey, pick a "skill," paste in a message, and get a ready-to-send
reply — powered by a **local** model via [Ollama](https://ollama.com) (private,
no API key).

Think Spotlight/Raycast, but a conversational AI panel with reusable prompt
skills for outreach, replies, and rewriting.

## Features

- **Always on top** — a floating `NSPanel` that stays above other windows,
  including other apps in fullscreen.
- **Global hotkey** — **⌘⇧Space** toggles the panel from anywhere. **Esc** hides
  it. Drag it anywhere; it remembers its position.
- **Skills (special prompts)** — switch the system prompt from the picker:
  - **Plain chat** — normal conversation.
  - **Connection writer** — paste someone's message/profile → a short, genuine
    connection note with a real hook.
  - **Reply analyzer** — paste an incoming message → intent/tone read + three
    reply options (warm / concise / formal).
  - **Tone rewriter** — paste your draft → rewritten in a friendlier tone.
  - Skills are stored in SQLite and **editable** in-app (picker → "Edit …").
- **Switchable models** — pick from local Ollama models or Google Gemini via the
  `cpu` menu in the header. Local is private/offline; Gemini needs an API key.
- **Clean formatting, always** — every reply comes back in short paragraphs with
  real spacing between them, and never contains `**` (asked for in the prompt,
  and stripped from the stream so it's guaranteed). See
  [formatting.md](formatting.md).
- **Standing rules** — type `/command {always answer in under 5 lines}` and that
  rule applies to every reply from then on, across skills and restarts. Manage
  them in the picker → "Rules …". For a one-off nudge, use `WW:{…}` instead.
- **Stop** — the send button becomes a stop button (**⌘.**) while a reply
  streams; whatever arrived is kept.
- **Persistent** — your conversation, skills, and rules survive a restart.
- **Menu-bar icon** — toggle or quit (the app has no Dock icon by design).
- **Copy** — hover a reply to copy it.

## Requirements

- macOS 13+
- **Swift 6** toolchain (Command Line Tools or Xcode)
- **[Ollama](https://ollama.com)** running locally with a model pulled:
  ```sh
  ollama pull qwen3:30b   # default model
  ```
  Any chat model works — change the default in
  [`OllamaClient.swift`](Sources/MacOnScreenChat/OllamaClient.swift).

## Gemini (optional)

To use Gemini models, add your key to a local `.env` (gitignored, never committed):

```sh
cp .env.example .env
# edit .env and set GEMINI_API_KEY=...
./build-app.sh           # copies .env into the app's support folder
```

The key is read at runtime from `.env` / the app-support copy — it is **never**
bundled into the `.app` or committed. Local Ollama models need no key.

## Run

GUI apps must run as a bundle (a bare `swift run` won't show a window):

```sh
./build-app.sh           # builds MacOnScreenChat.app
open MacOnScreenChat.app  # launch
```

Headless data-layer check (no GUI):

```sh
.build/debug/MacOnScreenChat --selftest
```

Dry-run the input markers — shows what gets saved as a rule, what steers just
this reply, and what actually reaches the model (writes nothing):

```sh
.build/debug/MacOnScreenChat --markers 'summarize this /command {no emojis} WW:{one line}'
```

## How it works

| Piece | File |
|---|---|
| Floating panel (always-on-top) | `FloatingPanel.swift`, `PanelController.swift` |
| Global hotkey (Carbon, no Accessibility perm) | `GlobalHotKey.swift` |
| Chat UI | `ContentView.swift`, `MessageBubble.swift` |
| View model / streaming | `ChatViewModel.swift` |
| LLM backend (swappable) | `LLMClient.swift`, `OllamaClient.swift`, `EchoClient` |
| Reply formatting + `**` stripping | `ResponseStyle.swift` |
| Skills + history + rules (SQLite/GRDB) | `Models.swift`, `AppDatabase.swift`, `SkillStore.swift`, `RuleStore.swift` |
| Skill / rules editors | `SkillEditor.swift`, `RulesEditor.swift` |
| Menu-bar icon | `MenuBarController.swift` |

The LLM is behind an `LLMClient` protocol, so swapping Ollama for another
provider (e.g. a hosted API) is a single new type — no UI changes.

Data lives in `~/Library/Application Support/MacOnScreenChat/app.sqlite`.

## Roadmap

See [workflow.md](workflow.md). Not yet done:

- Model picker in-app (currently set in code).
- Multi-conversation history (currently a single rolling thread).
- Configurable hotkey UI.
- **Code-signing + notarization + `.dmg`** for sharing (deferred — runs
  unsigned locally for now).
