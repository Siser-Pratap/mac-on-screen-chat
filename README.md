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
- **Local & private** — talks to Ollama on `localhost`; nothing leaves your Mac.
- **Persistent** — your conversation and skills survive a restart.
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

## Run

```sh
swift run            # build + launch
# or
swift build && .build/debug/MacOnScreenChat
```

Headless data-layer check (no GUI):

```sh
.build/debug/MacOnScreenChat --selftest
```

## How it works

| Piece | File |
|---|---|
| Floating panel (always-on-top) | `FloatingPanel.swift`, `PanelController.swift` |
| Global hotkey (Carbon, no Accessibility perm) | `GlobalHotKey.swift` |
| Chat UI | `ContentView.swift`, `MessageBubble.swift` |
| View model / streaming | `ChatViewModel.swift` |
| LLM backend (swappable) | `LLMClient.swift`, `OllamaClient.swift`, `EchoClient` |
| Skills + history (SQLite/GRDB) | `Models.swift`, `AppDatabase.swift`, `SkillStore.swift` |
| Skill editor | `SkillEditor.swift` |
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
